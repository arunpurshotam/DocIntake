using DocIntake.Core.Abstractions;
using DocIntake.Core.Domain;
using DocIntake.Infrastructure.Queue;
using DocIntake.Infrastructure.Storage;

namespace DocIntake.Tests.TestDoubles;

/// <summary>Wraps another extractor and counts how many times it was called.</summary>
public sealed class CountingExtractor(IExtractor inner) : IExtractor
{
    private int _calls;
    public int Calls => Volatile.Read(ref _calls);

    public Task<ExtractionResult> ExtractAsync(Document doc, Stream content)
    {
        Interlocked.Increment(ref _calls);
        return inner.ExtractAsync(doc, content);
    }
}

/// <summary>Fails with a transient error on every call, so retries are always exhausted.</summary>
public sealed class AlwaysTransientExtractor : IExtractor
{
    public Task<ExtractionResult> ExtractAsync(Document doc, Stream content) =>
        throw new TransientExtractionException("Service temporarily unavailable.");
}

/// <summary>Returns a fixed result.</summary>
public sealed class StubExtractor(ExtractionResult result) : IExtractor
{
    public Task<ExtractionResult> ExtractAsync(Document doc, Stream content) => Task.FromResult(result);
}

/// <summary>A real channel queue that also records every enqueued id.</summary>
public sealed class RecordingJobQueue : IJobQueue
{
    private readonly ChannelJobQueue _inner = new();
    private readonly List<Guid> _enqueued = [];

    public IReadOnlyList<Guid> Enqueued
    {
        get { lock (_enqueued) { return _enqueued.ToList(); } }
    }

    public Task EnqueueAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        lock (_enqueued) { _enqueued.Add(documentId); }
        return _inner.EnqueueAsync(documentId, cancellationToken);
    }

    public Task<Guid> DequeueAsync(CancellationToken cancellationToken) => _inner.DequeueAsync(cancellationToken);
}

/// <summary>
/// A real dead-letter store that completes <see cref="FirstAdded"/> when something is added,
/// so a test can await the background worker instead of sleeping.
/// </summary>
public sealed class SignalingDeadLetterStore : IDeadLetterStore
{
    private readonly InMemoryDeadLetterStore _inner = new();

    public TaskCompletionSource<DeadLetter> FirstAdded { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task AddAsync(DeadLetter deadLetter, CancellationToken cancellationToken = default)
    {
        await _inner.AddAsync(deadLetter, cancellationToken);
        FirstAdded.TrySetResult(deadLetter);
    }

    public Task<IReadOnlyList<DeadLetter>> ListAsync(CancellationToken cancellationToken = default) =>
        _inner.ListAsync(cancellationToken);
}
