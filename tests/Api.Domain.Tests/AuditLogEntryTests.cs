using Api.Domain.Aggregates;
using SharedKernel;

namespace Api.Domain.Tests;

public sealed class AuditLogEntryTests
{
    private readonly TenantId _tenantId = TenantId.New();
    private readonly DocumentId _documentId = DocumentId.New();
    private readonly UserId _reviewerId = UserId.New();

    [Fact]
    public void RecordExtractionCompleted_creates_entry_with_correct_action()
    {
        var entry = AuditLogEntry.RecordExtractionCompleted(_tenantId, _documentId);

        Assert.Equal(AuditAction.ExtractionCompleted, entry.Action);
        Assert.Equal(_tenantId, entry.TenantId);
        Assert.Equal(_documentId, entry.DocumentId);
        Assert.Null(entry.PerformedBy);
        Assert.Null(entry.FieldName);
        Assert.Null(entry.PreviousValue);
        Assert.Null(entry.NewValue);
        Assert.True(entry.Timestamp <= DateTimeOffset.UtcNow);
        Assert.True(entry.Timestamp > DateTimeOffset.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public void RecordReviewStarted_creates_entry_with_reviewer_id()
    {
        var entry = AuditLogEntry.RecordReviewStarted(_tenantId, _documentId, _reviewerId);

        Assert.Equal(AuditAction.ReviewStarted, entry.Action);
        Assert.Equal(_reviewerId, entry.PerformedBy);
        Assert.Null(entry.FieldName);
    }

    [Fact]
    public void RecordFieldCorrected_creates_entry_with_all_fields()
    {
        var entry = AuditLogEntry.RecordFieldCorrected(
            _tenantId, _documentId, _reviewerId, "PatientName", "John Doe", "Jane Doe");

        Assert.Equal(AuditAction.FieldCorrected, entry.Action);
        Assert.Equal(_reviewerId, entry.PerformedBy);
        Assert.Equal("PatientName", entry.FieldName);
        Assert.Equal("John Doe", entry.PreviousValue);
        Assert.Equal("Jane Doe", entry.NewValue);
    }

    [Fact]
    public void RecordReviewFinalized_creates_entry_with_reviewer_id()
    {
        var entry = AuditLogEntry.RecordReviewFinalized(_tenantId, _documentId, _reviewerId);

        Assert.Equal(AuditAction.ReviewFinalized, entry.Action);
        Assert.Equal(_reviewerId, entry.PerformedBy);
        Assert.Null(entry.FieldName);
        Assert.Null(entry.PreviousValue);
        Assert.Null(entry.NewValue);
    }

    [Fact]
    public void Each_entry_has_unique_id()
    {
        var entry1 = AuditLogEntry.RecordExtractionCompleted(_tenantId, _documentId);
        var entry2 = AuditLogEntry.RecordExtractionCompleted(_tenantId, _documentId);

        Assert.NotEqual(entry1.Id, entry2.Id);
    }
}
