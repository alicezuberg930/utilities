using Utilities.Application.DTOs.Responses;

namespace Utilities.Application.Interfaces;

public interface IJobProgressStream
{
    IJobProgressSubscription Subscribe(Guid jobId);
}

public interface IYoutubeProgressStream : IJobProgressStream
{
}

public interface IFileProgressStream : IJobProgressStream
{
}

public interface IJobProgressSubscription : IDisposable
{
    IAsyncEnumerable<JobProgressEvent> ReadAllAsync(CancellationToken cancellationToken);
}

public interface IJobProgressPublisher
{
    void PublishProgress(Guid jobId, double progress);
    void Complete(Guid jobId);
    void Fail(Guid jobId, string message);
}
