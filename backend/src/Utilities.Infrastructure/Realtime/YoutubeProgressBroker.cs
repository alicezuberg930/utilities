using System.Collections.Concurrent;
using System.Threading.Channels;
using Utilities.Application.DTOs.Responses;
using Utilities.Application.Interfaces;

namespace Utilities.Infrastructure.Realtime;

public sealed class JobProgressBroker : IYoutubeProgressStream, IFileProgressStream, IJobProgressPublisher
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<JobProgressEvent>>> _subscribers = new();

    public IJobProgressSubscription Subscribe(Guid jobId)
    {
        var subscriberId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<JobProgressEvent>();
        var subscribers = _subscribers.GetOrAdd(jobId, _ => new());
        subscribers[subscriberId] = channel;

        return new JobProgressSubscription(channel.Reader, () => Remove(jobId, subscriberId));
    }

    public void PublishProgress(Guid jobId, double progress) => Publish(jobId, new JobProgressEvent(null, new JobProgress(progress)));

    public void Complete(Guid jobId)
    {
        if (!_subscribers.TryRemove(jobId, out var subscribers)) return;

        foreach (var channel in subscribers.Values)
        {
            channel.Writer.TryWrite(new JobProgressEvent("complete", "done"));
            channel.Writer.TryComplete();
        }
    }

    public void Fail(Guid jobId, string message)
    {
        if (!_subscribers.TryRemove(jobId, out var subscribers)) return;

        foreach (var channel in subscribers.Values)
        {
            channel.Writer.TryWrite(new JobProgressEvent("error", new JobFailure(message)));
            channel.Writer.TryComplete();
        }
    }

    private void Publish(Guid jobId, JobProgressEvent progressEvent)
    {
        if (!_subscribers.TryGetValue(jobId, out var subscribers)) return;

        foreach (var channel in subscribers.Values)
        {
            channel.Writer.TryWrite(progressEvent);
        }
    }

    private void Remove(Guid jobId, Guid subscriberId)
    {
        if (!_subscribers.TryGetValue(jobId, out var subscribers)) return;

        subscribers.TryRemove(subscriberId, out _);
        if (subscribers.IsEmpty)
        {
            _subscribers.TryRemove(jobId, out _);
        }
    }

    private sealed class JobProgressSubscription(ChannelReader<JobProgressEvent> reader, Action unsubscribe) : IJobProgressSubscription
    {
        public IAsyncEnumerable<JobProgressEvent> ReadAllAsync(CancellationToken cancellationToken) => reader.ReadAllAsync(cancellationToken);

        public void Dispose() => unsubscribe();
    }
}
