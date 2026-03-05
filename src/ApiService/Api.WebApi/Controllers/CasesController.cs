using Api.Application.DTOs;
using Api.Application.Queries;
using Api.WebApi.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;

namespace Api.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class CasesController : ControllerBase
{
    private readonly ListCasesHandler _listHandler;
    private readonly GetCaseByIdHandler _getByIdHandler;
    private readonly SearchCasesHandler _searchHandler;
    private readonly ITenantContext _tenantContext;

    public CasesController(
        ListCasesHandler listHandler,
        GetCaseByIdHandler getByIdHandler,
        SearchCasesHandler searchHandler,
        ITenantContext tenantContext)
    {
        _listHandler = listHandler;
        _getByIdHandler = getByIdHandler;
        _searchHandler = searchHandler;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var result = await _listHandler.HandleAsync(tenantId, page, pageSize, ct);

        if (result.IsFailure)
            return StatusCode(500, ApiResponse<SearchCasesResultDto>.Fail(
                result.Error.Code, "An internal error occurred"));

        return Ok(ApiResponse<SearchCasesResultDto>.Ok(result.Value));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var result = await _getByIdHandler.HandleAsync(id, tenantId, ct);

        if (result.IsFailure)
            return StatusCode(500, ApiResponse<CaseDetailDto>.Fail(
                result.Error.Code, "An internal error occurred"));

        if (result.Value is null)
            return NotFound(ApiResponse<CaseDetailDto>.Fail("NOT_FOUND", "Case not found"));

        return Ok(ApiResponse<CaseDetailDto>.Ok(result.Value));
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] SearchCasesRequest request,
        CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var result = await _searchHandler.HandleAsync(
            tenantId, request.Q, request.Status, request.From, request.To,
            request.Page, request.PageSize, ct);

        if (result.IsFailure)
            return StatusCode(500, ApiResponse<SearchCasesResultDto>.Fail(
                result.Error.Code, "An internal error occurred"));

        return Ok(ApiResponse<SearchCasesResultDto>.Ok(result.Value));
    }
}
