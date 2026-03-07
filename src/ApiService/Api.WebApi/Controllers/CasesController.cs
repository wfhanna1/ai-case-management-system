using Api.Application.DTOs;
using Api.Application.Queries;
using Api.WebApi.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;

namespace Api.WebApi.Controllers;

/// <summary>
/// Manages cases. Requires authentication.
/// </summary>
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

    /// <summary>
    /// Lists all cases for the authenticated tenant, paged.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of results per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged list of cases.</returns>
    /// <response code="200">Cases returned successfully.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<SearchCasesResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<SearchCasesResultDto>), StatusCodes.Status500InternalServerError)]
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

    /// <summary>
    /// Gets a single case by its ID.
    /// </summary>
    /// <param name="id">Case identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Case detail.</returns>
    /// <response code="200">Case found and returned.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="404">Case not found.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CaseDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<CaseDetailDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<CaseDetailDto>), StatusCodes.Status500InternalServerError)]
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

    /// <summary>
    /// Searches cases by keyword, status, or date range.
    /// </summary>
    /// <param name="request">Search parameters (query, status, date range, pagination).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Matching cases.</returns>
    /// <response code="200">Search completed successfully.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<SearchCasesResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<SearchCasesResultDto>), StatusCodes.Status500InternalServerError)]
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
