namespace Utilities.Infrastructure.External.Conversion;

public interface ICadConverterProcess
{
    Task<CadConverterProcessResult> ConvertAsync(
        CadConverterProcessRequest request,
        Action<double> reportProgress,
        CancellationToken cancellationToken);
}

public sealed record CadConverterProcessRequest(
    string InputPath,
    string OutputPath,
    string InputFormat);

public sealed record CadConverterProcessResult(
    int ExitCode,
    bool TimedOut,
    string Diagnostics)
{
    public bool Success => !TimedOut && ExitCode == CadConverterExitCodes.Success;
}

public static class CadConverterExitCodes
{
    public const int Success = 0;
    public const int InvalidInput = 2;
    public const int Unavailable = 3;
    public const int ImportFailed = 4;
    public const int ExportFailed = 5;
    public const int InternalError = 6;
    public const int ResourceLimitExceeded = 7;
}
