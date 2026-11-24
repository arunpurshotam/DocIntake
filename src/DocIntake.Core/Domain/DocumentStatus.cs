namespace DocIntake.Core.Domain;

/// <summary>Where a document is in the intake pipeline.</summary>
public enum DocumentStatus
{
    Received,
    Extracted,
    NeedsReview,
    Approved,
    Failed
}
