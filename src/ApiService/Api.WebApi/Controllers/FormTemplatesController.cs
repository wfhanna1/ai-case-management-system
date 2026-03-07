using Api.Application.Commands;
using Api.Application.DTOs;
using Api.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;

namespace Api.WebApi.Controllers;

/// <summary>
/// Manages form templates used to drive field extraction. Requires authentication.
/// </summary>
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

    /// <summary>
    /// Creates a new form template. Requires Admin role.
    /// </summary>
    /// <param name="request">Template definition including name, type, and fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created form template.</returns>
    /// <response code="201">Template created successfully.</response>
    /// <response code="400">Template creation failed (e.g. duplicate name).</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Caller does not have the Admin role.</response>
    /// <response code="422">Request body failed validation.</response>
    [HttpPost]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType(typeof(ApiResponse<FormTemplateDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<FormTemplateDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<FormTemplateDto>), StatusCodes.Status422UnprocessableEntity)]
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

    /// <summary>
    /// Gets a single form template by its ID.
    /// </summary>
    /// <param name="id">Form template identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Form template with all fields.</returns>
    /// <response code="200">Template found and returned.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="404">Template not found.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<FormTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<FormTemplateDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<FormTemplateDto>), StatusCodes.Status500InternalServerError)]
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

    /// <summary>
    /// Lists all form templates for the authenticated tenant.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All templates for the tenant.</returns>
    /// <response code="200">Templates returned successfully.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FormTemplateDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FormTemplateDto>>), StatusCodes.Status500InternalServerError)]
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
