using System.Text;
using DocIntake.Api.Controllers;
using DocIntake.Core;
using DocIntake.Core.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DocIntake.Tests;

public class ReviewQueueTests
{
    private readonly InMemoryDocumentStore _store = new();

    private DocumentsController CreateDocumentsController() =>
        new(_store, new DocumentProcessor(
            new FakeExtractor(),
            new Validator([new RequiredFieldsRule(), new WithholdingRule()]),
            _store));

    private static IFormFile FakeUpload(string fileName, string text = "synthetic content")
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName);
    }

    [Fact]
    public async Task ReviewQueue_ListsOnlyNeedsReviewDocuments_WithReasons()
    {
        var documents = CreateDocumentsController();
        await documents.Upload(FakeUpload("w2-good.pdf"), CancellationToken.None);
        await documents.Upload(FakeUpload("w2-blurry.pdf"), CancellationToken.None);
        await documents.Upload(FakeUpload("w2-broken.pdf"), CancellationToken.None);

        var queue = await new ReviewQueueController(_store).Get(CancellationToken.None);

        var item = Assert.Single(queue);
        Assert.Equal("w2-blurry.pdf", item.FileName);
        Assert.Equal(DocumentStatus.NeedsReview, item.Status);
        Assert.NotEmpty(item.ReviewReasons);
    }

    [Fact]
    public async Task ReviewQueue_IsOldestFirst()
    {
        var first = new Document("first.pdf");
        first.MarkExtracted(0.50);
        await Task.Delay(5); // ensure distinct CreatedAt
        var second = new Document("second.pdf");
        second.MarkExtracted(0.50);
        await _store.SaveAsync(second);
        await _store.SaveAsync(first);

        var queue = await new ReviewQueueController(_store).Get(CancellationToken.None);

        Assert.Equal(["first.pdf", "second.pdf"], queue.Select(d => d.FileName));
    }

    [Fact]
    public async Task ReviewQueue_Empty_ReturnsEmptyList()
    {
        var queue = await new ReviewQueueController(_store).Get(CancellationToken.None);

        Assert.Empty(queue);
    }

    [Theory]
    [InlineData(false)] // no file attached
    [InlineData(true)]  // empty file attached
    public async Task Upload_WithoutFileContent_Returns400(bool attachEmptyFile)
    {
        var file = attachEmptyFile ? FakeUpload("empty.pdf", "") : null;

        var result = await CreateDocumentsController().Upload(file, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Empty(await _store.ListByStatusAsync(DocumentStatus.Received));
    }
}
