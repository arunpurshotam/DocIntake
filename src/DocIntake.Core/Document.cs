namespace DocIntake.Core;

/// <summary>A document submitted for intake, plus its processing outcome.</summary>
public class Document
{
    /// <summary>Extraction confidence at or above this value is auto-approved.</summary>
    public const double AutoApproveThreshold = 0.90;

    public Document(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        Id = Guid.NewGuid();
        FileName = fileName;
        Status = DocumentStatus.Received;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }
    public string FileName { get; }
    public DocumentStatus Status { get; private set; }

    /// <summary>Extraction confidence from 0.0 to 1.0; null until extraction has run.</summary>
    public double? Confidence { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    private readonly List<string> _reviewReasons = [];

    /// <summary>Why a human needs to look at this document; empty unless Status is NeedsReview.</summary>
    public IReadOnlyList<string> ReviewReasons => _reviewReasons;

    /// <summary>Records a successful extraction that had no validation issues.</summary>
    public void MarkExtracted(double confidence) => MarkExtracted(confidence, []);

    /// <summary>
    /// Records a successful extraction. The document is Approved only when confidence
    /// is at least <see cref="AutoApproveThreshold"/> and there are no validation issues;
    /// otherwise it goes to NeedsReview with a reason for each problem.
    /// </summary>
    public void MarkExtracted(double confidence, IEnumerable<string> validationIssues)
    {
        if (double.IsNaN(confidence) || confidence < 0.0 || confidence > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "Confidence must be between 0.0 and 1.0.");
        }
        ArgumentNullException.ThrowIfNull(validationIssues);

        Confidence = confidence;
        _reviewReasons.Clear();

        if (confidence < AutoApproveThreshold)
        {
            _reviewReasons.Add(FormattableString.Invariant(
                $"Extraction confidence {confidence:0.00} is below the {AutoApproveThreshold:0.00} threshold."));
        }
        _reviewReasons.AddRange(validationIssues);

        Status = _reviewReasons.Count == 0
            ? DocumentStatus.Approved
            : DocumentStatus.NeedsReview;
    }

    /// <summary>Records that processing failed for this document.</summary>
    public void MarkFailed()
    {
        _reviewReasons.Clear();
        Status = DocumentStatus.Failed;
    }
}
