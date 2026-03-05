using Api.Domain.Aggregates;
using SharedKernel;

namespace Api.Domain.Tests;

public sealed class IntakeDocumentReviewTests
{
    private readonly TenantId _tenantId = TenantId.New();

    private static IntakeDocument CreateCompletedDocument(TenantId tenantId)
    {
        var doc = IntakeDocument.Submit(tenantId, "test.pdf", "storage/key");
        doc.MarkProcessing();
        doc.MarkCompleted([new ExtractedField("PatientName", "John Doe", 0.95)]);
        return doc;
    }

    // --- MarkCompleted with fields ---

    [Fact]
    public void MarkCompleted_with_extracted_fields_stores_them()
    {
        var doc = IntakeDocument.Submit(_tenantId, "test.pdf", "storage/key");
        doc.MarkProcessing();

        var fields = new List<ExtractedField>
        {
            new("PatientName", "John Doe", 0.95),
            new("DateOfBirth", "1990-01-01", 0.88)
        };

        var result = doc.MarkCompleted(fields);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.Completed, doc.Status);
        Assert.Equal(2, doc.ExtractedFields.Count);
        Assert.Equal("PatientName", doc.ExtractedFields[0].Name);
        Assert.Equal("John Doe", doc.ExtractedFields[0].Value);
        Assert.Equal(0.95, doc.ExtractedFields[0].Confidence);
    }

    [Fact]
    public void MarkCompleted_without_fields_succeeds_with_empty_collection()
    {
        var doc = IntakeDocument.Submit(_tenantId, "test.pdf", "storage/key");
        doc.MarkProcessing();

        var result = doc.MarkCompleted();

        Assert.True(result.IsSuccess);
        Assert.Empty(doc.ExtractedFields);
    }

    // --- MarkPendingReview ---

    [Fact]
    public void MarkPendingReview_from_Completed_succeeds()
    {
        var doc = CreateCompletedDocument(_tenantId);

        var result = doc.MarkPendingReview();

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.PendingReview, doc.Status);
    }

    [Fact]
    public void MarkPendingReview_from_Submitted_fails()
    {
        var doc = IntakeDocument.Submit(_tenantId, "test.pdf", "storage/key");

        var result = doc.MarkPendingReview();

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_TRANSITION", result.Error.Code);
    }

    [Fact]
    public void MarkPendingReview_from_InReview_fails()
    {
        var doc = CreateCompletedDocument(_tenantId);
        doc.MarkPendingReview();
        doc.StartReview(UserId.New());

        var result = doc.MarkPendingReview();

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_TRANSITION", result.Error.Code);
    }

    // --- StartReview ---

    [Fact]
    public void StartReview_from_PendingReview_succeeds_and_sets_reviewer()
    {
        var doc = CreateCompletedDocument(_tenantId);
        doc.MarkPendingReview();
        var reviewerId = UserId.New();

        var result = doc.StartReview(reviewerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.InReview, doc.Status);
        Assert.Equal(reviewerId, doc.ReviewedBy);
    }

    [Fact]
    public void StartReview_from_Submitted_fails()
    {
        var doc = IntakeDocument.Submit(_tenantId, "test.pdf", "storage/key");

        var result = doc.StartReview(UserId.New());

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_TRANSITION", result.Error.Code);
    }

    // --- CorrectField ---

    [Fact]
    public void CorrectField_while_InReview_updates_corrected_value()
    {
        var doc = CreateCompletedDocument(_tenantId);
        doc.MarkPendingReview();
        var reviewerId = UserId.New();
        doc.StartReview(reviewerId);

        var result = doc.CorrectField("PatientName", "Jane Doe", reviewerId);

        Assert.True(result.IsSuccess);
        Assert.Equal("John Doe", result.Value.previousValue);
        Assert.Equal("Jane Doe", result.Value.newValue);
        Assert.Equal("Jane Doe", doc.ExtractedFields[0].CorrectedValue);
        Assert.Equal("John Doe", doc.ExtractedFields[0].Value); // original preserved
    }

    [Fact]
    public void CorrectField_on_already_corrected_field_uses_corrected_value_as_previous()
    {
        var doc = CreateCompletedDocument(_tenantId);
        doc.MarkPendingReview();
        var reviewerId = UserId.New();
        doc.StartReview(reviewerId);
        doc.CorrectField("PatientName", "Jane Doe", reviewerId);

        var result = doc.CorrectField("PatientName", "Janet Doe", reviewerId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Jane Doe", result.Value.previousValue);
        Assert.Equal("Janet Doe", result.Value.newValue);
    }

    [Fact]
    public void CorrectField_when_not_InReview_fails()
    {
        var doc = CreateCompletedDocument(_tenantId);
        doc.MarkPendingReview();

        var result = doc.CorrectField("PatientName", "Jane Doe", UserId.New());

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_TRANSITION", result.Error.Code);
    }

    [Fact]
    public void CorrectField_for_nonexistent_field_returns_FIELD_NOT_FOUND()
    {
        var doc = CreateCompletedDocument(_tenantId);
        doc.MarkPendingReview();
        doc.StartReview(UserId.New());

        var result = doc.CorrectField("NonExistentField", "somevalue", UserId.New());

        Assert.True(result.IsFailure);
        Assert.Equal("FIELD_NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public void CorrectField_is_case_insensitive_for_field_name()
    {
        var doc = CreateCompletedDocument(_tenantId);
        doc.MarkPendingReview();
        var reviewerId = UserId.New();
        doc.StartReview(reviewerId);

        var result = doc.CorrectField("patientname", "Jane Doe", reviewerId);

        Assert.True(result.IsSuccess);
    }

    // --- Finalize ---

    [Fact]
    public void Finalize_from_InReview_succeeds()
    {
        var doc = CreateCompletedDocument(_tenantId);
        doc.MarkPendingReview();
        var reviewerId = UserId.New();
        doc.StartReview(reviewerId);

        var result = doc.Finalize(reviewerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.Finalized, doc.Status);
        Assert.Equal(reviewerId, doc.ReviewedBy);
        Assert.NotNull(doc.ReviewedAt);
    }

    [Fact]
    public void Finalize_from_PendingReview_fails()
    {
        var doc = CreateCompletedDocument(_tenantId);
        doc.MarkPendingReview();

        var result = doc.Finalize(UserId.New());

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_TRANSITION", result.Error.Code);
    }

    [Fact]
    public void Finalize_from_Finalized_fails()
    {
        var doc = CreateCompletedDocument(_tenantId);
        doc.MarkPendingReview();
        var reviewerId = UserId.New();
        doc.StartReview(reviewerId);
        doc.Finalize(reviewerId);

        var result = doc.Finalize(reviewerId);

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_TRANSITION", result.Error.Code);
    }
}
