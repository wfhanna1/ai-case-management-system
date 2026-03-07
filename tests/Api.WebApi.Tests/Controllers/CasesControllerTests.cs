using Api.Application.DTOs;
using Api.Application.Queries;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Api.WebApi;
using Api.WebApi.Controllers;
using Api.WebApi.Validation;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;

namespace Api.WebApi.Tests.Controllers;

public sealed class CasesControllerTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();
    private static readonly TenantId Tenant = new(TenantGuid);

    private static readonly Result<(IReadOnlyList<Case> Items, int TotalCount)> EmptyListResult =
        Result<(IReadOnlyList<Case> Items, int TotalCount)>.Success((new List<Case>(), 0));

    private static readonly Result<Case?> NullCaseResult =
        Result<Case?>.Success(null);

    private static CasesController CreateController(ICaseRepository repository)
    {
        var tenantContext = new StubTenantContext(TenantGuid);
        return new CasesController(
            new ListCasesHandler(repository),
            new GetCaseByIdHandler(repository),
            new SearchCasesHandler(repository),
            tenantContext);
    }

    // --- List ---

    [Fact]
    public async Task List_success_returns_200_with_cases()
    {
        var case1 = Case.Create(Tenant, "Alice Smith");
        var case2 = Case.Create(Tenant, "Bob Jones");
        var repo = new StubCaseRepository
        {
            ListResult = Result<(IReadOnlyList<Case> Items, int TotalCount)>.Success(
                (new List<Case> { case1, case2 }, 2)),
            FindByIdResult = NullCaseResult,
            SearchResult = EmptyListResult
        };
        var controller = CreateController(repo);

        var result = await controller.List(page: 1, pageSize: 20);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var response = Assert.IsType<ApiResponse<SearchCasesResultDto>>(okResult.Value);
        Assert.NotNull(response.Data);
        Assert.Null(response.Error);
        Assert.Equal(2, response.Data.TotalCount);
        Assert.Equal(2, response.Data.Items.Count);
        Assert.Equal("Alice Smith", response.Data.Items[0].SubjectName);
        Assert.Equal("Bob Jones", response.Data.Items[1].SubjectName);
    }

    [Fact]
    public async Task List_handler_failure_returns_500()
    {
        var repo = new StubCaseRepository
        {
            ListResult = Result<(IReadOnlyList<Case> Items, int TotalCount)>.Failure(
                new Error("DB_ERROR", "Database connection failed")),
            FindByIdResult = NullCaseResult,
            SearchResult = EmptyListResult
        };
        var controller = CreateController(repo);

        var result = await controller.List(page: 1, pageSize: 20);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var response = Assert.IsType<ApiResponse<SearchCasesResultDto>>(statusResult.Value);
        Assert.Null(response.Data);
        Assert.NotNull(response.Error);
        Assert.Equal("DB_ERROR", response.Error.Code);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_found_returns_200_with_case()
    {
        var existingCase = Case.Create(Tenant, "Charlie Brown");
        var repo = new StubCaseRepository
        {
            ListResult = EmptyListResult,
            FindByIdResult = Result<Case?>.Success(existingCase),
            SearchResult = EmptyListResult
        };
        var controller = CreateController(repo);

        var result = await controller.GetById(existingCase.Id.Value, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var response = Assert.IsType<ApiResponse<CaseDetailDto>>(okResult.Value);
        Assert.NotNull(response.Data);
        Assert.Null(response.Error);
        Assert.Equal(existingCase.Id.Value, response.Data.Id);
        Assert.Equal("Charlie Brown", response.Data.SubjectName);
        Assert.Empty(response.Data.Documents);
    }

    [Fact]
    public async Task GetById_not_found_returns_404()
    {
        var repo = new StubCaseRepository
        {
            ListResult = EmptyListResult,
            FindByIdResult = Result<Case?>.Success(null),
            SearchResult = EmptyListResult
        };
        var controller = CreateController(repo);

        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
        var response = Assert.IsType<ApiResponse<CaseDetailDto>>(notFoundResult.Value);
        Assert.Null(response.Data);
        Assert.NotNull(response.Error);
        Assert.Equal("NOT_FOUND", response.Error.Code);
        Assert.Equal("Case not found", response.Error.Message);
    }

    [Fact]
    public async Task GetById_handler_failure_returns_500()
    {
        var repo = new StubCaseRepository
        {
            ListResult = EmptyListResult,
            FindByIdResult = Result<Case?>.Failure(
                new Error("DB_ERROR", "Connection timeout")),
            SearchResult = EmptyListResult
        };
        var controller = CreateController(repo);

        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var response = Assert.IsType<ApiResponse<CaseDetailDto>>(statusResult.Value);
        Assert.Null(response.Data);
        Assert.NotNull(response.Error);
        Assert.Equal("DB_ERROR", response.Error.Code);
    }

    // --- Search ---

    [Fact]
    public async Task Search_success_returns_200_with_results()
    {
        var case1 = Case.Create(Tenant, "Dana White");
        var repo = new StubCaseRepository
        {
            ListResult = EmptyListResult,
            FindByIdResult = NullCaseResult,
            SearchResult = Result<(IReadOnlyList<Case> Items, int TotalCount)>.Success(
                (new List<Case> { case1 }, 1))
        };
        var controller = CreateController(repo);

        var request = new SearchCasesRequest(Q: "Dana", Status: null, From: null, To: null, Page: 1, PageSize: 20);
        var result = await controller.Search(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var response = Assert.IsType<ApiResponse<SearchCasesResultDto>>(okResult.Value);
        Assert.NotNull(response.Data);
        Assert.Null(response.Error);
        Assert.Equal(1, response.Data.TotalCount);
        Assert.Single(response.Data.Items);
        Assert.Equal("Dana White", response.Data.Items[0].SubjectName);
    }

    [Fact]
    public async Task Search_handler_failure_returns_500()
    {
        var repo = new StubCaseRepository
        {
            ListResult = EmptyListResult,
            FindByIdResult = NullCaseResult,
            SearchResult = Result<(IReadOnlyList<Case> Items, int TotalCount)>.Failure(
                new Error("SEARCH_ERROR", "Search index unavailable"))
        };
        var controller = CreateController(repo);

        var request = new SearchCasesRequest(Q: "test", Status: null, From: null, To: null, Page: 1, PageSize: 20);
        var result = await controller.Search(request);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var response = Assert.IsType<ApiResponse<SearchCasesResultDto>>(statusResult.Value);
        Assert.Null(response.Data);
        Assert.NotNull(response.Error);
        Assert.Equal("SEARCH_ERROR", response.Error.Code);
    }

    // --- Test doubles ---

    private sealed class StubTenantContext : ITenantContext
    {
        public TenantId? TenantId { get; }
        public StubTenantContext(Guid id) => TenantId = new TenantId(id);
    }

    private sealed class StubCaseRepository : ICaseRepository
    {
        public required Result<(IReadOnlyList<Case> Items, int TotalCount)> ListResult { get; set; }
        public required Result<Case?> FindByIdResult { get; set; }
        public required Result<(IReadOnlyList<Case> Items, int TotalCount)> SearchResult { get; set; }

        public Task<Result<(IReadOnlyList<Case> Items, int TotalCount)>> ListByTenantAsync(
            TenantId tenantId, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(ListResult);

        public Task<Result<Case?>> FindByIdAsync(
            CaseId id, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(FindByIdResult);

        public Task<Result<Case?>> FindBySubjectNameAsync(
            string subjectName, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(Result<Case?>.Success(null));

        public Task<Result<(IReadOnlyList<Case> Items, int TotalCount)>> SearchAsync(
            TenantId tenantId, string? query, DocumentStatus? status,
            DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize,
            CancellationToken ct = default)
            => Task.FromResult(SearchResult);

        public Task<Result<Unit>> SaveAsync(Case @case, CancellationToken ct = default)
            => Task.FromResult(Result<Unit>.Success(Unit.Value));

        public Task<Result<Unit>> UpdateAsync(Case @case, CancellationToken ct = default)
            => Task.FromResult(Result<Unit>.Success(Unit.Value));

        public Task<Result<int>> CountByTenantAsync(TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(Result<int>.Success(0));
    }
}
