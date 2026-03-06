using Api.Application.Commands;
using Api.Application.DTOs;
using Api.Application.Queries;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Api.WebApi;
using Api.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;

namespace Api.WebApi.Tests.Controllers;

public sealed class FormTemplatesControllerTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();

    private static FormTemplatesController CreateController(StubFormTemplateRepository repo)
    {
        var tenantContext = new StubTenantContext(TenantGuid);
        var createHandler = new CreateFormTemplateHandler(repo);
        var getByIdHandler = new GetFormTemplateByIdHandler(repo);
        var listHandler = new ListFormTemplatesByTenantHandler(repo);
        return new FormTemplatesController(createHandler, getByIdHandler, listHandler, tenantContext);
    }

    private static CreateFormTemplateRequest ValidRequest() =>
        new("Child Welfare Intake",
            "Standard child welfare form",
            "ChildWelfare",
            new List<TemplateFieldDto>
            {
                new("Child Name", "Text", true, null),
                new("Date of Birth", "Date", true, null),
                new("Notes", "TextArea", false, null)
            });

    [Fact]
    public async Task Create_success_returns_201_with_template()
    {
        var repo = new StubFormTemplateRepository();
        var controller = CreateController(repo);

        var result = await controller.Create(ValidRequest(), CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, createdResult.StatusCode);

        var response = Assert.IsType<ApiResponse<FormTemplateDto>>(createdResult.Value);
        Assert.NotNull(response.Data);
        Assert.Null(response.Error);
        Assert.Equal("Child Welfare Intake", response.Data.Name);
        Assert.Equal("Standard child welfare form", response.Data.Description);
        Assert.Equal("ChildWelfare", response.Data.Type);
        Assert.True(response.Data.IsActive);
        Assert.Equal(3, response.Data.Fields.Count);
        Assert.Equal(TenantGuid, response.Data.TenantId);

        Assert.Single(repo.Saved);
    }

    [Fact]
    public async Task Create_handler_failure_returns_error()
    {
        var repo = new StubFormTemplateRepository();
        var controller = CreateController(repo);

        var request = new CreateFormTemplateRequest(
            "Bad Template",
            "Desc",
            "InvalidType",
            new List<TemplateFieldDto>());

        var result = await controller.Create(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<FormTemplateDto>>(badRequest.Value);
        Assert.NotNull(response.Error);
        Assert.Equal("INVALID_TEMPLATE_TYPE", response.Error.Code);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task GetById_found_returns_200()
    {
        var repo = new StubFormTemplateRepository();
        var template = FormTemplate.Create(
            new TenantId(TenantGuid),
            "Existing Template",
            "Some description",
            TemplateType.HousingAssistance,
            new List<TemplateField>
            {
                new("Address", FieldType.Text, true, null)
            });
        repo.Templates.Add(template);

        var controller = CreateController(repo);

        var result = await controller.GetById(template.Id.Value, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<FormTemplateDto>>(okResult.Value);
        Assert.NotNull(response.Data);
        Assert.Null(response.Error);
        Assert.Equal("Existing Template", response.Data.Name);
        Assert.Equal("HousingAssistance", response.Data.Type);
        Assert.Single(response.Data.Fields);
    }

    [Fact]
    public async Task GetById_not_found_returns_404()
    {
        var repo = new StubFormTemplateRepository();
        var controller = CreateController(repo);

        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var response = Assert.IsType<ApiResponse<FormTemplateDto>>(notFoundResult.Value);
        Assert.NotNull(response.Error);
        Assert.Equal("NOT_FOUND", response.Error.Code);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task List_success_returns_200_with_templates()
    {
        var repo = new StubFormTemplateRepository();

        var template1 = FormTemplate.Create(
            new TenantId(TenantGuid),
            "Template A",
            "First",
            TemplateType.ChildWelfare,
            new List<TemplateField>());
        var template2 = FormTemplate.Create(
            new TenantId(TenantGuid),
            "Template B",
            "Second",
            TemplateType.MentalHealthReferral,
            new List<TemplateField> { new("Referral Reason", FieldType.TextArea, true, null) });

        repo.Templates.Add(template1);
        repo.Templates.Add(template2);

        var controller = CreateController(repo);

        var result = await controller.List(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<FormTemplateDto>>>(okResult.Value);
        Assert.NotNull(response.Data);
        Assert.Null(response.Error);
        Assert.Equal(2, response.Data.Count);
        Assert.Equal("Template A", response.Data[0].Name);
        Assert.Equal("Template B", response.Data[1].Name);
    }

    [Fact]
    public async Task List_handler_failure_returns_500()
    {
        var repo = new StubFormTemplateRepository
        {
            ListShouldFail = true
        };
        var controller = CreateController(repo);

        var result = await controller.List(CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<FormTemplateDto>>>(statusResult.Value);
        Assert.NotNull(response.Error);
        Assert.Null(response.Data);
    }

    private sealed class StubTenantContext : ITenantContext
    {
        public TenantId? TenantId { get; }
        public StubTenantContext(Guid id) => TenantId = new TenantId(id);
    }

    private sealed class StubFormTemplateRepository : IFormTemplateRepository
    {
        public List<FormTemplate> Templates { get; } = [];
        public List<FormTemplate> Saved { get; } = [];
        public bool ListShouldFail { get; set; }

        public Task<Result<FormTemplate?>> FindByIdAsync(
            FormTemplateId id, TenantId tenantId, CancellationToken ct = default)
        {
            var template = Templates.FirstOrDefault(
                t => t.Id.Value == id.Value && t.TenantId.Value == tenantId.Value);
            return Task.FromResult(Result<FormTemplate?>.Success(template));
        }

        public Task<Result<IReadOnlyList<FormTemplate>>> ListByTenantAsync(
            TenantId tenantId, CancellationToken ct = default)
        {
            if (ListShouldFail)
                return Task.FromResult(Result<IReadOnlyList<FormTemplate>>.Failure(
                    new Error("DB_ERROR", "Database connection failed")));

            var filtered = Templates.Where(t => t.TenantId.Value == tenantId.Value).ToList();
            return Task.FromResult(Result<IReadOnlyList<FormTemplate>>.Success(
                (IReadOnlyList<FormTemplate>)filtered));
        }

        public Task<Result<Unit>> SaveAsync(FormTemplate template, CancellationToken ct = default)
        {
            Saved.Add(template);
            Templates.Add(template);
            return Task.FromResult(Result<Unit>.Success(Unit.Value));
        }
    }
}
