using Api.Application.Queries;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class SearchCasesHandlerTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();
    private readonly StubCaseRepository _repository = new();
    private readonly SearchCasesHandler _handler;

    public SearchCasesHandlerTests()
    {
        _handler = new SearchCasesHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_ReturnsMatchingCases()
    {
        var c = Case.Create(new TenantId(TenantGuid), "John Doe");
        _repository.SearchResult = Result<(IReadOnlyList<Case>, int)>.Success(
            (new List<Case> { c }, 1));

        var result = await _handler.HandleAsync(TenantGuid, "John", null, null, null, 1, 10);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("John Doe", result.Value.Items[0].SubjectName);
    }

    [Fact]
    public async Task HandleAsync_ParsesStatusFilter()
    {
        _repository.SearchResult = Result<(IReadOnlyList<Case>, int)>.Success(
            (new List<Case>(), 0));

        await _handler.HandleAsync(TenantGuid, null, "Finalized", null, null, 1, 10);

        Assert.Equal(DocumentStatus.Finalized, _repository.LastStatusFilter);
    }

    [Fact]
    public async Task HandleAsync_ClampsPageAndPageSize()
    {
        _repository.SearchResult = Result<(IReadOnlyList<Case>, int)>.Success(
            (new List<Case>(), 0));

        var result = await _handler.HandleAsync(TenantGuid, null, null, null, null, -1, 500);

        Assert.Equal(1, result.Value.Page);
        Assert.Equal(100, result.Value.PageSize);
    }

    [Fact]
    public async Task HandleAsync_RepoFailure_ReturnsFailure()
    {
        _repository.SearchResult = Result<(IReadOnlyList<Case>, int)>.Failure(
            new Error("DB_ERROR", "fail"));

        var result = await _handler.HandleAsync(TenantGuid, null, null, null, null, 1, 10);

        Assert.True(result.IsFailure);
    }

    private sealed class StubCaseRepository : ICaseRepository
    {
        public Result<(IReadOnlyList<Case>, int)> SearchResult { get; set; } =
            Result<(IReadOnlyList<Case>, int)>.Success((new List<Case>(), 0));
        public DocumentStatus? LastStatusFilter { get; private set; }

        public Task<Result<(IReadOnlyList<Case> Items, int TotalCount)>> SearchAsync(
            TenantId tenantId, string? query, DocumentStatus? status,
            DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken ct = default)
        {
            LastStatusFilter = status;
            return Task.FromResult(SearchResult);
        }

        public Task<Result<Case?>> FindByIdAsync(CaseId id, TenantId tenantId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Case?>> FindBySubjectNameAsync(string subjectName, TenantId tenantId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<(IReadOnlyList<Case> Items, int TotalCount)>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Unit>> SaveAsync(Case @case, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Unit>> UpdateAsync(Case @case, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<int>> CountByTenantAsync(TenantId tenantId, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
