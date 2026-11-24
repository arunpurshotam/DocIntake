using System.Collections.Concurrent;
using DocIntake.Core.Abstractions;

namespace DocIntake.Infrastructure.Storage;

/// <summary>Keeps uploaded bytes in memory. Data is lost on restart; blob storage would replace this.</summary>
public class InMemoryDocumentContentStore : IDocumentContentStore
{
    private readonly ConcurrentDictionary<Guid, byte[]> _content = new();

    public Task SaveAsync(Guid documentId, byte[] content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        _content[documentId] = content;
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        Stream? stream = _content.TryGetValue(documentId, out var bytes)
            ? new MemoryStream(bytes, writable: false)
            : null;
        return Task.FromResult(stream);
    }
}
