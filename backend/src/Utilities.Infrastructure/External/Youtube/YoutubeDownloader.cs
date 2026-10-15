using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utilities.Domain.Constants;
using Utilities.Domain.Entities;
using Utilities.Infrastructure.Configuration;

namespace Utilities.Infrastructure.External.Youtube;

public sealed partial class YoutubeDownloader(
    IOptions<YoutubeOptions> options,
    ILogger<YoutubeDownloader> logger)
{
    private readonly YoutubeOptions _options = options.Value;

    public async Task<YoutubeDownloadResult> DownloadAsync(
        YoutubeTask task,
        string outputPath,
        Action<double> reportProgress,
        CancellationToken cancellationToken)
    {
        using var process = CreateDownloadProcess(task, outputPath);
        var errors = new StringBuilder();
        var errorLock = new object();

        process.OutputDataReceived += (_, args) => HandleOutput(args.Data, reportProgress);
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is null) return;

            HandleOutput(args.Data, reportProgress);
            lock (errorLock)
            {
                if (errors.Length < 4000)
                {
                    errors.AppendLine(args.Data);
                }
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            using var cancellationRegistration = cancellationToken.Register(() => TryKill(process));
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            logger.LogError(exception, "Could not start yt-dlp at {ExecutablePath}", _options.ExecutablePath);
            return YoutubeDownloadResult.Failed("The YouTube downloader is not available.");
        }

        if (process.ExitCode == 0)
        {
            return YoutubeDownloadResult.Succeeded();
        }

        logger.LogWarning(
            "yt-dlp job {JobId} exited with code {ExitCode}: {Error}",
            task.JobId,
            process.ExitCode,
            errors);
        return YoutubeDownloadResult.Failed("The YouTube download failed.");
    }

    private Process CreateDownloadProcess(YoutubeTask task, string outputPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        if (task.Format == YoutubeFormats.Audio)
        {
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add(BuildAudioFormatSelector(task.AudioQuality));
            startInfo.ArgumentList.Add("-x");
            startInfo.ArgumentList.Add("--audio-format");
            startInfo.ArgumentList.Add("mp3");
        }
        else
        {
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add(BuildVideoFormatSelector(task.VideoQuality));
            startInfo.ArgumentList.Add("--merge-output-format");
            startInfo.ArgumentList.Add("mp4");
        }

        startInfo.ArgumentList.Add("--newline");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(outputPath);
        startInfo.ArgumentList.Add(task.Url);

        return new Process { StartInfo = startInfo };
    }

    private static string BuildAudioFormatSelector(double? audioQuality)
    {
        if (audioQuality is null || !double.IsFinite(audioQuality.Value) || audioQuality <= 0)
        {
            throw new InvalidOperationException($"Unsupported audio quality '{audioQuality}'.");
        }

        var bitrate = audioQuality.Value.ToString("R", CultureInfo.InvariantCulture);
        return $"ba[abr<={bitrate}]";
    }

    private static string BuildVideoFormatSelector(string? videoQuality)
    {
        var height = videoQuality switch
        {
            "360p" => 360,
            "480p" => 480,
            "720p" => 720,
            "1080p" => 1080,
            _ => throw new InvalidOperationException($"Unsupported video quality '{videoQuality}'."),
        };

        return $"bv*[height<={height}][ext=mp4]+ba[ext=m4a]/b[height<={height}][ext=mp4]";
    }

    private static void HandleOutput(string? output, Action<double> reportProgress)
    {
        if (output is null) return;

        var match = DownloadProgressRegex().Match(output);
        if (match.Success &&
            double.TryParse(
                match.Groups[1].Value,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var progress))
        {
            reportProgress(progress);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the state check and the kill request.
        }
    }

    [GeneratedRegex(@"\[download\]\s+(\d+(?:\.\d+)?)%", RegexOptions.CultureInvariant)]
    private static partial Regex DownloadProgressRegex();
}

public sealed record YoutubeDownloadResult(bool Success, string? Error)
{
    public static YoutubeDownloadResult Succeeded() => new(true, null);
    public static YoutubeDownloadResult Failed(string error) => new(false, error);
}
