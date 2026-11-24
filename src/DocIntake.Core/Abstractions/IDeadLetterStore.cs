using DocIntake.Core.Domain;

namespace DocIntake.Core.Abstractions;

/// <summary>Keeps documents that failed processing for good.</summary>
public interface IDeadLetterStore
{
    Task AddAsync(DeadLetter deadLetter, CancellationToken cancellationToken = default);

    /// <summary>Returns all dead letters, oldest first.</summary>
    Task<IReadOnlyList<DeadLetter>> ListAsync(CancellationToken cancellationToken = default);
}
