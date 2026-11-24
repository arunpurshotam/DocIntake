using DocIntake.Core.Abstractions;
using DocIntake.Core.Domain;
using DocIntake.Infrastructure.Queue;
using DocIntake.Infrastructure.Resilience;
using DocIntake.Infrastructure.Storage;

namespace DocIntake.Tests.Infrastructure;

public class QueueAndResilienceTests
{
    private static readonly ExtractionResult AnyResult = new(new Dictionary<string, string>(), 0.95);

    [Fact]
    public async Task ChannelJobQueue_DeliversIdsInOrder()
    {
        var queue = new ChannelJobQueue();
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());

        await queue.EnqueueAsync(a);
        await queue.EnqueueAsync(b);

        Assert.Equal(a, await queue.DequeueAsync(CancellationToken.None));
        Assert.Equal(b, await queue.DequeueAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ChannelJobQueue_Dequeue_StopsWhenCancelled()
    {
        using var cts = new CancellationTokenSource();
        var pending = new ChannelJobQueue().DequeueAsync(cts.Token); // nothing queued, so it waits

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task InMemoryDeadLetterStore_ListsOldestFirst()
    {
        var store = new InMemoryDeadLetterStore();
        var first = new DeadLetter(Guid.NewGuid(), "first", 1, DateTimeOffset.UtcNow);
        var second = new DeadLetter(Guid.NewGuid(), "second", 4, DateTimeOffset.UtcNow);

        await store.AddAsync(first);
        await store.AddAsync(second);

        Assert.Equal([first, second], await store.ListAsync());
    }

    [Fact]
    public async Task RetryPolicy_ZeroRetries_RunsOnce()
    {
        var policy = new PollyExtractionRetryPolicy(new() { MaxRetryAttempts = 0, BaseDelay = TimeSpan.Zero });
        var attempts = 0;

        await Assert.ThrowsAsync<TransientExtractionException>(() => policy.ExecuteAsync(_ =>
        {
            attempts++;
            throw new TransientExtractionException("down");
        }, CancellationToken.None));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task RetryPolicy_ReturnsResult_OfFirstSuccessfulAttempt()
    {
        var policy = new PollyExtractionRetryPolicy(new() { MaxRetryAttempts = 3, BaseDelay = TimeSpan.Zero });
        var attempts = 0;

        var result = await policy.ExecuteAsync(_ =>
        {
            attempts++;
            return attempts < 3 ? throw new TransientExtractionException("down") : Task.FromResult(AnyResult);
        }, CancellationToken.None);

        Assert.Same(AnyResult, result);
        Assert.Equal(3, attempts);
    }
}
