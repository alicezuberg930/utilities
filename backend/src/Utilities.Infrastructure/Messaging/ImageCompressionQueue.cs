using System.Threading.Channels;
using Utilities.Application.Interfaces;

namespace Utilities.Infrastructure.Messaging;

public sealed class ImageCompressionQueue : IImageCompressionQueue
{
    private readonly Channel<Guid> _jobs = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    public ValueTask QueueAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        _jobs.Writer.WriteAsync(jobId, cancellationToken);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) =>
        _jobs.Reader.ReadAllAsync(cancellationToken);
}
