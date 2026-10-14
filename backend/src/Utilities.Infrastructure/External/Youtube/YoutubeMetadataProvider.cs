using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utilities.Application.DTOs.Responses;
using Utilities.Application.Exceptions;
using Utilities.Application.Interfaces;
using Utilities.Infrastructure.Configuration;

namespace Utilities.Infrastructure.External.Youtube;

public sealed class YoutubeMetadataProvider(
    IOptions<YoutubeOptions> options,
    ILogger<YoutubeMetadataProvider> logger) : IYoutubeMetadataProvider
{
    private readonly YoutubeOptions _options = options.Value;

    public async Task<YoutubeMetadataResponse> GetInfoAsync(string url, CancellationToken cancellationToken)
    {
        using var process = CreateProcess("--dump-json", url);
        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            logger.LogError(exception, "Could not start yt-dlp at {ExecutablePath}", _options.ExecutablePath);
            throw new ExternalServiceException("The YouTube downloader is not available.", ExternalServiceFailure.Unavailable, exception);
        }

        using var cancellationRegistration = cancellationToken.Register(() => TryKill(process));
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            logger.LogWarning("yt-dlp metadata request failed with exit code {ExitCode}: {Error}", process.ExitCode, stderr);
            throw new RequestValidationException("Unable to read metadata from the supplied YouTube URL.");
        }

        try
        {
            using var document = JsonDocument.Parse(stdout);
            return ExtractStreams(document.RootElement);
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "yt-dlp returned invalid metadata JSON.");
            throw new ExternalServiceException(
                "The YouTube downloader returned an invalid response.",
                ExternalServiceFailure.InvalidResponse,
                exception);
        }
    }

    private Process CreateProcess(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return new Process { StartInfo = startInfo };
    }

    private static YoutubeMetadataResponse ExtractStreams(JsonElement metadata)
    {
        var videoFormats = new List<JsonElement>();
        var audioFormats = new List<JsonElement>();

        if (metadata.TryGetProperty("formats", out var formats) && formats.ValueKind == JsonValueKind.Array)
        {
            foreach (var format in formats.EnumerateArray())
            {
                var videoExtension = GetString(format, "video_ext");
                var audioExtension = GetString(format, "audio_ext");

                if ((videoExtension is "mp4" or "webm") && audioExtension == "none")
                {
                    videoFormats.Add(format.Clone());
                }

                if ((audioExtension is "webm" or "m4a") && videoExtension == "none")
                {
                    audioFormats.Add(format.Clone());
                }
            }
        }

        return new YoutubeMetadataResponse(GetString(metadata, "title"), GetString(metadata, "thumbnail"), videoFormats, audioFormats, GetString(metadata, "format_id"));
    }

    private static string? GetString(JsonElement element, string propertyName) => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;

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
