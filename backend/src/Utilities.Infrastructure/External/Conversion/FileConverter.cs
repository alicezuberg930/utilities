using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utilities.Domain.Constants;
using Utilities.Domain.Entities;
using Utilities.Infrastructure.Configuration;

namespace Utilities.Infrastructure.External.Conversion;

public sealed class FileConverter(
    IOptions<FileConversionOptions> options,
    ILogger<FileConverter> logger)
{
    private readonly FileConversionOptions _options = options.Value;

    public async Task<FileConversionResult> ConvertAsync(
        ConvertTask task,
        string inputPath,
        string outputPath,
        Action<double> reportProgress,
        CancellationToken cancellationToken)
    {
        var durationSeconds = await GetDurationSecondsAsync(inputPath, cancellationToken);
        using var process = CreateConversionProcess(task, inputPath, outputPath);
        var errors = new StringBuilder();
        var errorLock = new object();

        process.OutputDataReceived += (_, args) =>
            HandleProgress(args.Data, durationSeconds, reportProgress);
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is null) return;

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
            logger.LogError(exception, "Could not start FFmpeg at {ExecutablePath}", _options.FfmpegExecutablePath);
            return FileConversionResult.Failed("The file converter is not available.");
        }

        if (process.ExitCode == 0)
        {
            return FileConversionResult.Succeeded();
        }

        logger.LogWarning(
            "FFmpeg conversion job {JobId} exited with code {ExitCode}: {Error}",
            task.JobId,
            process.ExitCode,
            errors);
        return FileConversionResult.Failed("The file conversion failed.");
    }

    private Process CreateConversionProcess(
        ConvertTask task,
        string inputPath,
        string outputPath)
    {
        var startInfo = CreateStartInfo(_options.FfmpegExecutablePath);
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(inputPath);

        if (FileConversionFormats.AudioOutputs.Contains(task.ConvertedFormat))
        {
            startInfo.ArgumentList.Add("-map");
            startInfo.ArgumentList.Add("0:a:0");
            startInfo.ArgumentList.Add("-vn");
            AddAudioCodecArguments(startInfo, task.ConvertedFormat);
        }
        else
        {
            startInfo.ArgumentList.Add("-map");
            startInfo.ArgumentList.Add("0:v:0");
            startInfo.ArgumentList.Add("-map");
            startInfo.ArgumentList.Add("0:a:0?");
            AddVideoCodecArguments(startInfo, task.ConvertedFormat);
        }

        startInfo.ArgumentList.Add("-progress");
        startInfo.ArgumentList.Add("pipe:1");
        startInfo.ArgumentList.Add("-nostats");
        startInfo.ArgumentList.Add(outputPath);
        return new Process { StartInfo = startInfo };
    }

    private async Task<double?> GetDurationSecondsAsync(
        string inputPath,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = CreateStartInfo(_options.FfprobeExecutablePath),
        };
        process.StartInfo.ArgumentList.Add("-v");
        process.StartInfo.ArgumentList.Add("error");
        process.StartInfo.ArgumentList.Add("-show_entries");
        process.StartInfo.ArgumentList.Add("format=duration");
        process.StartInfo.ArgumentList.Add("-of");
        process.StartInfo.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
        process.StartInfo.ArgumentList.Add(inputPath);

        try
        {
            process.Start();
            using var cancellationRegistration = cancellationToken.Register(() => TryKill(process));
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask;
            _ = await errorTask;

            return process.ExitCode == 0 &&
                   double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) &&
                   duration > 0
                ? duration
                : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            logger.LogWarning(exception, "Could not read media duration with FFprobe at {ExecutablePath}", _options.FfprobeExecutablePath);
            return null;
        }
    }

    private static ProcessStartInfo CreateStartInfo(string executablePath) => new()
    {
        FileName = executablePath,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
    };

    private static void AddAudioCodecArguments(ProcessStartInfo startInfo, string format)
    {
        var codec = format switch
        {
            "aac" or "m4a" => "aac",
            "flac" => "flac",
            "mp3" => "libmp3lame",
            "ogg" => "libvorbis",
            "opus" => "libopus",
            "wav" => "pcm_s16le",
            _ => throw new InvalidOperationException($"Unsupported audio output format '{format}'."),
        };

        startInfo.ArgumentList.Add("-c:a");
        startInfo.ArgumentList.Add(codec);
    }

    private static void AddVideoCodecArguments(ProcessStartInfo startInfo, string format)
    {
        var (videoCodec, audioCodec) = format switch
        {
            "avi" => ("mpeg4", "libmp3lame"),
            "mkv" => ("libx264", "aac"),
            "mov" or "mp4" => ("libx264", "aac"),
            "webm" => ("libvpx-vp9", "libopus"),
            _ => throw new InvalidOperationException($"Unsupported video output format '{format}'."),
        };

        startInfo.ArgumentList.Add("-c:v");
        startInfo.ArgumentList.Add(videoCodec);
        startInfo.ArgumentList.Add("-c:a");
        startInfo.ArgumentList.Add(audioCodec);

        if (format is "mov" or "mp4")
        {
            startInfo.ArgumentList.Add("-movflags");
            startInfo.ArgumentList.Add("+faststart");
        }
    }

    private static void HandleProgress(
        string? output,
        double? durationSeconds,
        Action<double> reportProgress)
    {
        if (output is null || durationSeconds is null || !output.StartsWith("out_time_us=", StringComparison.Ordinal))
        {
            return;
        }

        if (long.TryParse(output.AsSpan("out_time_us=".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var elapsedMicroseconds))
        {
            var progress = Math.Clamp(elapsedMicroseconds / (durationSeconds.Value * 1_000_000d) * 100d, 0d, 99d);
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
}

public sealed record FileConversionResult(bool Success, string? Error)
{
    public static FileConversionResult Succeeded() => new(true, null);
    public static FileConversionResult Failed(string error) => new(false, error);
}
