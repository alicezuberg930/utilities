using Utilities.Domain.Entities;

namespace Utilities.Application.Interfaces;

public interface IFileConversionEngine
{
    bool CanConvert(string originalFormat, string convertedFormat);

    Task<FileConversionResult> ConvertAsync(
        ConvertTask task,
        string inputPath,
        string outputPath,
        Action<double> reportProgress,
        CancellationToken cancellationToken);
}

public sealed record FileConversionResult(bool Success, string? Error)
{
    public static FileConversionResult Succeeded() => new(true, null);
    public static FileConversionResult Failed(string error) => new(false, error);
}
