using Utilities.Application.Interfaces;
using Utilities.Domain.Entities;

namespace Utilities.Infrastructure.External.Conversion;

public sealed class FileConversionDispatcher(IEnumerable<IFileConversionEngine> engines)
{
    private readonly IReadOnlyList<IFileConversionEngine> _engines = engines.ToArray();

    public Task<FileConversionResult> ConvertAsync(
        ConvertTask task,
        string inputPath,
        string outputPath,
        Action<double> reportProgress,
        CancellationToken cancellationToken)
    {
        var matches = _engines
            .Where(engine => engine.CanConvert(task.OriginalFormat, task.ConvertedFormat))
            .Take(2)
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0].ConvertAsync(
                task,
                inputPath,
                outputPath,
                reportProgress,
                cancellationToken),
            0 => Task.FromResult(FileConversionResult.Failed(
                $"No converter is available for '{task.OriginalFormat}' to '{task.ConvertedFormat}'.")),
            _ => throw new InvalidOperationException(
                $"Multiple converters are registered for '{task.OriginalFormat}' to '{task.ConvertedFormat}'."),
        };
    }
}
