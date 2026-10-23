using Utilities.Domain.Entities;

namespace Utilities.Application.Interfaces;

public interface IImageCompressionStorage
{
    string GetInputPath(Guid jobId, string originalFormat);
    string GetOutputPath(CompressionTask task);
    Task SaveInputAsync(
        Guid jobId,
        string originalFormat,
        Stream content,
        CancellationToken cancellationToken);
    bool Exists(string path);
    long GetFileSize(string path);
    void DeleteIfExists(string path);
    void EnsureDirectoryExists();
}
