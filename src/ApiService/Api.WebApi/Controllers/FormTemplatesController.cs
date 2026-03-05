using Api.Application.Commands;
using Api.Application.DTOs;
using Api.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;

namespace Api.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/form-templates")]
public sealed class FormTemplatesController : ControllerBase
{
    private readonly CreateFormTemplateHandler _createHandler;
    private readonly GetFormTemplateByIdHandler _getByIdHandler;
    private readonly ListFormTemplatesByTenantHandler _listHandler;
    private readonly ITenantContext _tenantContext;

    public FormTemplatesController(
        CreateFormTemplateHandler createHandler,
        GetFormTemplateByIdHandler getByIdHandler,
        ListFormTemplatesByTenantHandler listHandler,
        ITenantContext tenantContext)
    {
        _createHandler = createHandler;
        _getByIdHandler = getByIdHandler;
        _listHandler = listHandler;
        _tenantContext = tenantContext;
    }

    [HttpPost]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Create(
        [FromBody] CreateFormTemplateRequest request,
        CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var result = await _createHandler.HandleAsync(tenantId, request, ct);

        if (result.IsFailure)
            return BadRequest(ApiResponse<FormTemplateDto>.Fail(result.Error.Code, result.Error.Message));

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id },
            ApiResponse<FormTemplateDto>.Ok(result.Value));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var result = await _getByIdHandler.HandleAsync(id, tenantId, ct);

        if (result.IsFailure)
            return StatusCode(500, ApiResponse<FormTemplateDto>.Fail(result.Error.Code, "An internal error occurred"));

        if (result.Value is null)
            return NotFound(ApiResponse<FormTemplateDto>.Fail("NOT_FOUND", "Form template not found"));

        return Ok(ApiResponse<FormTemplateDto>.Ok(result.Value));
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var result = await _listHandler.HandleAsync(tenantId, ct);

        if (result.IsFailure)
            return StatusCode(500, ApiResponse<IReadOnlyList<FormTemplateDto>>.Fail(
                result.Error.Code, "An internal error occurred"));

        return Ok(ApiResponse<IReadOnlyList<FormTemplateDto>>.Ok(result.Value));
    }
}
