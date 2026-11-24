using DocIntake.Core.Domain;

namespace DocIntake.Tests.Domain;

public class DocumentTests
{
    [Fact]
    public void NewDocument_StartsAsReceived_WithNoConfidence()
    {
        var document = new Document("invoice-001.pdf");

        Assert.Equal(DocumentStatus.Received, document.Status);
        Assert.Null(document.Confidence);
        Assert.NotEqual(Guid.Empty, document.Id);
    }

    [Fact]
    public void MarkExtracted_HighConfidence_IsApproved()
    {
        var document = new Document("invoice-001.pdf");

        document.MarkExtracted(0.95);

        Assert.Equal(DocumentStatus.Approved, document.Status);
        Assert.Equal(0.95, document.Confidence);
    }

    [Fact]
    public void MarkExtracted_LowConfidence_NeedsReview()
    {
        var document = new Document("invoice-001.pdf");

        document.MarkExtracted(0.60);

        Assert.Equal(DocumentStatus.NeedsReview, document.Status);
        Assert.Equal(0.60, document.Confidence);
    }

    [Fact]
    public void MarkExtracted_ExactlyAtThreshold_IsApproved()
    {
        var document = new Document("invoice-001.pdf");

        document.MarkExtracted(0.90);

        Assert.Equal(DocumentStatus.Approved, document.Status);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void MarkExtracted_OutOfRange_Throws(double confidence)
    {
        var document = new Document("invoice-001.pdf");

        Assert.Throws<ArgumentOutOfRangeException>(() => document.MarkExtracted(confidence));
    }

    [Fact]
    public void NewDocument_KeepsContentHash()
    {
        var document = new Document("invoice-001.pdf", contentHash: "abc123");

        Assert.Equal("abc123", document.ContentHash);
    }

    [Fact]
    public void MarkProcessing_SetsStatus_AndClearsOldReasons()
    {
        var document = new Document("invoice-001.pdf");
        document.MarkExtracted(0.50); // NeedsReview with a reason

        document.MarkProcessing();

        Assert.Equal(DocumentStatus.Processing, document.Status);
        Assert.Empty(document.ReviewReasons);
    }

    [Fact]
    public void MarkFailed_SetsStatusToFailed()
    {
        var document = new Document("invoice-001.pdf");

        document.MarkFailed();

        Assert.Equal(DocumentStatus.Failed, document.Status);
    }
}
