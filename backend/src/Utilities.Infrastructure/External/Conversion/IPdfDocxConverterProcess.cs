namespace Utilities.Infrastructure.External.Conversion;

public interface IPdfDocxConverterProcess
{
    Task<PdfDocxConverterProcessResult> ConvertAsync(
        PdfDocxConverterProcessRequest request,
        Action<double> reportProgress,
        CancellationToken cancellationToken);
}

public sealed record PdfDocxConverterProcessRequest(
    string InputPath,
    string OutputPath);

public sealed record PdfDocxConverterProcessResult(
    int ExitCode,
    bool TimedOut,
    string Diagnostics)
{
    public bool Success => !TimedOut && ExitCode == PdfDocxConverterExitCodes.Success;
}

public static class PdfDocxConverterExitCodes
{
    public const int Success = 0;
    public const int InvalidInput = 2;
    public const int Unavailable = 3;
    public const int ReadFailed = 4;
    public const int WriteFailed = 5;
    public const int InternalError = 6;
    public const int ResourceLimitExceeded = 7;
}
