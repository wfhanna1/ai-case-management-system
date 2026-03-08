using System.Security.Claims;
using Api.Application.Commands;
using Api.Application.DTOs;
using Api.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;

namespace Api.WebApi.Controllers;

/// <summary>
/// Manages the document review workflow. Read endpoints are accessible to all authenticated
/// users; mutation endpoints (start, correct-field, finalize) require the Reviewer or Admin role.
/// </summary>
[ApiController]
[Authorize]
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

    /// <summary>
    /// Lists documents pending review for the authenticated tenant, paged.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of results per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Documents awaiting review.</returns>
    /// <response code="200">List returned successfully.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Caller does not have the Reviewer or Admin role.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("pending")]
    [Authorize(Policy = "RequireReviewer")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ReviewDocumentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ReviewDocumentDto>>), StatusCodes.Status500InternalServerError)]
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

    /// <summary>
    /// Gets the review detail for a specific document, including extracted fields.
    /// Accessible to all authenticated users.
    /// </summary>
    /// <param name="documentId">Document identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Review document with extracted field data.</returns>
    /// <response code="200">Review detail returned.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="404">Document not found.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("{documentId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ReviewDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<ReviewDocumentDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ReviewDocumentDto>), StatusCodes.Status500InternalServerError)]
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

    /// <summary>
    /// Transitions a document into the InReview state, assigning it to the current reviewer.
    /// </summary>
    /// <param name="documentId">Document identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Empty success response.</returns>
    /// <response code="200">Review started successfully.</response>
    /// <response code="401">Not authenticated or user identity missing from token.</response>
    /// <response code="403">Caller does not have the Reviewer or Admin role.</response>
    /// <response code="404">Document not found.</response>
    /// <response code="409">Document is not in a state that allows starting review.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpPost("{documentId:guid}/start")]
    [Authorize(Policy = "RequireReviewer")]
    [ProducesResponseType(typeof(ApiResponse<EmptyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EmptyResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<EmptyResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<EmptyResponse>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<EmptyResponse>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> StartReview(Guid documentId, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null)
            return Unauthorized(ApiResponse<EmptyResponse>.Fail("MISSING_USER", "User identity is missing from token."));

        var result = await _startReviewHandler.HandleAsync(documentId, tenantId, reviewerId.Value, ct);

        if (result.IsFailure)
        {
            return result.Error.Code switch
            {
                "NOT_FOUND" => NotFound(ApiResponse<EmptyResponse>.Fail(result.Error.Code, result.Error.Message)),
                "INVALID_TRANSITION" => Conflict(ApiResponse<EmptyResponse>.Fail(result.Error.Code, result.Error.Message)),
                _ => StatusCode(500, ApiResponse<EmptyResponse>.Fail(result.Error.Code, "An internal error occurred"))
            };
        }

        return Ok(ApiResponse<EmptyResponse>.Ok(new EmptyResponse()));
    }

    /// <summary>
    /// Corrects the value of an extracted field on a document that is under review.
    /// </summary>
    /// <param name="documentId">Document identifier.</param>
    /// <param name="request">Field name and corrected value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Empty success response.</returns>
    /// <response code="200">Field corrected successfully.</response>
    /// <response code="401">Not authenticated or user identity missing from token.</response>
    /// <response code="403">Caller does not have the Reviewer or Admin role.</response>
    /// <response code="404">Document or field not found.</response>
    /// <response code="409">Document is not in a state that allows field correction.</response>
    /// <response code="422">Request body failed validation.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpPost("{documentId:guid}/correct-field")]
    [Authorize(Policy = "RequireReviewer")]
    [ProducesResponseType(typeof(ApiResponse<EmptyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EmptyResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<EmptyResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<EmptyResponse>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<EmptyResponse>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<EmptyResponse>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CorrectField(
        Guid documentId,
        [FromBody] CorrectFieldRequest request,
        CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null)
            return Unauthorized(ApiResponse<EmptyResponse>.Fail("MISSING_USER", "User identity is missing from token."));

        var result = await _correctFieldHandler.HandleAsync(
            documentId, tenantId, reviewerId.Value, request.FieldName, request.NewValue, ct);

        if (result.IsFailure)
        {
            return result.Error.Code switch
            {
                "NOT_FOUND" => NotFound(ApiResponse<EmptyResponse>.Fail(result.Error.Code, result.Error.Message)),
                "FIELD_NOT_FOUND" => NotFound(ApiResponse<EmptyResponse>.Fail(result.Error.Code, result.Error.Message)),
                "INVALID_TRANSITION" => Conflict(ApiResponse<EmptyResponse>.Fail(result.Error.Code, result.Error.Message)),
                _ => StatusCode(500, ApiResponse<EmptyResponse>.Fail(result.Error.Code, "An internal error occurred"))
            };
        }

        return Ok(ApiResponse<EmptyResponse>.Ok(new EmptyResponse()));
    }

    /// <summary>
    /// Finalizes a document review, transitioning the document to the Reviewed state.
    /// </summary>
    /// <param name="documentId">Document identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Empty success response.</returns>
    /// <response code="200">Review finalized successfully.</response>
    /// <response code="401">Not authenticated or user identity missing from token.</response>
    /// <response code="403">Caller does not have the Reviewer or Admin role.</response>
    /// <response code="404">Document not found.</response>
    /// <response code="409">Document is not in a state that allows finalization.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpPost("{documentId:guid}/finalize")]
    [Authorize(Policy = "RequireReviewer")]
    [ProducesResponseType(typeof(ApiResponse<EmptyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EmptyResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<EmptyResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<EmptyResponse>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<EmptyResponse>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Finalize(Guid documentId, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null)
            return Unauthorized(ApiResponse<EmptyResponse>.Fail("MISSING_USER", "User identity is missing from token."));

        var result = await _finalizeReviewHandler.HandleAsync(documentId, tenantId, reviewerId.Value, ct);

        if (result.IsFailure)
        {
            return result.Error.Code switch
            {
                "NOT_FOUND" => NotFound(ApiResponse<EmptyResponse>.Fail(result.Error.Code, result.Error.Message)),
                "INVALID_TRANSITION" => Conflict(ApiResponse<EmptyResponse>.Fail(result.Error.Code, result.Error.Message)),
                _ => StatusCode(500, ApiResponse<EmptyResponse>.Fail(result.Error.Code, "An internal error occurred"))
            };
        }

        return Ok(ApiResponse<EmptyResponse>.Ok(new EmptyResponse()));
    }

    /// <summary>
    /// Returns the top 5 cases semantically similar to the given document, based on extracted field content.
    /// Accessible to all authenticated users.
    /// </summary>
    /// <param name="documentId">Document identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of similar cases with similarity scores.</returns>
    /// <response code="200">Similar cases returned.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="500">Failed to retrieve similar cases.</response>
    [HttpGet("{documentId:guid}/similar-cases")]
    [ProducesResponseType(typeof(ApiResponse<SimilarCasesResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<SimilarCasesResultDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSimilarCases(Guid documentId, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var result = await _similarCasesHandler.HandleAsync(documentId, tenantId, 5, ct);

        if (result.IsFailure)
            return StatusCode(500, ApiResponse<SimilarCasesResultDto>.Fail(
                result.Error.Code, "Failed to retrieve similar cases"));

        return Ok(ApiResponse<SimilarCasesResultDto>.Ok(result.Value));
    }

    /// <summary>
    /// Returns the full audit trail for a document (all state transitions and field corrections).
    /// Accessible to all authenticated users.
    /// </summary>
    /// <param name="documentId">Document identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Ordered list of audit log entries.</returns>
    /// <response code="200">Audit trail returned.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("{documentId:guid}/audit")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AuditLogEntryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AuditLogEntryDto>>), StatusCodes.Status500InternalServerError)]
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
