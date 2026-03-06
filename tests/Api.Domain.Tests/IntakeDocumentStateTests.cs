using Api.Domain.Aggregates;
using Api.Domain.Aggregates.Events;
using SharedKernel;

namespace Api.Domain.Tests;

public sealed class IntakeDocumentStateTests
{
    private readonly TenantId _tenantId = TenantId.New();

    // --- Submit validation ---

    [Fact]
    public void Submit_with_null_originalFileName_throws_ArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            IntakeDocument.Submit(_tenantId, null!, "storage/key"));
    }

    [Fact]
    public void Submit_with_whitespace_storageKey_throws_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            IntakeDocument.Submit(_tenantId, "test.pdf", "   "));
    }

    [Fact]
    public void Submit_raises_DocumentSubmittedEvent()
    {
        var doc = IntakeDocument.Submit(_tenantId, "test.pdf", "storage/key");

        Assert.Contains(doc.DomainEvents, e => e is DocumentSubmittedEvent);
    }

    // --- MarkProcessing ---

    [Fact]
    public void MarkProcessing_from_Submitted_succeeds_and_raises_event()
    {
        var doc = IntakeDocument.Submit(_tenantId, "test.pdf", "storage/key");

        var result = doc.MarkProcessing();

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.Processing, doc.Status);
        Assert.Contains(doc.DomainEvents, e => e is DocumentProcessingStartedEvent);
    }

    [Fact]
    public void MarkProcessing_from_Processing_fails_with_INVALID_TRANSITION()
    {
        var doc = IntakeDocument.Submit(_tenantId, "test.pdf", "storage/key");
        doc.MarkProcessing();

        var result = doc.MarkProcessing();

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_TRANSITION", result.Error.Code);
    }

    // --- MarkFailed ---

    [Fact]
    public void MarkFailed_from_Submitted_succeeds_and_raises_event()
    {
        var doc = IntakeDocument.Submit(_tenantId, "test.pdf", "storage/key");

        var result = doc.MarkFailed("OCR timeout");

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.Failed, doc.Status);
        Assert.Contains(doc.DomainEvents, e => e is DocumentFailedEvent);
    }

    [Fact]
    public void MarkFailed_from_Processing_succeeds()
    {
        var doc = IntakeDocument.Submit(_tenantId, "test.pdf", "storage/key");
        doc.MarkProcessing();

        var result = doc.MarkFailed("Worker crashed");

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.Failed, doc.Status);
    }

    [Fact]
    public void MarkFailed_from_Completed_fails_with_INVALID_TRANSITION()
    {
        var doc = IntakeDocument.Submit(_tenantId, "test.pdf", "storage/key");
        doc.MarkProcessing();
        doc.MarkCompleted();

        var result = doc.MarkFailed("Should not work");

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_TRANSITION", result.Error.Code);
    }

    // --- AssignToCase ---

    [Fact]
    public void AssignToCase_sets_CaseId()
    {
        var doc = IntakeDocument.Submit(_tenantId, "test.pdf", "storage/key");
        var caseId = CaseId.New();

        doc.AssignToCase(caseId);

        Assert.Equal(caseId, doc.CaseId);
    }

    [Fact]
    public void AssignToCase_with_null_throws_ArgumentNullException()
    {
        var doc = IntakeDocument.Submit(_tenantId, "test.pdf", "storage/key");

        Assert.Throws<ArgumentNullException>(() => doc.AssignToCase(null!));
    }
}
