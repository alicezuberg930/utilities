using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utilities.Application.Interfaces;
using Utilities.Domain.Constants;
using Utilities.Domain.Entities;
using Utilities.Infrastructure.Configuration;

namespace Utilities.Infrastructure.External.Conversion;

public sealed class CadFileConversionEngine(
    ICadConverterProcess converterProcess,
    IOptions<FileConversionOptions> options,
    ILogger<CadFileConversionEngine> logger) : IFileConversionEngine
{
    private const string ResourceLimitFailureMessage =
        "The CAD file exceeds the configured conversion complexity or output-size limit.";
    private readonly FileConversionOptions _options = options.Value;

    public bool CanConvert(string originalFormat, string convertedFormat) =>
        FileConversionFormats.IsCadConversion(originalFormat, convertedFormat);

    public async Task<FileConversionResult> ConvertAsync(
        ConvertTask task,
        string inputPath,
        string outputPath,
        Action<double> reportProgress,
        CancellationToken cancellationToken)
    {
        var partialOutputPath = $"{outputPath}.{Guid.NewGuid():N}.partial.glb";

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            reportProgress(5);

            var processResult = await converterProcess.ConvertAsync(
                new CadConverterProcessRequest(
                    inputPath,
                    partialOutputPath,
                    task.OriginalFormat),
                progress => reportProgress(Math.Clamp(5 + progress * 0.9, 5, 95)),
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(processResult.Diagnostics))
            {
                logger.LogWarning(
                    "CAD converter diagnostics for job {JobId}: {Diagnostics}",
                    task.JobId,
                    processResult.Diagnostics);
            }

            if (!processResult.Success)
            {
                logger.LogWarning(
                    "CAD converter failed for job {JobId} with exit code {ExitCode}; timed out: {TimedOut}",
                    task.JobId,
                    processResult.ExitCode,
                    processResult.TimedOut);
                return FileConversionResult.Failed(GetFailureMessage(processResult));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(partialOutputPath))
            {
                logger.LogWarning(
                    "CAD converter reported success without creating output for job {JobId}",
                    task.JobId);
                return FileConversionResult.Failed(
                    "The CAD converter did not produce an output file.");
            }

            var outputLength = new FileInfo(partialOutputPath).Length;
            if (outputLength > _options.CadMaxOutputBytes)
            {
                logger.LogWarning(
                    "CAD converter output for job {JobId} was {OutputBytes} bytes, exceeding the configured limit of {MaximumOutputBytes} bytes",
                    task.JobId,
                    outputLength,
                    _options.CadMaxOutputBytes);
                return FileConversionResult.Failed(ResourceLimitFailureMessage);
            }

            try
            {
                await GlbValidator.ValidateAsync(partialOutputPath, cancellationToken);
            }
            catch (InvalidDataException exception)
            {
                logger.LogWarning(
                    exception,
                    "CAD converter produced an invalid GLB for job {JobId}",
                    task.JobId);
                return FileConversionResult.Failed(
                    "The CAD converter produced an invalid or empty GLB file.");
            }

            reportProgress(97);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partialOutputPath, outputPath, overwrite: true);
            reportProgress(99);

            logger.LogInformation(
                "CAD conversion job {JobId} completed with the external converter",
                task.JobId);
            return FileConversionResult.Succeeded();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or
                                          UnauthorizedAccessException or
                                          NotSupportedException)
        {
            logger.LogWarning(exception, "CAD conversion job {JobId} failed", task.JobId);
            return FileConversionResult.Failed(
                "The CAD file could not be converted. It may be malformed or contain unsupported geometry.");
        }
        finally
        {
            try
            {
                File.Delete(partialOutputPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(
                    exception,
                    "Could not remove partial CAD output for job {JobId}",
                    task.JobId);
            }
        }
    }

    private static string GetFailureMessage(CadConverterProcessResult result)
    {
        if (result.TimedOut)
        {
            return "The CAD conversion exceeded the configured time limit.";
        }

        return result.ExitCode switch
        {
            CadConverterExitCodes.InvalidInput =>
                "The CAD input is invalid or uses an unsupported file variant.",
            CadConverterExitCodes.Unavailable =>
                "The CAD converter is not available.",
            CadConverterExitCodes.ImportFailed =>
                "The CAD file could not be read. It may be malformed or use unsupported geometry.",
            CadConverterExitCodes.ExportFailed =>
                "The CAD geometry could not be completely tessellated and exported.",
            CadConverterExitCodes.ResourceLimitExceeded =>
                ResourceLimitFailureMessage,
            _ => "The CAD conversion failed unexpectedly.",
        };
    }
}
