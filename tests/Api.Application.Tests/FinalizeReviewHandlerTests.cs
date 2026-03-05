using Api.Application.Commands;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class FinalizeReviewHandlerTests
{
    private readonly StubDocumentRepository _documentRepo = new();
    private readonly StubAuditLogRepository _auditRepo = new();
    private readonly FinalizeReviewHandler _handler;

    public FinalizeReviewHandlerTests()
    {
        _handler = new FinalizeReviewHandler(
            _documentRepo, _auditRepo,
            NullLogger<FinalizeReviewHandler>.Instance);
    }

    private static IntakeDocument CreatePendingReviewDocument(TenantId tenantId)
    {
        var doc = IntakeDocument.Submit(tenantId, "test.pdf", "storage/key");
        doc.MarkProcessing();
        doc.MarkCompleted();
        doc.MarkPendingReview();
        return doc;
    }

    [Fact]
    public async Task HandleAsync_from_InReview_finalizes_and_creates_audit_entry()
    {
        var tenantId = TenantId.New();
        var reviewerId = UserId.New();
        var doc = CreatePendingReviewDocument(tenantId);
        doc.StartReview(reviewerId);
        _documentRepo.Document = doc;

        var result = await _handler.HandleAsync(
            doc.Id.Value, tenantId.Value, reviewerId.Value, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.Finalized, doc.Status);
        Assert.True(_documentRepo.UpdateCalled);
        Assert.NotNull(_auditRepo.SavedEntry);
        Assert.Equal(AuditAction.ReviewFinalized, _auditRepo.SavedEntry!.Action);
    }

    [Fact]
    public async Task HandleAsync_from_PendingReview_starts_and_finalizes()
    {
        var tenantId = TenantId.New();
        var reviewerId = UserId.New();
        var doc = CreatePendingReviewDocument(tenantId);
        _documentRepo.Document = doc;

        var result = await _handler.HandleAsync(
            doc.Id.Value, tenantId.Value, reviewerId.Value, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.Finalized, doc.Status);
    }

    [Fact]
    public async Task HandleAsync_document_not_found_returns_NOT_FOUND()
    {
        _documentRepo.Document = null;

        var result = await _handler.HandleAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_already_finalized_returns_INVALID_TRANSITION()
    {
        var tenantId = TenantId.New();
        var reviewerId = UserId.New();
        var doc = CreatePendingReviewDocument(tenantId);
        doc.StartReview(reviewerId);
        doc.Finalize(reviewerId);
        _documentRepo.Document = doc;

        var result = await _handler.HandleAsync(
            doc.Id.Value, tenantId.Value, reviewerId.Value, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_TRANSITION", result.Error.Code);
    }

    private sealed class StubDocumentRepository : IDocumentRepository
    {
        public IntakeDocument? Document { get; set; }
        public bool UpdateCalled { get; private set; }

        public Task<Result<IntakeDocument?>> FindByIdAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(Result<IntakeDocument?>.Success(Document));

        public Task<Result<IntakeDocument?>> FindByIdUnfilteredAsync(DocumentId id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusAsync(TenantId tenantId, DocumentStatus status, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusesAsync(TenantId tenantId, IReadOnlyList<DocumentStatus> statuses, int page, int pageSize, CancellationToken ct = default)
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
