using Api.Application.Commands;
using Api.Application.DTOs;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class CreateFormTemplateHandlerTests
{
    private readonly StubRepository _repository = new();
    private readonly CreateFormTemplateHandler _handler;

    public CreateFormTemplateHandlerTests()
    {
        _handler = new CreateFormTemplateHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_valid_request_returns_success_with_dto()
    {
        var tenantId = Guid.NewGuid();
        var request = new CreateFormTemplateRequest(
            "Child Welfare Intake",
            "Intake form for child welfare cases",
            "ChildWelfare",
            [new TemplateFieldDto("FullName", "Text", true, null)]);

        var result = await _handler.HandleAsync(tenantId, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Child Welfare Intake", result.Value.Name);
        Assert.Equal("ChildWelfare", result.Value.Type);
        Assert.Single(result.Value.Fields);
        Assert.True(_repository.SavedTemplate is not null);
    }

    [Fact]
    public async Task HandleAsync_invalid_type_returns_failure()
    {
        var request = new CreateFormTemplateRequest("Name", "Desc", "InvalidType", []);

        var result = await _handler.HandleAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_TEMPLATE_TYPE", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_invalid_field_type_returns_failure()
    {
        var request = new CreateFormTemplateRequest(
            "Name", "Desc", "ChildWelfare",
            [new TemplateFieldDto("Label", "BadFieldType", true, null)]);

        var result = await _handler.HandleAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_FIELD_TYPE", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_repository_failure_returns_failure()
    {
        _repository.SaveResult = Result<Unit>.Failure(new Error("DB_ERROR", "timeout"));
        var request = new CreateFormTemplateRequest("Name", "Desc", "ChildWelfare", []);

        var result = await _handler.HandleAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DB_ERROR", result.Error.Code);
    }

    private sealed class StubRepository : IFormTemplateRepository
    {
        public FormTemplate? SavedTemplate { get; private set; }
        public Result<Unit> SaveResult { get; set; } = Result<Unit>.Success(Unit.Value);

        public Task<Result<Unit>> SaveAsync(FormTemplate template, CancellationToken ct = default)
        {
            SavedTemplate = template;
            return Task.FromResult(SaveResult);
        }

        public Task<Result<FormTemplate?>> FindByIdAsync(FormTemplateId id, TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<IReadOnlyList<FormTemplate>>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
