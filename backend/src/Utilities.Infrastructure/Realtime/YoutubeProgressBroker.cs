using System.Collections.Concurrent;
using System.Threading.Channels;
using Utilities.Application.DTOs.Responses;
using Utilities.Application.Interfaces;

namespace Utilities.Infrastructure.Realtime;

public sealed class YoutubeProgressBroker : IYoutubeProgressStream, IYoutubeProgressPublisher
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<YoutubeProgressEvent>>> _subscribers = new();

    public IYoutubeProgressSubscription Subscribe(Guid jobId)
    {
        var subscriberId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<YoutubeProgressEvent>();
        var subscribers = _subscribers.GetOrAdd(jobId, _ => new());
        subscribers[subscriberId] = channel;

        return new YoutubeProgressSubscription(channel.Reader, () => Remove(jobId, subscriberId));
    }

    public void PublishProgress(Guid jobId, double progress) => Publish(jobId, new YoutubeProgressEvent(null, new YoutubeProgress(progress)));

    public void Complete(Guid jobId)
    {
        if (!_subscribers.TryRemove(jobId, out var subscribers)) return;

        foreach (var channel in subscribers.Values)
        {
            channel.Writer.TryWrite(new YoutubeProgressEvent("complete", "done"));
            channel.Writer.TryComplete();
        }
    }

    public void Fail(Guid jobId, string message)
    {
        if (!_subscribers.TryRemove(jobId, out var subscribers)) return;

        foreach (var channel in subscribers.Values)
        {
            channel.Writer.TryWrite(new YoutubeProgressEvent("error", new YoutubeFailure(message)));
            channel.Writer.TryComplete();
        }
    }

    private void Publish(Guid jobId, YoutubeProgressEvent progressEvent)
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

    private sealed class YoutubeProgressSubscription(ChannelReader<YoutubeProgressEvent> reader, Action unsubscribe) : IYoutubeProgressSubscription
    {
        public IAsyncEnumerable<YoutubeProgressEvent> ReadAllAsync(CancellationToken cancellationToken) => reader.ReadAllAsync(cancellationToken);

        public void Dispose() => unsubscribe();
    }
}
