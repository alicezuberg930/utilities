using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utilities.Application.Interfaces;
using Utilities.Domain.Constants;
using Utilities.Domain.Entities;
using Utilities.Infrastructure.Configuration;

namespace Utilities.Infrastructure.External.Conversion;

public sealed class PdfDocxFileConversionEngine(
    IPdfDocxConverterProcess converterProcess,
    IOptions<FileConversionOptions> options,
    ILogger<PdfDocxFileConversionEngine> logger) : IFileConversionEngine
{
    private const string ResourceLimitFailureMessage =
        "The PDF exceeds the configured page-count, input-size, or output-size limit.";
    private readonly FileConversionOptions _options = options.Value;

    public bool CanConvert(string originalFormat, string convertedFormat) =>
        FileConversionFormats.IsDocumentConversion(originalFormat, convertedFormat);

    public async Task<FileConversionResult> ConvertAsync(
        ConvertTask task,
        string inputPath,
        string outputPath,
        Action<double> reportProgress,
        CancellationToken cancellationToken)
    {
        var partialOutputPath = $"{outputPath}.{Guid.NewGuid():N}.partial.docx";

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            reportProgress(5);

            var processResult = await converterProcess.ConvertAsync(
                new PdfDocxConverterProcessRequest(inputPath, partialOutputPath),
                progress => reportProgress(Math.Clamp(5 + progress * 0.9, 5, 95)),
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(processResult.Diagnostics))
            {
                logger.LogWarning(
                    "PDF to DOCX converter diagnostics for job {JobId}: {Diagnostics}",
                    task.JobId,
                    processResult.Diagnostics);
            }

            if (!processResult.Success)
            {
                logger.LogWarning(
                    "PDF to DOCX converter failed for job {JobId} with exit code {ExitCode}; timed out: {TimedOut}",
                    task.JobId,
                    processResult.ExitCode,
                    processResult.TimedOut);
                return FileConversionResult.Failed(GetFailureMessage(processResult));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(partialOutputPath))
            {
                logger.LogWarning(
                    "PDF to DOCX converter reported success without creating output for job {JobId}",
                    task.JobId);
                return FileConversionResult.Failed(
                    "The PDF to DOCX converter did not produce an output file.");
            }

            var outputLength = new FileInfo(partialOutputPath).Length;
            if (outputLength == 0)
            {
                logger.LogWarning(
                    "PDF to DOCX converter produced an empty output for job {JobId}",
                    task.JobId);
                return FileConversionResult.Failed(
                    "The PDF to DOCX converter produced an invalid or empty DOCX file.");
            }

            if (outputLength > _options.PdfDocxMaxOutputBytes)
            {
                logger.LogWarning(
                    "PDF to DOCX converter output for job {JobId} was {OutputBytes} bytes; configured maximum is {MaximumOutputBytes} bytes",
                    task.JobId,
                    outputLength,
                    _options.PdfDocxMaxOutputBytes);
                return FileConversionResult.Failed(ResourceLimitFailureMessage);
            }

            try
            {
                await DocxValidator.ValidateAsync(partialOutputPath, cancellationToken);
            }
            catch (InvalidDataException exception)
            {
                logger.LogWarning(
                    exception,
                    "PDF to DOCX converter produced an invalid DOCX for job {JobId}",
                    task.JobId);
                return FileConversionResult.Failed(
                    "The PDF to DOCX converter produced an invalid or empty DOCX file.");
            }

            reportProgress(97);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partialOutputPath, outputPath, overwrite: true);
            reportProgress(99);

            logger.LogInformation(
                "PDF to DOCX conversion job {JobId} completed with the external converter",
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
            logger.LogWarning(exception, "PDF to DOCX conversion job {JobId} failed", task.JobId);
            return FileConversionResult.Failed(
                "The PDF could not be converted. It may be malformed, encrypted, or unsupported.");
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
                    "Could not remove partial PDF to DOCX output for job {JobId}",
                    task.JobId);
            }
        }
    }

    private static string GetFailureMessage(PdfDocxConverterProcessResult result)
    {
        if (result.TimedOut)
        {
            return "The PDF to DOCX conversion exceeded the configured time limit.";
        }

        return result.ExitCode switch
        {
            PdfDocxConverterExitCodes.InvalidInput =>
                "The PDF input is invalid, encrypted, or uses an unsupported file variant.",
            PdfDocxConverterExitCodes.Unavailable =>
                "The PDF to DOCX converter is not available.",
            PdfDocxConverterExitCodes.ReadFailed =>
                "The PDF could not be read. It may be malformed, encrypted, or unsupported.",
            PdfDocxConverterExitCodes.WriteFailed =>
                "The converted Word document could not be generated or written.",
            PdfDocxConverterExitCodes.ResourceLimitExceeded =>
                ResourceLimitFailureMessage,
            _ => "The PDF to DOCX conversion failed unexpectedly.",
        };
    }
}
