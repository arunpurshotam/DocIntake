using DocIntake.Core.Abstractions;
using DocIntake.Core.Domain;

namespace DocIntake.Infrastructure.Extraction;

/// <summary>
/// Stand-in extractor that returns fixed, synthetic wage-statement fields.
/// The file name picks the scenario, so each pipeline path can be tried by hand:
///   contains "broken" -> throws (document ends up Failed)
///   contains "blurry" -> confidence 0.60 (NeedsReview)
///   contains "good"   -> confidence 0.95 (Approved)
///   anything else     -> confidence 0.80 (NeedsReview; unknown quality goes to a human)
/// Matching ignores case. The content stream is not read.
/// </summary>
public class FakeExtractor : IExtractor
{
    public const double GoodConfidence = 0.95;
    public const double BlurryConfidence = 0.60;
    public const double DefaultConfidence = 0.80;

    public Task<ExtractionResult> ExtractAsync(Document doc, Stream content)
    {
        ArgumentNullException.ThrowIfNull(doc);

        if (NameContains(doc, "broken"))
        {
            throw new InvalidOperationException($"Simulated extraction failure for '{doc.FileName}'.");
        }

        var confidence = NameContains(doc, "blurry") ? BlurryConfidence
            : NameContains(doc, "good") ? GoodConfidence
            : DefaultConfidence;

        // Synthetic sample values only; no real personal data.
        var fields = new Dictionary<string, string>
        {
            ["EmployeeName"] = "Jane Sample",
            ["EmployerId"] = "00-0000000",
            ["Wages"] = "52000.00",
            ["Withholding"] = "7800.00",
        };

        return Task.FromResult(new ExtractionResult(fields, confidence));
    }

    private static bool NameContains(Document doc, string keyword) =>
        doc.FileName.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}
