using Api.Application.Queries;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class ListFormTemplatesByTenantHandlerTests
{
    private readonly StubRepository _repository = new();
    private readonly ListFormTemplatesByTenantHandler _handler;

    public ListFormTemplatesByTenantHandlerTests()
    {
        _handler = new ListFormTemplatesByTenantHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_returns_template_list()
    {
        var tenantId = TenantId.New();
        var templates = new List<FormTemplate>
        {
            FormTemplate.Create(tenantId, "Template 1", "Desc 1", TemplateType.ChildWelfare, []),
            FormTemplate.Create(tenantId, "Template 2", "Desc 2", TemplateType.HousingAssistance, [])
        };
        _repository.ListResult = Result<IReadOnlyList<FormTemplate>>.Success(templates);

        var result = await _handler.HandleAsync(tenantId.Value, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task HandleAsync_empty_list_returns_success()
    {
        _repository.ListResult = Result<IReadOnlyList<FormTemplate>>.Success(new List<FormTemplate>());

        var result = await _handler.HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task HandleAsync_repository_fails_returns_failure()
    {
        _repository.ListResult = Result<IReadOnlyList<FormTemplate>>.Failure(new Error("DB_ERROR", "timeout"));

        var result = await _handler.HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    private sealed class StubRepository : IFormTemplateRepository
    {
        public Result<IReadOnlyList<FormTemplate>> ListResult { get; set; } =
            Result<IReadOnlyList<FormTemplate>>.Success(new List<FormTemplate>());

        public Task<Result<IReadOnlyList<FormTemplate>>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(ListResult);

        public Task<Result<FormTemplate?>> FindByIdAsync(FormTemplateId id, TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<Unit>> SaveAsync(FormTemplate template, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
