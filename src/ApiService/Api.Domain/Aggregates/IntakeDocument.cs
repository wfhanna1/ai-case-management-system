using SharedKernel;

namespace Api.Domain.Aggregates;

/// <summary>
/// Represents the processing lifecycle of a single handwritten intake document.
/// This is the primary aggregate of the API service.
/// </summary>
public sealed class IntakeDocument : AggregateRoot<DocumentId>
{
    public TenantId TenantId { get; private set; }
    public string OriginalFileName { get; private set; }
    public string StorageKey { get; private set; }
    public DocumentStatus Status { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public UserId? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    private readonly List<ExtractedField> _extractedFields = [];
    public IReadOnlyList<ExtractedField> ExtractedFields => _extractedFields.AsReadOnly();

    // Required by EF Core for materialization from database.
    private IntakeDocument() : base(DocumentId.New())
    {
        TenantId = null!;
        OriginalFileName = null!;
        StorageKey = null!;
    }

    private IntakeDocument(
        DocumentId id,
        TenantId tenantId,
        string originalFileName,
        string storageKey) : base(id)
    {
        TenantId = tenantId;
        OriginalFileName = originalFileName;
        StorageKey = storageKey;
        Status = DocumentStatus.Submitted;
        SubmittedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Creates a new intake document submission.
    /// </summary>
    public static IntakeDocument Submit(
        TenantId tenantId,
        string originalFileName,
        string storageKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        var document = new IntakeDocument(DocumentId.New(), tenantId, originalFileName, storageKey);
        document.RaiseDomainEvent(new Events.DocumentSubmittedEvent(document.Id, tenantId));
        return document;
    }

    /// <summary>
    /// Marks the document as having been picked up by the OCR worker for processing.
    /// </summary>
    public Result<Unit> MarkProcessing()
    {
        if (Status != DocumentStatus.Submitted)
            return Result<Unit>.Failure(new Error("INVALID_TRANSITION",
                $"Cannot transition to Processing from {Status}."));

        Status = DocumentStatus.Processing;
        RaiseDomainEvent(new Events.DocumentProcessingStartedEvent(Id, TenantId));
        return Result<Unit>.Success(Unit.Value);
    }

    /// <summary>
    /// Records successful OCR completion and extracted text.
    /// Call MarkPendingReview() after this to route the document to reviewers.
    /// </summary>
    public Result<Unit> MarkCompleted(IReadOnlyList<ExtractedField>? extractedFields = null)
    {
        if (Status != DocumentStatus.Processing)
            return Result<Unit>.Failure(new Error("INVALID_TRANSITION",
                $"Cannot transition to Completed from {Status}."));

        Status = DocumentStatus.Completed;
        ProcessedAt = DateTimeOffset.UtcNow;

        if (extractedFields is not null)
        {
            _extractedFields.Clear();
            _extractedFields.AddRange(extractedFields);
        }

        RaiseDomainEvent(new Events.DocumentCompletedEvent(Id, TenantId));
        return Result<Unit>.Success(Unit.Value);
    }

    /// <summary>
    /// Transitions the document from Completed to PendingReview.
    /// Called after OCR completion to enqueue the document for a reviewer.
    /// </summary>
    public Result<Unit> MarkPendingReview()
    {
        if (Status != DocumentStatus.Completed)
            return Result<Unit>.Failure(new Error("INVALID_TRANSITION",
                $"Cannot transition to PendingReview from {Status}."));

        Status = DocumentStatus.PendingReview;
        return Result<Unit>.Success(Unit.Value);
    }

    /// <summary>
    /// A reviewer claims this document, transitioning it from PendingReview to InReview.
    /// </summary>
    public Result<Unit> StartReview(UserId reviewerId)
    {
        if (Status != DocumentStatus.PendingReview)
            return Result<Unit>.Failure(new Error("INVALID_TRANSITION",
                $"Cannot start review from {Status}."));

        Status = DocumentStatus.InReview;
        ReviewedBy = reviewerId;
        return Result<Unit>.Success(Unit.Value);
    }

    /// <summary>
    /// Corrects the value of a named extracted field. The document must be InReview.
    /// </summary>
    public Result<(string previousValue, string newValue)> CorrectField(string fieldName, string correctedValue, UserId reviewerId)
    {
        if (Status != DocumentStatus.InReview)
            return Result<(string, string)>.Failure(new Error("INVALID_TRANSITION",
                $"Cannot correct fields when document is {Status}."));

        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(correctedValue);

        var field = _extractedFields.FirstOrDefault(f =>
            string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));

        if (field is null)
            return Result<(string, string)>.Failure(new Error("FIELD_NOT_FOUND",
                $"Field '{fieldName}' does not exist on this document."));

        var previousValue = field.CorrectedValue ?? field.Value;
        var index = _extractedFields.IndexOf(field);
        _extractedFields[index] = field.WithCorrection(correctedValue);

        return Result<(string, string)>.Success((previousValue, correctedValue));
    }

    /// <summary>
    /// Finalizes the review. The document must be InReview.
    /// </summary>
    public Result<Unit> Finalize(UserId reviewerId)
    {
        if (Status != DocumentStatus.InReview)
            return Result<Unit>.Failure(new Error("INVALID_TRANSITION",
                $"Cannot finalize from {Status}."));

        Status = DocumentStatus.Finalized;
        ReviewedBy ??= reviewerId;
        ReviewedAt = DateTimeOffset.UtcNow;
        return Result<Unit>.Success(Unit.Value);
    }

    /// <summary>
    /// Records a processing failure with a reason.
    /// </summary>
    public Result<Unit> MarkFailed(string reason)
    {
        if (Status == DocumentStatus.Completed)
            return Result<Unit>.Failure(new Error("INVALID_TRANSITION",
                "Cannot fail an already-completed document."));

        Status = DocumentStatus.Failed;
        RaiseDomainEvent(new Events.DocumentFailedEvent(Id, TenantId, reason));
        return Result<Unit>.Success(Unit.Value);
    }
}
