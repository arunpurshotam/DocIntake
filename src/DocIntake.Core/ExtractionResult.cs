namespace DocIntake.Core;

/// <summary>The fields an extractor read from a document, and how sure it is about them.</summary>
public class ExtractionResult
{
    public ExtractionResult(IReadOnlyDictionary<string, string> fields, double confidence)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (double.IsNaN(confidence) || confidence < 0.0 || confidence > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "Confidence must be between 0.0 and 1.0.");
        }

        Fields = fields;
        Confidence = confidence;
    }

    /// <summary>Extracted values keyed by field name, e.g. "Wages" -> "52000.00".</summary>
    public IReadOnlyDictionary<string, string> Fields { get; }

    /// <summary>Overall extraction confidence from 0.0 to 1.0.</summary>
    public double Confidence { get; }
}
