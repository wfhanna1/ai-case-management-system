using Api.Application.DTOs;
using Api.Application.Queries;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class GetRecentActivitiesHandlerTests
{
    private readonly StubAuditLogRepository _auditRepo = new();
    private readonly GetRecentActivitiesHandler _handler;

    public GetRecentActivitiesHandlerTests()
    {
        _handler = new GetRecentActivitiesHandler(_auditRepo);
    }

    [Fact]
    public async Task HandleAsync_returns_recent_audit_entries()
    {
        var tenantId = TenantId.New();
        var docId = DocumentId.New();
        var reviewerId = UserId.New();

        _auditRepo.RecentEntries =
        [
            AuditLogEntry.RecordReviewFinalized(tenantId, docId, reviewerId),
            AuditLogEntry.RecordFieldCorrected(tenantId, docId, reviewerId, "Name", "Old", "New"),
            AuditLogEntry.RecordExtractionCompleted(tenantId, docId),
        ];

        var result = await _handler.HandleAsync(Guid.NewGuid(), 10, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
        Assert.Equal("ReviewFinalized", result.Value[0].Action);
        Assert.Equal("FieldCorrected", result.Value[1].Action);
        Assert.Equal("ExtractionCompleted", result.Value[2].Action);
    }

    [Fact]
    public async Task HandleAsync_includes_document_id_in_dto()
    {
        var tenantId = TenantId.New();
        var docId = DocumentId.New();

        _auditRepo.RecentEntries =
        [
            AuditLogEntry.RecordExtractionCompleted(tenantId, docId),
        ];

        var result = await _handler.HandleAsync(Guid.NewGuid(), 10, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(docId.Value, result.Value[0].DocumentId);
    }

    [Fact]
    public async Task HandleAsync_returns_empty_list_when_no_entries()
    {
        _auditRepo.RecentEntries = [];

        var result = await _handler.HandleAsync(Guid.NewGuid(), 10, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task HandleAsync_repository_failure_returns_error()
    {
        _auditRepo.FailResult = true;

        var result = await _handler.HandleAsync(Guid.NewGuid(), 10, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DB_ERROR", result.Error.Code);
    }

    private sealed class StubAuditLogRepository : IAuditLogRepository
    {
        public IReadOnlyList<AuditLogEntry> RecentEntries { get; set; } = [];
        public bool FailResult { get; set; }

        public Task<Result<Unit>> SaveAsync(AuditLogEntry entry, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IReadOnlyList<AuditLogEntry>>> ListByDocumentAsync(
            DocumentId documentId, TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IReadOnlyList<AuditLogEntry>>> ListRecentByTenantAsync(
            TenantId tenantId, int limit, CancellationToken ct = default)
        {
            if (FailResult)
                return Task.FromResult(Result<IReadOnlyList<AuditLogEntry>>.Failure(new Error("DB_ERROR", "timeout")));
            return Task.FromResult(Result<IReadOnlyList<AuditLogEntry>>.Success(RecentEntries));
        }
    }
}
