using System.Collections.Concurrent;

namespace DocIntake.Core;

/// <summary>Thread-safe, process-local store. Data is lost on restart.</summary>
public class InMemoryDocumentStore : IDocumentStore
{
    private readonly ConcurrentDictionary<Guid, Document> _documents = new();

    public Task SaveAsync(Document document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        _documents[document.Id] = document;
        return Task.CompletedTask;
    }

    public Task<Document?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _documents.TryGetValue(id, out var document);
        return Task.FromResult(document);
    }
}
