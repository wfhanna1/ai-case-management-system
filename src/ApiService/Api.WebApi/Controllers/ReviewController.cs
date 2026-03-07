using System.Security.Claims;
using Api.Application.Commands;
using Api.Application.DTOs;
using Api.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;

namespace Api.WebApi.Controllers;

[ApiController]
[Authorize(Policy = "RequireReviewer")]
[Route("api/reviews")]
public sealed class ReviewController : ControllerBase
{
    private readonly ListPendingReviewHandler _listPendingHandler;
    private readonly GetDocumentReviewHandler _getReviewHandler;
    private readonly StartReviewHandler _startReviewHandler;
    private readonly CorrectFieldHandler _correctFieldHandler;
    private readonly FinalizeReviewHandler _finalizeReviewHandler;
    private readonly GetAuditTrailHandler _getAuditTrailHandler;
    private readonly GetSimilarCasesHandler _similarCasesHandler;
    private readonly ITenantContext _tenantContext;

    public ReviewController(
        ListPendingReviewHandler listPendingHandler,
        GetDocumentReviewHandler getReviewHandler,
        StartReviewHandler startReviewHandler,
        CorrectFieldHandler correctFieldHandler,
        FinalizeReviewHandler finalizeReviewHandler,
        GetAuditTrailHandler getAuditTrailHandler,
        GetSimilarCasesHandler similarCasesHandler,
        ITenantContext tenantContext)
    {
        _listPendingHandler = listPendingHandler;
        _getReviewHandler = getReviewHandler;
        _startReviewHandler = startReviewHandler;
        _correctFieldHandler = correctFieldHandler;
        _finalizeReviewHandler = finalizeReviewHandler;
        _getAuditTrailHandler = getAuditTrailHandler;
        _similarCasesHandler = similarCasesHandler;
        _tenantContext = tenantContext;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> ListPending(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var result = await _listPendingHandler.HandleAsync(tenantId, page, pageSize, ct);

        if (result.IsFailure)
            return StatusCode(500, ApiResponse<PendingReviewResultDto>.Fail(
                result.Error.Code, "An internal error occurred"));

        return Ok(ApiResponse<PendingReviewResultDto>.Ok(result.Value));
    }

    [HttpGet("{documentId:guid}")]
    public async Task<IActionResult> GetReview(Guid documentId, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var result = await _getReviewHandler.HandleAsync(documentId, tenantId, ct);

        if (result.IsFailure)
            return StatusCode(500, ApiResponse<ReviewDocumentDto>.Fail(
                result.Error.Code, "An internal error occurred"));

        if (result.Value is null)
            return NotFound(ApiResponse<ReviewDocumentDto>.Fail("NOT_FOUND", "Document not found"));

        return Ok(ApiResponse<ReviewDocumentDto>.Ok(result.Value));
    }

    [HttpPost("{documentId:guid}/start")]
    public async Task<IActionResult> StartReview(Guid documentId, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null)
            return Unauthorized(ApiResponse<object>.Fail("MISSING_USER", "User identity is missing from token."));

        var result = await _startReviewHandler.HandleAsync(documentId, tenantId, reviewerId.Value, ct);

        if (result.IsFailure)
        {
            return result.Error.Code switch
            {
                "NOT_FOUND" => NotFound(ApiResponse<object>.Fail(result.Error.Code, result.Error.Message)),
                "INVALID_TRANSITION" => Conflict(ApiResponse<object>.Fail(result.Error.Code, result.Error.Message)),
                _ => StatusCode(500, ApiResponse<object>.Fail(result.Error.Code, "An internal error occurred"))
            };
        }

        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("{documentId:guid}/correct-field")]
    public async Task<IActionResult> CorrectField(
        Guid documentId,
        [FromBody] CorrectFieldRequest request,
        CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null)
            return Unauthorized(ApiResponse<object>.Fail("MISSING_USER", "User identity is missing from token."));

        var result = await _correctFieldHandler.HandleAsync(
            documentId, tenantId, reviewerId.Value, request.FieldName, request.NewValue, ct);

        if (result.IsFailure)
        {
            return result.Error.Code switch
            {
                "NOT_FOUND" => NotFound(ApiResponse<object>.Fail(result.Error.Code, result.Error.Message)),
                "FIELD_NOT_FOUND" => NotFound(ApiResponse<object>.Fail(result.Error.Code, result.Error.Message)),
                "INVALID_TRANSITION" => Conflict(ApiResponse<object>.Fail(result.Error.Code, result.Error.Message)),
                _ => StatusCode(500, ApiResponse<object>.Fail(result.Error.Code, "An internal error occurred"))
            };
        }

        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("{documentId:guid}/finalize")]
    public async Task<IActionResult> Finalize(Guid documentId, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null)
            return Unauthorized(ApiResponse<object>.Fail("MISSING_USER", "User identity is missing from token."));

        var result = await _finalizeReviewHandler.HandleAsync(documentId, tenantId, reviewerId.Value, ct);

        if (result.IsFailure)
        {
            return result.Error.Code switch
            {
                "NOT_FOUND" => NotFound(ApiResponse<object>.Fail(result.Error.Code, result.Error.Message)),
                "INVALID_TRANSITION" => Conflict(ApiResponse<object>.Fail(result.Error.Code, result.Error.Message)),
                _ => StatusCode(500, ApiResponse<object>.Fail(result.Error.Code, "An internal error occurred"))
            };
        }

        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpGet("{documentId:guid}/similar-cases")]
    public async Task<IActionResult> GetSimilarCases(Guid documentId, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var result = await _similarCasesHandler.HandleAsync(documentId, tenantId, 5, ct);

        if (result.IsFailure)
            return StatusCode(500, ApiResponse<SimilarCasesResultDto>.Fail(
                result.Error.Code, "Failed to retrieve similar cases"));

        return Ok(ApiResponse<SimilarCasesResultDto>.Ok(result.Value));
    }

    [HttpGet("{documentId:guid}/audit")]
    public async Task<IActionResult> GetAuditTrail(Guid documentId, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var result = await _getAuditTrailHandler.HandleAsync(documentId, tenantId, ct);

        if (result.IsFailure)
            return StatusCode(500, ApiResponse<IReadOnlyList<AuditLogEntryDto>>.Fail(
                result.Error.Code, "An internal error occurred"));

        return Ok(ApiResponse<IReadOnlyList<AuditLogEntryDto>>.Ok(result.Value));
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (sub is null) return null;
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
