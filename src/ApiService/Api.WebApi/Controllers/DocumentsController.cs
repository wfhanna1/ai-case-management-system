using Api.Application.Commands;
using Api.Application.DTOs;
using Api.Application.Queries;
using Api.WebApi.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Api.WebApi;
using SharedKernel;

namespace Api.WebApi.Controllers;

/// <summary>
/// Manages intake document submission and retrieval. Requires authentication.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class DocumentsController : ControllerBase
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/png", "image/jpeg", "image/tiff"
    };

    private readonly SubmitDocumentHandler _submitHandler;
    private readonly GetDocumentByIdHandler _getByIdHandler;
    private readonly ListDocumentsByTenantHandler _listHandler;
    private readonly SearchDocumentsHandler _searchHandler;
    private readonly GetDashboardStatsHandler _statsHandler;
    private readonly ITenantContext _tenantContext;

    public DocumentsController(
        SubmitDocumentHandler submitHandler,
        GetDocumentByIdHandler getByIdHandler,
        ListDocumentsByTenantHandler listHandler,
        SearchDocumentsHandler searchHandler,
        GetDashboardStatsHandler statsHandler,
        ITenantContext tenantContext)
    {
        _submitHandler = submitHandler;
        _getByIdHandler = getByIdHandler;
        _listHandler = listHandler;
        _searchHandler = searchHandler;
        _statsHandler = statsHandler;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Uploads a new intake document for processing. Requires IntakeWorker or Admin role.
    /// Accepted file types: PDF, PNG, JPEG, TIFF. Maximum size: 10 MB.
    /// </summary>
    /// <param name="file">The document file to upload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created document record.</returns>
    /// <response code="201">Document uploaded successfully.</response>
    /// <response code="400">File is missing, empty, or has an unsupported content type.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Caller does not have the IntakeWorker or Admin role.</response>
    [HttpPost]
    [Authorize(Policy = "RequireIntakeWorker")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    [ProducesResponseType(typeof(ApiResponse<DocumentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<DocumentDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Submit(
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null)
            return BadRequest(ApiResponse<DocumentDto>.Fail(
                "MISSING_FILE", "A file is required."));

        if (file.Length == 0)
            return BadRequest(ApiResponse<DocumentDto>.Fail(
                "EMPTY_FILE", "The uploaded file is empty."));

        if (!AllowedContentTypes.Contains(file.ContentType))
            return BadRequest(ApiResponse<DocumentDto>.Fail(
                "INVALID_FILE_TYPE", "Permitted types: PDF, PNG, JPEG, TIFF"));

        var tenantId = _tenantContext.TenantId!.Value;
        await using var stream = file.OpenReadStream();
        var request = new SubmitDocumentRequest(tenantId, file.FileName, file.ContentType);
        var result = await _submitHandler.HandleAsync(stream, request, ct);

        if (result.IsFailure)
            return BadRequest(ApiResponse<DocumentDto>.Fail(result.Error.Code, result.Error.Message));

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id },
            ApiResponse<DocumentDto>.Ok(result.Value));
    }

    /// <summary>
    /// Gets a single document by its ID.
    /// </summary>
    /// <param name="id">Document identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Document detail.</returns>
    /// <response code="200">Document found and returned.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="404">Document not found.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<DocumentDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<DocumentDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var result = await _getByIdHandler.HandleAsync(id, tenantId, ct);

        if (result.IsFailure)
            return StatusCode(500, ApiResponse<DocumentDto>.Fail(result.Error.Code, "An internal error occurred"));

        if (result.Value is null)
            return NotFound(ApiResponse<DocumentDto>.Fail("NOT_FOUND", "Document not found"));

        return Ok(ApiResponse<DocumentDto>.Ok(result.Value));
    }

    /// <summary>
    /// Searches documents by file name, status, date range, or extracted field value.
    /// </summary>
    /// <param name="request">Search parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Matching documents, paged.</returns>
    /// <response code="200">Search completed successfully.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<SearchDocumentsResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<SearchDocumentsResultDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Search(
        [FromQuery] SearchDocumentsRequest request,
        CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var result = await _searchHandler.HandleAsync(
            tenantId, request.FileName, request.Status, request.From, request.To,
            request.FieldValue, request.Page, request.PageSize, ct);

        if (result.IsFailure)
            return StatusCode(500, ApiResponse<SearchDocumentsResultDto>.Fail(
                result.Error.Code, "An internal error occurred"));

        return Ok(ApiResponse<SearchDocumentsResultDto>.Ok(result.Value));
    }

    /// <summary>
    /// Returns dashboard statistics for the authenticated tenant.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Document counts grouped by status.</returns>
    /// <response code="200">Statistics returned successfully.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<DashboardStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<DashboardStatsDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var result = await _statsHandler.HandleAsync(tenantId, ct);

        if (result.IsFailure)
            return StatusCode(500, ApiResponse<DashboardStatsDto>.Fail(
                result.Error.Code, "An internal error occurred"));

        return Ok(ApiResponse<DashboardStatsDto>.Ok(result.Value));
    }

    /// <summary>
    /// Lists all documents for the authenticated tenant, paged.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of results per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged list of documents.</returns>
    /// <response code="200">Documents returned successfully.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DocumentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DocumentDto>>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var result = await _listHandler.HandleAsync(tenantId, page, pageSize, ct);

        if (result.IsFailure)
            return StatusCode(500, ApiResponse<IReadOnlyList<DocumentDto>>.Fail(
                result.Error.Code, "An internal error occurred"));

        return Ok(ApiResponse<IReadOnlyList<DocumentDto>>.Ok(result.Value));
    }
}
