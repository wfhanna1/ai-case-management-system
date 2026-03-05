using Api.Application.Commands;
using Api.Application.DTOs;
using Api.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Api.WebApi;
using SharedKernel;

namespace Api.WebApi.Controllers;

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
    private readonly ITenantContext _tenantContext;

    public DocumentsController(
        SubmitDocumentHandler submitHandler,
        GetDocumentByIdHandler getByIdHandler,
        ListDocumentsByTenantHandler listHandler,
        ITenantContext tenantContext)
    {
        _submitHandler = submitHandler;
        _getByIdHandler = getByIdHandler;
        _listHandler = listHandler;
        _tenantContext = tenantContext;
    }

    [HttpPost]
    [Authorize(Policy = "RequireIntakeWorker")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
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

    [HttpGet("{id:guid}")]
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

    [HttpGet]
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
