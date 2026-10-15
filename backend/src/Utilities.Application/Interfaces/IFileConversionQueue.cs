namespace Utilities.Application.Interfaces;

public interface IFileConversionQueue
{
    ValueTask QueueAsync(Guid jobId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}
