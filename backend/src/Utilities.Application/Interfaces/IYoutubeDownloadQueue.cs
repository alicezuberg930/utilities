namespace Utilities.Application.Interfaces;

public interface IYoutubeDownloadQueue
{
    ValueTask QueueAsync(Guid jobId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}
