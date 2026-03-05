using Api.Application.Queries;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class GetCaseByIdHandlerTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();
    private readonly StubCaseRepository _repository = new();
    private readonly GetCaseByIdHandler _handler;

    public GetCaseByIdHandlerTests()
    {
        _handler = new GetCaseByIdHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_Found_ReturnsCaseDetail()
    {
        var c = Case.Create(new TenantId(TenantGuid), "Jane Smith");
        _repository.FindResult = Result<Case?>.Success(c);

        var result = await _handler.HandleAsync(c.Id.Value, TenantGuid);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Jane Smith", result.Value!.SubjectName);
    }

    [Fact]
    public async Task HandleAsync_NotFound_ReturnsNull()
    {
        _repository.FindResult = Result<Case?>.Success(null);

        var result = await _handler.HandleAsync(Guid.NewGuid(), TenantGuid);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task HandleAsync_RepoFailure_ReturnsFailure()
    {
        _repository.FindResult = Result<Case?>.Failure(new Error("DB_ERROR", "fail"));

        var result = await _handler.HandleAsync(Guid.NewGuid(), TenantGuid);

        Assert.True(result.IsFailure);
        Assert.Equal("DB_ERROR", result.Error.Code);
    }

    private sealed class StubCaseRepository : ICaseRepository
    {
        public Result<Case?> FindResult { get; set; } = Result<Case?>.Success(null);

        public Task<Result<Case?>> FindByIdAsync(CaseId id, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(FindResult);

        public Task<Result<Case?>> FindBySubjectNameAsync(string subjectName, TenantId tenantId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<(IReadOnlyList<Case> Items, int TotalCount)>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<(IReadOnlyList<Case> Items, int TotalCount)>> SearchAsync(TenantId tenantId, string? query, DocumentStatus? status, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Unit>> SaveAsync(Case @case, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Unit>> UpdateAsync(Case @case, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
