using Utilities.Domain.Entities;

namespace Utilities.Application.Interfaces;

public interface IImageCompressionEngine
{
    Task<ImageCompressionResult> CompressAsync(
        CompressionTask task,
        string inputPath,
        string outputPath,
        Action<double> reportProgress,
        CancellationToken cancellationToken);
}

public sealed record ImageCompressionResult(bool Success, long? CompressedSizeBytes, string? Error)
{
    public static ImageCompressionResult Succeeded(long compressedSizeBytes) =>
        new(true, compressedSizeBytes, null);

    public static ImageCompressionResult Failed(string error) => new(false, null, error);
}
