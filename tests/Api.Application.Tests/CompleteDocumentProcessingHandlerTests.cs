using Api.Application.Commands;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class CompleteDocumentProcessingHandlerTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();
    private static readonly TenantId Tenant = new(TenantGuid);

    private readonly StubDocumentRepository _docRepo = new();
    private readonly StubAuditLogRepository _auditRepo = new();
    private readonly StubCaseRepository _caseRepo = new();
    private readonly CompleteDocumentProcessingHandler _handler;

    public CompleteDocumentProcessingHandlerTests()
    {
        var assignHandler = new AssignDocumentToCaseHandler(
            _docRepo, _caseRepo,
            NullLogger<AssignDocumentToCaseHandler>.Instance);

        _handler = new CompleteDocumentProcessingHandler(
            _docRepo, _auditRepo, assignHandler,
            NullLogger<CompleteDocumentProcessingHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_transitions_Submitted_to_PendingReview()
    {
        var doc = IntakeDocument.Submit(Tenant, "test.pdf", "storage/key");
        _docRepo.Document = doc;
        _docRepo.UnfilteredDocument = doc;

        var fields = new Dictionary<string, (string Value, double Confidence)>
        {
            ["PatientName"] = ("Jane Doe", 0.95),
            ["DateOfBirth"] = ("1990-01-01", 0.88)
        };

        var result = await _handler.HandleAsync(doc.Id.Value, TenantGuid, fields);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.PendingReview, doc.Status);
        Assert.Equal(2, doc.ExtractedFields.Count);
        Assert.True(_docRepo.UpdateCalled);
    }

    [Fact]
    public async Task HandleAsync_is_idempotent_when_already_Processing()
    {
        var doc = IntakeDocument.Submit(Tenant, "test.pdf", "storage/key");
        doc.MarkProcessing();
        _docRepo.Document = doc;
        _docRepo.UnfilteredDocument = doc;

        var fields = new Dictionary<string, (string Value, double Confidence)>
        {
            ["PatientName"] = ("Jane Doe", 0.95)
        };

        var result = await _handler.HandleAsync(doc.Id.Value, TenantGuid, fields);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.PendingReview, doc.Status);
    }

    [Fact]
    public async Task HandleAsync_document_not_found_returns_failure()
    {
        _docRepo.UnfilteredDocument = null;

        var fields = new Dictionary<string, (string Value, double Confidence)>();
        var result = await _handler.HandleAsync(Guid.NewGuid(), TenantGuid, fields);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_tenant_mismatch_returns_failure()
    {
        var otherTenant = TenantId.New();
        var doc = IntakeDocument.Submit(otherTenant, "test.pdf", "storage/key");
        _docRepo.UnfilteredDocument = doc;

        var fields = new Dictionary<string, (string Value, double Confidence)>();
        var result = await _handler.HandleAsync(doc.Id.Value, TenantGuid, fields);

        Assert.True(result.IsFailure);
        Assert.Equal("TENANT_MISMATCH", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_assigns_document_to_case_when_name_field_present()
    {
        var doc = IntakeDocument.Submit(Tenant, "test.pdf", "storage/key");
        _docRepo.Document = doc;
        _docRepo.UnfilteredDocument = doc;

        var fields = new Dictionary<string, (string Value, double Confidence)>
        {
            ["PatientName"] = ("Jane Doe", 0.95)
        };

        var result = await _handler.HandleAsync(doc.Id.Value, TenantGuid, fields);

        Assert.True(result.IsSuccess);
        Assert.NotNull(doc.CaseId);
        Assert.True(_caseRepo.SaveCalled);
    }

    [Fact]
    public async Task HandleAsync_records_audit_entry()
    {
        var doc = IntakeDocument.Submit(Tenant, "test.pdf", "storage/key");
        _docRepo.Document = doc;
        _docRepo.UnfilteredDocument = doc;

        var fields = new Dictionary<string, (string Value, double Confidence)>
        {
            ["PatientName"] = ("Jane Doe", 0.95)
        };

        var result = await _handler.HandleAsync(doc.Id.Value, TenantGuid, fields);

        Assert.True(result.IsSuccess);
        Assert.NotNull(_auditRepo.SavedEntry);
        Assert.Equal(AuditAction.ExtractionCompleted, _auditRepo.SavedEntry!.Action);
    }

    private sealed class StubDocumentRepository : IDocumentRepository
    {
        public IntakeDocument? Document { get; set; }
        public IntakeDocument? UnfilteredDocument { get; set; }
        public bool UpdateCalled { get; private set; }

        public Task<Result<IntakeDocument?>> FindByIdUnfilteredAsync(DocumentId id, CancellationToken ct = default)
            => Task.FromResult(Result<IntakeDocument?>.Success(UnfilteredDocument));

        public Task<Result<Unit>> UpdateAsync(IntakeDocument document, CancellationToken ct = default)
        {
            UpdateCalled = true;
            return Task.FromResult(Result<Unit>.Success(Unit.Value));
        }

        public Task<Result<IntakeDocument?>> FindByIdAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(Result<IntakeDocument?>.Success(Document));
        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusAsync(TenantId tenantId, DocumentStatus status, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<(IReadOnlyList<IntakeDocument> Items, int TotalCount)>> ListByStatusesAsync(TenantId tenantId, IReadOnlyList<DocumentStatus> statuses, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Unit>> SaveAsync(IntakeDocument document, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Unit>> DeleteAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<(IReadOnlyList<IntakeDocument> Items, int TotalCount)>> SearchAsync(TenantId tenantId, string? fileNameContains, DocumentStatus? status, DateTimeOffset? submittedAfter, DateTimeOffset? submittedBefore, string? extractedFieldContains, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<(int PendingReview, int ProcessedToday, TimeSpan AverageProcessingTime)>> GetStatsAsync(TenantId tenantId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class StubAuditLogRepository : IAuditLogRepository
    {
        public AuditLogEntry? SavedEntry => SavedEntries.LastOrDefault();
        public List<AuditLogEntry> SavedEntries { get; } = [];

        public Task<Result<Unit>> SaveAsync(AuditLogEntry entry, CancellationToken ct = default)
        {
            SavedEntries.Add(entry);
            return Task.FromResult(Result<Unit>.Success(Unit.Value));
        }

        public Task<Result<IReadOnlyList<AuditLogEntry>>> ListByDocumentAsync(DocumentId documentId, TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<IReadOnlyList<AuditLogEntry>>> ListRecentByTenantAsync(TenantId tenantId, int limit, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private sealed class StubCaseRepository : ICaseRepository
    {
        public Case? ExistingCase { get; set; }
        public Case? SavedCase { get; private set; }
        public bool SaveCalled { get; private set; }
        public bool UpdateCalled { get; private set; }

        public Task<Result<Case?>> FindBySubjectNameAsync(string subjectName, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(Result<Case?>.Success(ExistingCase));

        public Task<Result<Unit>> SaveAsync(Case @case, CancellationToken ct = default)
        {
            SaveCalled = true;
            SavedCase = @case;
            return Task.FromResult(Result<Unit>.Success(Unit.Value));
        }

        public Task<Result<Unit>> UpdateAsync(Case @case, CancellationToken ct = default)
        {
            UpdateCalled = true;
            return Task.FromResult(Result<Unit>.Success(Unit.Value));
        }

        public Task<Result<Case?>> FindByIdAsync(CaseId id, TenantId tenantId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<(IReadOnlyList<Case> Items, int TotalCount)>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<(IReadOnlyList<Case> Items, int TotalCount)>> SearchAsync(TenantId tenantId, string? query, DocumentStatus? status, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<int>> CountByTenantAsync(TenantId tenantId, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
