using DocIntake.Core.Abstractions;
using DocIntake.Core.Domain;
using DocIntake.Core.Services;
using DocIntake.Core.Validation;
using DocIntake.Infrastructure.Extraction;
using DocIntake.Infrastructure.Storage;

namespace DocIntake.Tests.Services;

public class DocumentProcessorTests
{
    private readonly InMemoryDocumentStore _store = new();

    private DocumentProcessor CreateProcessor(IExtractor extractor) =>
        new(extractor, new Validator([new RequiredFieldsRule(), new WithholdingRule()]), _store);

    private async Task<Document> Process(string fileName, IExtractor? extractor = null)
    {
        var document = new Document(fileName);
        using var content = new MemoryStream([1, 2, 3]);
        await CreateProcessor(extractor ?? new FakeExtractor()).ProcessAsync(document, content);
        return document;
    }

    [Fact]
    public async Task GoodFile_IsApproved_AndSaved()
    {
        var document = await Process("w2-good.pdf");

        Assert.Equal(DocumentStatus.Approved, document.Status);
        Assert.Equal(0.95, document.Confidence);
        Assert.Empty(document.ReviewReasons);
        Assert.Same(document, await _store.GetAsync(document.Id));
    }

    [Fact]
    public async Task BlurryFile_NeedsReview_WithConfidenceReason()
    {
        var document = await Process("w2-blurry.pdf");

        Assert.Equal(DocumentStatus.NeedsReview, document.Status);
        Assert.Contains("below the 0.90 threshold", Assert.Single(document.ReviewReasons));
        Assert.Same(document, await _store.GetAsync(document.Id));
    }

    [Fact]
    public async Task BrokenFile_ExtractorThrows_IsFailed_AndSaved()
    {
        var document = await Process("w2-broken.pdf");

        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Null(document.Confidence);
        Assert.Same(document, await _store.GetAsync(document.Id));
    }

    [Fact]
    public async Task HighConfidence_WithValidationIssue_NeedsReview()
    {
        var extractor = new StubExtractor(new ExtractionResult(
            new Dictionary<string, string>
            {
                ["EmployeeName"] = "Jane Sample",
                ["EmployerId"] = "00-0000000",
                ["Wages"] = "1000.00",
                ["Withholding"] = "2000.00",
            },
            confidence: 0.99));

        var document = await Process("w2-good.pdf", extractor);

        Assert.Equal(DocumentStatus.NeedsReview, document.Status);
        Assert.Contains("greater than Wages", Assert.Single(document.ReviewReasons));
    }

    [Fact]
    public async Task LowConfidence_AndValidationIssue_ListsBothReasons()
    {
        var extractor = new StubExtractor(new ExtractionResult(
            new Dictionary<string, string> { ["EmployerId"] = "00-0000000", ["Wages"] = "1000.00" },
            confidence: 0.50));

        var document = await Process("w2.pdf", extractor);

        Assert.Equal(DocumentStatus.NeedsReview, document.Status);
        Assert.Equal(2, document.ReviewReasons.Count);
    }

    private sealed class StubExtractor(ExtractionResult result) : IExtractor
    {
        public Task<ExtractionResult> ExtractAsync(Document doc, Stream content) => Task.FromResult(result);
    }
}
