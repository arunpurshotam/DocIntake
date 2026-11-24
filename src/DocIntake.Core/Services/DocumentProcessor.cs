using DocIntake.Core.Abstractions;
using DocIntake.Core.Domain;
using DocIntake.Core.Validation;

namespace DocIntake.Core.Services;

/// <summary>
/// Runs one document through the pipeline:
/// mark Processing -> extract (with retries) -> validate -> decide -> save.
/// If extraction finally fails, the document is marked Failed and a dead letter is recorded.
/// </summary>
public class DocumentProcessor(
    IExtractor extractor,
    Validator validator,
    IDocumentStore store,
    IDocumentContentStore contentStore,
    IDeadLetterStore deadLetters,
    IExtractionRetryPolicy retryPolicy)
{
    public async Task ProcessAsync(Document document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.MarkProcessing();
        await store.SaveAsync(document, cancellationToken);

        var attempts = 0;
        ExtractionResult result;
        try
        {
            result = await retryPolicy.ExecuteAsync(async token =>
            {
                attempts++;
                // A fresh stream per attempt, so a retry never sees a half-read stream.
                await using var content = await contentStore.OpenReadAsync(document.Id, token)
                    ?? throw new InvalidOperationException($"No stored content for document {document.Id}.");
                return await extractor.ExtractAsync(document, content);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // Shutting down: not the document's fault, so don't dead-letter it.
        }
        catch (Exception ex)
        {
            // Retries exhausted, or a permanent error: give up on this document.
            document.MarkFailed();
            await store.SaveAsync(document, cancellationToken);
            await deadLetters.AddAsync(
                new DeadLetter(document.Id, $"{ex.GetType().Name}: {ex.Message}", attempts, DateTimeOffset.UtcNow),
                cancellationToken);
            return;
        }

        var issues = validator.Validate(result);
        document.MarkExtracted(result.Confidence, issues);
        await store.SaveAsync(document, cancellationToken);
    }
}
