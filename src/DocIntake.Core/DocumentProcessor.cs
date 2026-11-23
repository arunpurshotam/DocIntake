using DocIntake.Core.Validation;

namespace DocIntake.Core;

/// <summary>
/// Runs one document through the pipeline: extract -> validate -> decide -> save.
/// </summary>
public class DocumentProcessor(IExtractor extractor, Validator validator, IDocumentStore store)
{
    public async Task ProcessAsync(Document document, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(content);

        ExtractionResult result;
        try
        {
            result = await extractor.ExtractAsync(document, content);
        }
        catch (Exception)
        {
            // Any extractor error is a processing failure for this document, not a crash for the caller.
            document.MarkFailed();
            await store.SaveAsync(document, cancellationToken);
            return;
        }

        var issues = validator.Validate(result);
        document.MarkExtracted(result.Confidence, issues);
        await store.SaveAsync(document, cancellationToken);
    }
}
