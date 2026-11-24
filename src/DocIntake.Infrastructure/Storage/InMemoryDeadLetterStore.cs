using System.Collections.Concurrent;
using DocIntake.Core.Abstractions;
using DocIntake.Core.Domain;

namespace DocIntake.Infrastructure.Storage;

/// <summary>Thread-safe, process-local dead-letter list. Data is lost on restart.</summary>
public class InMemoryDeadLetterStore : IDeadLetterStore
{
    private readonly ConcurrentQueue<DeadLetter> _deadLetters = new();

    public Task AddAsync(DeadLetter deadLetter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deadLetter);

        _deadLetters.Enqueue(deadLetter);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DeadLetter>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DeadLetter> all = _deadLetters.ToList(); // insertion order = oldest first
        return Task.FromResult(all);
    }
}
