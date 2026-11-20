namespace DocIntake.Core;

/// <summary>Persists documents. Async so a real database can replace the in-memory version later.</summary>
public interface IDocumentStore
{
    /// <summary>Adds the document, or replaces it if one with the same Id already exists.</summary>
    Task SaveAsync(Document document, CancellationToken cancellationToken = default);

    /// <summary>Returns the document, or null if no document has that Id.</summary>
    Task<Document?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}
