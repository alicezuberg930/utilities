using Microsoft.Extensions.Options;
using Utilities.Application.Interfaces;
using Utilities.Domain.Entities;
using Utilities.Infrastructure.Configuration;

namespace Utilities.Infrastructure.Services;

public sealed class FileConversionStorage(IOptions<FileConversionOptions> options) : IFileConversionStorage
{
    private readonly FileConversionOptions _options = options.Value;

    public string GetInputPath(Guid jobId, string originalFormat) =>
        Path.Combine(_options.JobsDirectory, $"{jobId:N}.source.{originalFormat}");

    public string GetOutputPath(ConvertTask task) =>
        Path.Combine(_options.JobsDirectory, $"{task.JobId:N}.{task.ConvertedFormat}");

    public async Task SaveInputAsync(
        Guid jobId,
        string originalFormat,
        Stream content,
        CancellationToken cancellationToken)
    {
        EnsureDirectoryExists();
        var path = GetInputPath(jobId, originalFormat);
        await using var output = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await content.CopyToAsync(output, cancellationToken);
    }

    public bool Exists(string path) => File.Exists(path);

    public void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void EnsureDirectoryExists() => Directory.CreateDirectory(_options.JobsDirectory);
}
