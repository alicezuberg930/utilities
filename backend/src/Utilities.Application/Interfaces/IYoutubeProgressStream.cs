using Utilities.Application.DTOs.Responses;

namespace Utilities.Application.Interfaces;

public interface IYoutubeProgressStream
{
    IYoutubeProgressSubscription Subscribe(Guid jobId);
}

public interface IYoutubeProgressSubscription : IDisposable
{
    IAsyncEnumerable<YoutubeProgressEvent> ReadAllAsync(CancellationToken cancellationToken);
}

public interface IYoutubeProgressPublisher
{
    void PublishProgress(Guid jobId, double progress);
    void Complete(Guid jobId);
    void Fail(Guid jobId, string message);
}
