using Api.Application.Commands;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class AssignDocumentToCaseHandlerTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();
    private static readonly TenantId Tenant = new(TenantGuid);

    private readonly StubDocumentRepository _docRepo = new();
    private readonly StubCaseRepository _caseRepo = new();
    private readonly AssignDocumentToCaseHandler _handler;

    public AssignDocumentToCaseHandlerTests()
    {
        _handler = new AssignDocumentToCaseHandler(
            _docRepo, _caseRepo,
            NullLogger<AssignDocumentToCaseHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_CreatesNewCase_WhenNoMatchExists()
    {
        var doc = CreateDocumentWithNameField("Jane Doe");
        _docRepo.Document = doc;

        var result = await _handler.HandleAsync(doc.Id, Tenant);

        Assert.True(result.IsSuccess);
        Assert.True(_caseRepo.SaveCalled);
        Assert.Equal("Jane Doe", _caseRepo.SavedCase!.SubjectName);
        Assert.NotNull(doc.CaseId);
    }

    [Fact]
    public async Task HandleAsync_LinksToExistingCase_WhenMatchExists()
    {
        var existingCase = Case.Create(Tenant, "John Doe");
        _caseRepo.ExistingCase = existingCase;

        var doc = CreateDocumentWithNameField("John Doe");
        _docRepo.Document = doc;

        var result = await _handler.HandleAsync(doc.Id, Tenant);

        Assert.True(result.IsSuccess);
        Assert.True(_caseRepo.UpdateCalled);
        Assert.False(_caseRepo.SaveCalled);
        Assert.Equal(existingCase.Id, doc.CaseId);
    }

    [Fact]
    public async Task HandleAsync_NoNameField_SkipsAssignment()
    {
        var doc = CreateDocumentWithField("DateOfBirth", "1990-01-01");
        _docRepo.Document = doc;

        var result = await _handler.HandleAsync(doc.Id, Tenant);

        Assert.True(result.IsSuccess);
        Assert.False(_caseRepo.SaveCalled);
        Assert.False(_caseRepo.UpdateCalled);
        Assert.Null(doc.CaseId);
    }

    [Fact]
    public async Task HandleAsync_DocumentNotFound_ReturnsFailure()
    {
        _docRepo.Document = null;

        var result = await _handler.HandleAsync(DocumentId.New(), Tenant);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_BlankNameField_SkipsAssignment()
    {
        var doc = CreateDocumentWithNameField("   ");
        _docRepo.Document = doc;

        var result = await _handler.HandleAsync(doc.Id, Tenant);

        Assert.True(result.IsSuccess);
        Assert.False(_caseRepo.SaveCalled);
    }

    private static IntakeDocument CreateDocumentWithNameField(string nameValue)
    {
        return CreateDocumentWithField("PatientName", nameValue);
    }

    private static IntakeDocument CreateDocumentWithField(string fieldName, string fieldValue)
    {
        var doc = IntakeDocument.Submit(Tenant, "test.pdf", "storage/test.pdf");
        doc.MarkProcessing();
        var fields = new List<ExtractedField> { new(fieldName, fieldValue, 0.95) };
        doc.MarkCompleted(fields);
        doc.MarkPendingReview();
        return doc;
    }

    private sealed class StubDocumentRepository : IDocumentRepository
    {
        public IntakeDocument? Document { get; set; }
        public bool UpdateCalled { get; private set; }

        public Task<Result<IntakeDocument?>> FindByIdUnfilteredAsync(DocumentId id, CancellationToken ct = default)
            => Task.FromResult(Result<IntakeDocument?>.Success(Document));

        public Task<Result<Unit>> UpdateAsync(IntakeDocument document, CancellationToken ct = default)
        {
            UpdateCalled = true;
            return Task.FromResult(Result<Unit>.Success(Unit.Value));
        }

        public Task<Result<IntakeDocument?>> FindByIdAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusAsync(TenantId tenantId, DocumentStatus status, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusesAsync(TenantId tenantId, IReadOnlyList<DocumentStatus> statuses, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Unit>> SaveAsync(IntakeDocument document, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Unit>> DeleteAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<(IReadOnlyList<IntakeDocument> Items, int TotalCount)>> SearchAsync(TenantId tenantId, string? fileNameContains, DocumentStatus? status, DateTimeOffset? submittedAfter, DateTimeOffset? submittedBefore, string? extractedFieldContains, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<(int PendingReview, int ProcessedToday, TimeSpan AverageProcessingTime)>> GetStatsAsync(TenantId tenantId, CancellationToken ct = default) => throw new NotImplementedException();
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
