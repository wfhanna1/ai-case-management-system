using Api.Application.Queries;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class GetFormTemplateByIdHandlerTests
{
    private readonly StubRepository _repository = new();
    private readonly GetFormTemplateByIdHandler _handler;

    public GetFormTemplateByIdHandlerTests()
    {
        _handler = new GetFormTemplateByIdHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_found_returns_success_with_dto()
    {
        var tenantId = TenantId.New();
        var template = FormTemplate.Create(
            tenantId, "Test Template", "Desc", TemplateType.ChildWelfare,
            [new TemplateField("Name", FieldType.Text, true, null)]);
        _repository.FindResult = Result<FormTemplate?>.Success(template);

        var result = await _handler.HandleAsync(template.Id.Value, tenantId.Value, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Test Template", result.Value!.Name);
        Assert.Single(result.Value.Fields);
    }

    [Fact]
    public async Task HandleAsync_not_found_returns_success_with_null()
    {
        _repository.FindResult = Result<FormTemplate?>.Success(null);

        var result = await _handler.HandleAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task HandleAsync_repository_fails_returns_failure()
    {
        _repository.FindResult = Result<FormTemplate?>.Failure(new Error("DB_ERROR", "timeout"));

        var result = await _handler.HandleAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DB_ERROR", result.Error.Code);
    }

    private sealed class StubRepository : IFormTemplateRepository
    {
        public Result<FormTemplate?> FindResult { get; set; } = Result<FormTemplate?>.Success(null);

        public Task<Result<FormTemplate?>> FindByIdAsync(FormTemplateId id, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(FindResult);

        public Task<Result<IReadOnlyList<FormTemplate>>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<Unit>> SaveAsync(FormTemplate template, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
