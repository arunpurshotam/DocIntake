using DocIntake.Core.Abstractions;
using DocIntake.Core.Domain;
using DocIntake.Core.Services;
using DocIntake.Core.Validation;
using DocIntake.Infrastructure.Extraction;
using DocIntake.Infrastructure.Resilience;
using DocIntake.Infrastructure.Storage;
using DocIntake.Tests.TestDoubles;

namespace DocIntake.Tests.Services;

public class DocumentProcessorTests
{
    // Production retry count (3), but no waiting between attempts.
    private static readonly ExtractionRetryOptions NoDelay = new() { MaxRetryAttempts = 3, BaseDelay = TimeSpan.Zero };

    private readonly InMemoryDocumentStore _store = new();
    private readonly InMemoryDocumentContentStore _content = new();
    private readonly InMemoryDeadLetterStore _deadLetters = new();

    private DocumentProcessor CreateProcessor(IExtractor extractor) => new(
        extractor,
        new Validator([new RequiredFieldsRule(), new WithholdingRule()]),
        _store,
        _content,
        _deadLetters,
        new PollyExtractionRetryPolicy(NoDelay));

    // Stores the document and its content the way the upload endpoint does, then processes it.
    private async Task<Document> Process(string fileName, IExtractor? extractor = null, bool storeContent = true)
    {
        var document = new Document(fileName);
        if (storeContent)
        {
            await _content.SaveAsync(document.Id, [1, 2, 3]);
        }
        await _store.SaveAsync(document);

        await CreateProcessor(extractor ?? new FakeExtractor()).ProcessAsync(document);
        return document;
    }

    // --- Decisions (unchanged from Milestone 2) ---

    [Fact]
    public async Task GoodFile_IsApproved_AndSaved()
    {
        var document = await Process("w2-good.pdf");

        Assert.Equal(DocumentStatus.Approved, document.Status);
        Assert.Equal(0.95, document.Confidence);
        Assert.Empty(document.ReviewReasons);
        Assert.Same(document, await _store.GetAsync(document.Id));
        Assert.Empty(await _deadLetters.ListAsync());
    }

    [Fact]
    public async Task BlurryFile_NeedsReview_WithConfidenceReason()
    {
        var document = await Process("w2-blurry.pdf");

        Assert.Equal(DocumentStatus.NeedsReview, document.Status);
        Assert.Contains("below the 0.90 threshold", Assert.Single(document.ReviewReasons));
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

    // --- Processing status ---

    [Fact]
    public async Task StatusIsProcessing_WhileExtracting()
    {
        DocumentStatus? statusSeenByExtractor = null;
        DocumentStatus? statusSavedInStore = null;
        var extractor = new CallbackExtractor(async doc =>
        {
            statusSeenByExtractor = doc.Status;
            statusSavedInStore = (await _store.GetAsync(doc.Id))?.Status;
        });

        var document = await Process("w2-good.pdf", extractor);

        Assert.Equal(DocumentStatus.Processing, statusSeenByExtractor);
        Assert.Equal(DocumentStatus.Processing, statusSavedInStore);
        Assert.Equal(DocumentStatus.Approved, document.Status);
    }

    // --- Retries and dead letters ---

    [Fact]
    public async Task Flaky_IsRetried_ThenSucceeds()
    {
        var extractor = new CountingExtractor(new FakeExtractor());

        var document = await Process("w2-flaky-good.pdf", extractor);

        Assert.Equal(DocumentStatus.Approved, document.Status);
        Assert.Equal(FakeExtractor.FlakyFailuresBeforeSuccess + 1, extractor.Calls); // 2 failures + 1 success
        Assert.Empty(await _deadLetters.ListAsync());
    }

    [Fact]
    public async Task TransientEveryTime_RetriesExhausted_GoesToDeadLetter()
    {
        var extractor = new CountingExtractor(new AlwaysTransientExtractor());

        var document = await Process("w2.pdf", extractor);

        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Equal(4, extractor.Calls); // 1 attempt + 3 retries
        var deadLetter = Assert.Single(await _deadLetters.ListAsync());
        Assert.Equal(document.Id, deadLetter.DocumentId);
        Assert.Equal(4, deadLetter.Attempts);
        Assert.StartsWith(nameof(TransientExtractionException), deadLetter.Error);
    }

    [Fact]
    public async Task PermanentError_IsNotRetried_AndGoesToDeadLetter()
    {
        var extractor = new CountingExtractor(new FakeExtractor());

        var document = await Process("w2-broken.pdf", extractor);

        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Null(document.Confidence);
        Assert.Equal(1, extractor.Calls);
        var deadLetter = Assert.Single(await _deadLetters.ListAsync());
        Assert.Equal(1, deadLetter.Attempts);
        Assert.StartsWith(nameof(InvalidOperationException), deadLetter.Error);
    }

    [Fact]
    public async Task MissingContent_IsAPermanentFailure()
    {
        var extractor = new CountingExtractor(new FakeExtractor());

        var document = await Process("w2-good.pdf", extractor, storeContent: false);

        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Equal(0, extractor.Calls);
        Assert.Contains("No stored content", Assert.Single(await _deadLetters.ListAsync()).Error);
    }

    [Fact]
    public async Task ShutdownDuringProcessing_IsNotDeadLettered()
    {
        using var cts = new CancellationTokenSource();
        var extractor = new CallbackExtractor(_ =>
        {
            cts.Cancel(); // simulate the host stopping mid-extraction
            cts.Token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        });
        var document = new Document("w2.pdf");
        await _content.SaveAsync(document.Id, [1]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateProcessor(extractor).ProcessAsync(document, cts.Token));

        Assert.Empty(await _deadLetters.ListAsync());
    }

    /// <summary>Runs a callback, then returns a high-confidence valid result.</summary>
    private sealed class CallbackExtractor(Func<Document, Task> onExtract) : IExtractor
    {
        public async Task<ExtractionResult> ExtractAsync(Document doc, Stream content)
        {
            await onExtract(doc);
            return await new FakeExtractor().ExtractAsync(doc, content);
        }
    }
}
