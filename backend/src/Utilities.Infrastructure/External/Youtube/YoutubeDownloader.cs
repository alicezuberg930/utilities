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
        YoutubeJob job,
        string outputPath,
        Action<double> reportProgress,
        CancellationToken cancellationToken)
    {
        using var process = CreateDownloadProcess(job, outputPath);
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
            job.JobId,
            process.ExitCode,
            errors);
        return YoutubeDownloadResult.Failed("The YouTube download failed.");
    }

    private Process CreateDownloadProcess(YoutubeJob job, string outputPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        if (job.Format == YoutubeFormats.Audio)
        {
            startInfo.ArgumentList.Add("-x");
            startInfo.ArgumentList.Add("--audio-format");
            startInfo.ArgumentList.Add("mp3");
        }
        else
        {
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("bv*+ba/b");
            startInfo.ArgumentList.Add("--merge-output-format");
            startInfo.ArgumentList.Add("mp4");
        }

        startInfo.ArgumentList.Add("--newline");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(outputPath);
        startInfo.ArgumentList.Add(job.Url);

        return new Process { StartInfo = startInfo };
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
