namespace DocIntake.Core;

/// <summary>Reads structured fields out of a document's content (e.g. OCR or an AI model).</summary>
public interface IExtractor
{
    Task<ExtractionResult> ExtractAsync(Document doc, Stream content);
}
