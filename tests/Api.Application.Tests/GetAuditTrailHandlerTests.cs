using Api.Application.Queries;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class GetAuditTrailHandlerTests
{
    private readonly StubAuditLogRepository _auditRepo = new();
    private readonly GetAuditTrailHandler _handler;

    public GetAuditTrailHandlerTests()
    {
        _handler = new GetAuditTrailHandler(_auditRepo);
    }

    [Fact]
    public async Task HandleAsync_returns_mapped_audit_entries()
    {
        var tenantId = TenantId.New();
        var documentId = DocumentId.New();
        var reviewerId = UserId.New();

        _auditRepo.Entries =
        [
            AuditLogEntry.RecordExtractionCompleted(tenantId, documentId),
            AuditLogEntry.RecordReviewStarted(tenantId, documentId, reviewerId),
            AuditLogEntry.RecordFieldCorrected(tenantId, documentId, reviewerId, "PatientName", "John", "Jane"),
            AuditLogEntry.RecordReviewFinalized(tenantId, documentId, reviewerId)
        ];

        var result = await _handler.HandleAsync(documentId.Value, tenantId.Value, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.Count);
        Assert.Equal("ExtractionCompleted", result.Value[0].Action);
        Assert.Equal("ReviewStarted", result.Value[1].Action);
        Assert.Equal("FieldCorrected", result.Value[2].Action);
        Assert.Equal("ReviewFinalized", result.Value[3].Action);
        Assert.Equal("PatientName", result.Value[2].FieldName);
    }

    [Fact]
    public async Task HandleAsync_repository_failure_returns_failure()
    {
        _auditRepo.FailResult = true;

        var result = await _handler.HandleAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DB_ERROR", result.Error.Code);
    }

    private sealed class StubAuditLogRepository : IAuditLogRepository
    {
        public IReadOnlyList<AuditLogEntry> Entries { get; set; } = [];
        public bool FailResult { get; set; }

        public Task<Result<Unit>> SaveAsync(AuditLogEntry entry, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IReadOnlyList<AuditLogEntry>>> ListByDocumentAsync(
            DocumentId documentId, TenantId tenantId, CancellationToken ct = default)
        {
            if (FailResult)
                return Task.FromResult(Result<IReadOnlyList<AuditLogEntry>>.Failure(new Error("DB_ERROR", "timeout")));
            return Task.FromResult(Result<IReadOnlyList<AuditLogEntry>>.Success(Entries));
        }

        public Task<Result<IReadOnlyList<AuditLogEntry>>> ListRecentByTenantAsync(
            TenantId tenantId, int limit, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
