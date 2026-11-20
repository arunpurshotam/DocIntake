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

    /// <summary>
    /// Records a successful extraction. High-confidence results are approved;
    /// everything else is routed to a human for review.
    /// </summary>
    public void MarkExtracted(double confidence)
    {
        if (double.IsNaN(confidence) || confidence < 0.0 || confidence > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "Confidence must be between 0.0 and 1.0.");
        }

        Confidence = confidence;
        Status = confidence >= AutoApproveThreshold
            ? DocumentStatus.Approved
            : DocumentStatus.NeedsReview;
    }

    /// <summary>Records that processing failed for this document.</summary>
    public void MarkFailed()
    {
        Status = DocumentStatus.Failed;
    }
}
