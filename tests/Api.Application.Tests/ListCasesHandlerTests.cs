using Api.Application.Queries;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class ListCasesHandlerTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();
    private readonly StubCaseRepository _repository = new();
    private readonly ListCasesHandler _handler;

    public ListCasesHandlerTests()
    {
        _handler = new ListCasesHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_ReturnsCases()
    {
        var c = Case.Create(new TenantId(TenantGuid), "John Doe");
        _repository.ListResult = Result<(IReadOnlyList<Case>, int)>.Success(
            (new List<Case> { c }, 1));

        var result = await _handler.HandleAsync(TenantGuid, 1, 10);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("John Doe", result.Value.Items[0].SubjectName);
        Assert.Equal(1, result.Value.TotalCount);
    }

    [Fact]
    public async Task HandleAsync_EmptyResults()
    {
        _repository.ListResult = Result<(IReadOnlyList<Case>, int)>.Success(
            (new List<Case>(), 0));

        var result = await _handler.HandleAsync(TenantGuid, 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
    }

    [Fact]
    public async Task HandleAsync_ClampsPageAndPageSize()
    {
        _repository.ListResult = Result<(IReadOnlyList<Case>, int)>.Success(
            (new List<Case>(), 0));

        var result = await _handler.HandleAsync(TenantGuid, 0, 200);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Page);
        Assert.Equal(100, result.Value.PageSize);
    }

    [Fact]
    public async Task HandleAsync_RepoFailure_ReturnsFailure()
    {
        _repository.ListResult = Result<(IReadOnlyList<Case>, int)>.Failure(
            new Error("DB_ERROR", "connection lost"));

        var result = await _handler.HandleAsync(TenantGuid, 1, 10);

        Assert.True(result.IsFailure);
        Assert.Equal("DB_ERROR", result.Error.Code);
    }

    private sealed class StubCaseRepository : ICaseRepository
    {
        public Result<(IReadOnlyList<Case>, int)> ListResult { get; set; } =
            Result<(IReadOnlyList<Case>, int)>.Success((new List<Case>(), 0));

        public Task<Result<(IReadOnlyList<Case> Items, int TotalCount)>> ListByTenantAsync(
            TenantId tenantId, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(ListResult);

        public Task<Result<Case?>> FindByIdAsync(CaseId id, TenantId tenantId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Case?>> FindBySubjectNameAsync(string subjectName, TenantId tenantId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<(IReadOnlyList<Case> Items, int TotalCount)>> SearchAsync(TenantId tenantId, string? query, DocumentStatus? status, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Unit>> SaveAsync(Case @case, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Unit>> UpdateAsync(Case @case, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<int>> CountByTenantAsync(TenantId tenantId, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
