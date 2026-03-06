using Api.Application.Commands;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class StartReviewHandlerTests
{
    private readonly StubDocumentRepository _documentRepo = new();
    private readonly StubAuditLogRepository _auditRepo = new();
    private readonly StartReviewHandler _handler;

    public StartReviewHandlerTests()
    {
        _handler = new StartReviewHandler(
            _documentRepo, _auditRepo,
            NullLogger<StartReviewHandler>.Instance);
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
    public async Task HandleAsync_document_not_found_returns_NOT_FOUND()
    {
        _documentRepo.Document = null;

        var result = await _handler.HandleAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_repository_find_failure_propagates()
    {
        _documentRepo.FindFailure = new Error("DB_ERROR", "connection refused");

        var result = await _handler.HandleAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DB_ERROR", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_invalid_transition_returns_INVALID_TRANSITION()
    {
        var tenantId = TenantId.New();
        var doc = IntakeDocument.Submit(tenantId, "test.pdf", "storage/key");
        // Document is in Submitted state, which cannot transition to InReview
        _documentRepo.Document = doc;

        var result = await _handler.HandleAsync(
            doc.Id.Value, tenantId.Value, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_TRANSITION", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_happy_path_returns_success_and_updates_document()
    {
        var tenantId = TenantId.New();
        var reviewerId = UserId.New();
        var doc = CreatePendingReviewDocument(tenantId);
        _documentRepo.Document = doc;

        var result = await _handler.HandleAsync(
            doc.Id.Value, tenantId.Value, reviewerId.Value, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.InReview, doc.Status);
        Assert.True(_documentRepo.UpdateCalled);
        Assert.NotNull(_auditRepo.SavedEntry);
        Assert.Equal(AuditAction.ReviewStarted, _auditRepo.SavedEntry!.Action);
    }

    [Fact]
    public async Task HandleAsync_audit_save_failure_still_returns_success()
    {
        var tenantId = TenantId.New();
        var reviewerId = UserId.New();
        var doc = CreatePendingReviewDocument(tenantId);
        _documentRepo.Document = doc;
        _auditRepo.FailOnSave = true;

        var result = await _handler.HandleAsync(
            doc.Id.Value, tenantId.Value, reviewerId.Value, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.InReview, doc.Status);
        Assert.True(_documentRepo.UpdateCalled);
    }

    private sealed class StubDocumentRepository : IDocumentRepository
    {
        public IntakeDocument? Document { get; set; }
        public bool UpdateCalled { get; private set; }
        public Error? FindFailure { get; set; }

        public Task<Result<IntakeDocument?>> FindByIdAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
        {
            if (FindFailure is not null)
                return Task.FromResult(Result<IntakeDocument?>.Failure(FindFailure));
            return Task.FromResult(Result<IntakeDocument?>.Success(Document));
        }

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

        public Task<Result<(IReadOnlyList<IntakeDocument> Items, int TotalCount)>> SearchAsync(
            TenantId tenantId, string? fileNameContains, DocumentStatus? status,
            DateTimeOffset? submittedAfter, DateTimeOffset? submittedBefore,
            string? extractedFieldContains, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private sealed class StubAuditLogRepository : IAuditLogRepository
    {
        public AuditLogEntry? SavedEntry { get; private set; }
        public bool FailOnSave { get; set; }

        public Task<Result<Unit>> SaveAsync(AuditLogEntry entry, CancellationToken ct = default)
        {
            if (FailOnSave)
                return Task.FromResult(Result<Unit>.Failure(new Error("DB_ERROR", "audit write failed")));
            SavedEntry = entry;
            return Task.FromResult(Result<Unit>.Success(Unit.Value));
        }

        public Task<Result<IReadOnlyList<AuditLogEntry>>> ListByDocumentAsync(DocumentId documentId, TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
