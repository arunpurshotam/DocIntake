using System.Text;
using DocIntake.Api.Controllers;
using DocIntake.Core.Domain;
using DocIntake.Core.Services;
using DocIntake.Core.Validation;
using DocIntake.Infrastructure.Extraction;
using DocIntake.Infrastructure.Queue;
using DocIntake.Infrastructure.Resilience;
using DocIntake.Infrastructure.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DocIntake.Tests.Api;

public class ReviewQueueTests
{
    private readonly InMemoryDocumentStore _store = new();
    private readonly InMemoryDocumentContentStore _content = new();

    // Runs a document through the real pipeline (fake extractor, real rules, no retry delays).
    private async Task ProcessAsync(string fileName)
    {
        var processor = new DocumentProcessor(
            new FakeExtractor(),
            new Validator([new RequiredFieldsRule(), new WithholdingRule()]),
            _store,
            _content,
            new InMemoryDeadLetterStore(),
            new PollyExtractionRetryPolicy(new ExtractionRetryOptions { BaseDelay = TimeSpan.Zero }));

        var document = new Document(fileName);
        await _content.SaveAsync(document.Id, Encoding.UTF8.GetBytes("synthetic content"));
        await _store.SaveAsync(document);
        await processor.ProcessAsync(document);
    }

    private static IFormFile FakeUpload(string fileName, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName);
    }

    [Fact]
    public async Task ReviewQueue_ListsOnlyNeedsReviewDocuments_WithReasons()
    {
        await ProcessAsync("w2-good.pdf");
        await ProcessAsync("w2-blurry.pdf");
        await ProcessAsync("w2-broken.pdf");

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
        var controller = new DocumentsController(_store, _content, new ChannelJobQueue());

        var result = await controller.Upload(file, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Empty(await _store.ListByStatusAsync(DocumentStatus.Received));
    }
}
