namespace Utilities.Application.Interfaces;

public interface IImageCompressionQueue
{
    ValueTask QueueAsync(Guid jobId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}
