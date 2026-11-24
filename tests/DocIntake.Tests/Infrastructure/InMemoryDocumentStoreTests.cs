using DocIntake.Core.Domain;
using DocIntake.Infrastructure.Storage;

namespace DocIntake.Tests.Infrastructure;

public class InMemoryDocumentStoreTests
{
    [Fact]
    public async Task SaveAsync_ThenGetAsync_ReturnsSameDocument()
    {
        var store = new InMemoryDocumentStore();
        var document = new Document("invoice-001.pdf");

        await store.SaveAsync(document);
        var loaded = await store.GetAsync(document.Id);

        Assert.NotNull(loaded);
        Assert.Equal(document.Id, loaded.Id);
        Assert.Equal("invoice-001.pdf", loaded.FileName);
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        var store = new InMemoryDocumentStore();

        var loaded = await store.GetAsync(Guid.NewGuid());

        Assert.Null(loaded);
    }
}
