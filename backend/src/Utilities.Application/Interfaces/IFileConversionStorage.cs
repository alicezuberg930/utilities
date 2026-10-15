using Utilities.Domain.Entities;

namespace Utilities.Application.Interfaces;

public interface IFileConversionStorage
{
    string GetInputPath(Guid jobId, string originalFormat);
    string GetOutputPath(ConvertTask task);
    Task SaveInputAsync(Guid jobId, string originalFormat, Stream content, CancellationToken cancellationToken);
    bool Exists(string path);
    void DeleteIfExists(string path);
    void EnsureDirectoryExists();
}
