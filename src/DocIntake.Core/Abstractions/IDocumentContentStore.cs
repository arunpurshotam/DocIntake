namespace DocIntake.Core.Abstractions;

/// <summary>
/// Keeps the uploaded bytes so the background worker can read them later.
/// (The job queue only carries the document id.)
/// </summary>
public interface IDocumentContentStore
{
    Task SaveAsync(Guid documentId, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>Opens a fresh read-only stream over the content, or returns null if none was stored.</summary>
    Task<Stream?> OpenReadAsync(Guid documentId, CancellationToken cancellationToken = default);
}
