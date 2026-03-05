using Api.Application.Commands;
using Api.Application.DTOs;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;

namespace Api.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DocumentsController : ControllerBase
{
    private readonly SubmitDocumentHandler _submitHandler;
    private readonly IDocumentRepository _repository;

    public DocumentsController(SubmitDocumentHandler submitHandler, IDocumentRepository repository)
    {
        _submitHandler = submitHandler;
        _repository = repository;
    }

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> Submit(
        [FromForm] Guid tenantId,
        IFormFile file,
        CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var request = new SubmitDocumentRequest(tenantId, file.FileName, file.ContentType);
        var result = await _submitHandler.HandleAsync(stream, request, ct);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error.Code, message = result.Error.Message });

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
        CancellationToken ct)
    {
        var result = await _repository.FindByIdAsync(
            new DocumentId(id), new TenantId(tenantId), ct);

        if (result.IsFailure)
            return StatusCode(500, new { error = result.Error.Code });

        if (result.Value is null)
            return NotFound();

        var doc = result.Value;
        return Ok(new DocumentDto(
            doc.Id.Value, doc.TenantId.Value, doc.OriginalFileName,
            doc.Status.ToString(), doc.SubmittedAt, doc.ProcessedAt));
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _repository.ListByTenantAsync(
            new TenantId(tenantId), page, pageSize, ct);

        if (result.IsFailure)
            return StatusCode(500, new { error = result.Error.Code });

        return Ok(result.Value.Select(doc => new DocumentDto(
            doc.Id.Value, doc.TenantId.Value, doc.OriginalFileName,
            doc.Status.ToString(), doc.SubmittedAt, doc.ProcessedAt)));
    }
}
