using System.Threading.Channels;
using DocIntake.Core.Abstractions;

namespace DocIntake.Infrastructure.Queue;

/// <summary>
/// In-process queue built on <see cref="Channel{T}"/>. Jobs are lost on restart;
/// a durable broker (e.g. a message queue) would replace this in production.
/// </summary>
public class ChannelJobQueue : IJobQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public Task EnqueueAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(documentId, cancellationToken).AsTask();

    public Task<Guid> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken).AsTask();
}
