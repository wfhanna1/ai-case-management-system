using Api.Application.Commands;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class CorrectFieldHandlerTests
{
    private readonly StubDocumentRepository _documentRepo = new();
    private readonly StubAuditLogRepository _auditRepo = new();
    private readonly CorrectFieldHandler _handler;

    public CorrectFieldHandlerTests()
    {
        _handler = new CorrectFieldHandler(
            _documentRepo, _auditRepo,
            NullLogger<CorrectFieldHandler>.Instance);
    }

    private static IntakeDocument CreateInReviewDocument(TenantId tenantId)
    {
        var doc = IntakeDocument.Submit(tenantId, "test.pdf", "storage/key");
        doc.MarkProcessing();
        doc.MarkCompleted([new ExtractedField("PatientName", "John Doe", 0.95)]);
        doc.MarkPendingReview();
        doc.StartReview(UserId.New());
        return doc;
    }

    [Fact]
    public async Task HandleAsync_valid_correction_updates_field_and_saves_audit()
    {
        var tenantId = TenantId.New();
        var reviewerId = UserId.New();
        var doc = CreateInReviewDocument(tenantId);
        _documentRepo.Document = doc;

        var result = await _handler.HandleAsync(
            doc.Id.Value, tenantId.Value, reviewerId.Value, "PatientName", "Jane Doe",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(_documentRepo.UpdateCalled);
        Assert.NotNull(_auditRepo.SavedEntry);
        Assert.Equal(AuditAction.FieldCorrected, _auditRepo.SavedEntry!.Action);
        Assert.Equal("PatientName", _auditRepo.SavedEntry.FieldName);
        Assert.Equal("John Doe", _auditRepo.SavedEntry.PreviousValue);
        Assert.Equal("Jane Doe", _auditRepo.SavedEntry.NewValue);
    }

    [Fact]
    public async Task HandleAsync_corrected_value_persists_on_document()
    {
        var tenantId = TenantId.New();
        var reviewerId = UserId.New();
        var doc = CreateInReviewDocument(tenantId);
        _documentRepo.Document = doc;

        var result = await _handler.HandleAsync(
            doc.Id.Value, tenantId.Value, reviewerId.Value, "PatientName", "Jane Doe",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var field = doc.ExtractedFields.Single(f => f.Name == "PatientName");
        Assert.Equal("Jane Doe", field.CorrectedValue);
        Assert.Equal("John Doe", field.Value);
    }

    [Fact]
    public async Task HandleAsync_document_not_found_returns_NOT_FOUND()
    {
        var tenantId = TenantId.New();
        _documentRepo.Document = null;

        var result = await _handler.HandleAsync(
            Guid.NewGuid(), tenantId.Value, Guid.NewGuid(), "PatientName", "Jane Doe",
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_field_not_found_returns_FIELD_NOT_FOUND()
    {
        var tenantId = TenantId.New();
        var reviewerId = UserId.New();
        var doc = CreateInReviewDocument(tenantId);
        _documentRepo.Document = doc;

        var result = await _handler.HandleAsync(
            doc.Id.Value, tenantId.Value, reviewerId.Value, "NonExistent", "value",
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FIELD_NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_document_not_in_review_returns_INVALID_TRANSITION()
    {
        var tenantId = TenantId.New();
        var doc = IntakeDocument.Submit(tenantId, "test.pdf", "storage/key");
        _documentRepo.Document = doc;

        var result = await _handler.HandleAsync(
            doc.Id.Value, tenantId.Value, Guid.NewGuid(), "PatientName", "value",
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_TRANSITION", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_repository_failure_returns_failure()
    {
        var tenantId = TenantId.New();
        _documentRepo.FindResult = Result<IntakeDocument?>.Failure(new Error("DB_ERROR", "timeout"));

        var result = await _handler.HandleAsync(
            Guid.NewGuid(), tenantId.Value, Guid.NewGuid(), "PatientName", "value",
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DB_ERROR", result.Error.Code);
    }

    private sealed class StubDocumentRepository : IDocumentRepository
    {
        public IntakeDocument? Document { get; set; }
        public bool UpdateCalled { get; private set; }
        public Result<IntakeDocument?> FindResult { get; set; } = Result<IntakeDocument?>.Success(null);

        public Task<Result<IntakeDocument?>> FindByIdAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
        {
            if (FindResult.IsFailure) return Task.FromResult(FindResult);
            return Task.FromResult(Result<IntakeDocument?>.Success(Document));
        }

        public Task<Result<IntakeDocument?>> FindByIdUnfilteredAsync(DocumentId id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusAsync(TenantId tenantId, DocumentStatus status, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<(IReadOnlyList<IntakeDocument> Items, int TotalCount)>> ListByStatusesAsync(TenantId tenantId, IReadOnlyList<DocumentStatus> statuses, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<Unit>> SaveAsync(IntakeDocument document, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<Unit>> UpdateAsync(IntakeDocument document, CancellationToken ct = default)
        {
            UpdateCalled = true;
            return Task.FromResult(Result<Unit>.Success(Unit.Value));
        }

        public Task<Result<Unit>> DeleteAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<(IReadOnlyList<IntakeDocument> Items, int TotalCount)>> SearchAsync(
            TenantId tenantId, string? fileNameContains, DocumentStatus? status,
            DateTimeOffset? submittedAfter, DateTimeOffset? submittedBefore,
            string? extractedFieldContains, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<(int PendingReview, int ProcessedToday, TimeSpan AverageProcessingTime)>> GetStatsAsync(
            TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private sealed class StubAuditLogRepository : IAuditLogRepository
    {
        public AuditLogEntry? SavedEntry { get; private set; }

        public Task<Result<Unit>> SaveAsync(AuditLogEntry entry, CancellationToken ct = default)
        {
            SavedEntry = entry;
            return Task.FromResult(Result<Unit>.Success(Unit.Value));
        }

        public Task<Result<IReadOnlyList<AuditLogEntry>>> ListByDocumentAsync(DocumentId documentId, TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
