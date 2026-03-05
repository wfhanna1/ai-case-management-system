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
    /// </summary>
    public Result<Unit> MarkCompleted()
    {
        if (Status != DocumentStatus.Processing)
            return Result<Unit>.Failure(new Error("INVALID_TRANSITION",
                $"Cannot transition to Completed from {Status}."));

        Status = DocumentStatus.Completed;
        ProcessedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new Events.DocumentCompletedEvent(Id, TenantId));
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
