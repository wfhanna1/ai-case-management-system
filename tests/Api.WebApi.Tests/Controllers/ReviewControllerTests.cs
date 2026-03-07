using System.Security.Claims;
using Api.Application.Commands;
using Api.Application.DTOs;
using Api.Application.Queries;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Api.WebApi;
using Api.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Api.WebApi.Tests.Controllers;

public sealed class ReviewControllerTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();
    private static readonly Guid UserGuid = Guid.NewGuid();

    // -----------------------------------------------------------------------
    // Missing user ID returns 401 (pre-handler validation, no handler invoked)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task StartReview_missing_user_id_returns_401()
    {
        var controller = CreateControllerWithNullHandlers();
        SetNoUserClaims(controller);

        var result = await controller.StartReview(Guid.NewGuid(), CancellationToken.None);

        var objectResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        Assert.Equal("MISSING_USER", response.Error!.Code);
    }

    [Fact]
    public async Task CorrectField_missing_user_id_returns_401()
    {
        var controller = CreateControllerWithNullHandlers();
        SetNoUserClaims(controller);

        var request = new CorrectFieldRequest("SomeField", "SomeValue");
        var result = await controller.CorrectField(Guid.NewGuid(), request, CancellationToken.None);

        var objectResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        Assert.Equal("MISSING_USER", response.Error!.Code);
    }

    [Fact]
    public async Task Finalize_missing_user_id_returns_401()
    {
        var controller = CreateControllerWithNullHandlers();
        SetNoUserClaims(controller);

        var result = await controller.Finalize(Guid.NewGuid(), CancellationToken.None);

        var objectResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        Assert.Equal("MISSING_USER", response.Error!.Code);
    }

    // -----------------------------------------------------------------------
    // ListPending tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ListPending_success_returns_200_with_documents()
    {
        var doc = CreatePendingReviewDocument();
        var stubRepo = new StubDocumentRepository();
        stubRepo.SetListByStatusesResult(new[] { doc });

        var controller = CreateController(documentRepo: stubRepo);

        var result = await controller.ListPending(1, 20, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<ReviewDocumentDto>>>(okResult.Value);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data);
        Assert.Equal(doc.OriginalFileName, response.Data[0].OriginalFileName);
    }

    [Fact]
    public async Task ListPending_handler_failure_returns_500()
    {
        var stubRepo = new StubDocumentRepository();
        stubRepo.SetListByStatusesFailure(new Error("DB_ERROR", "Connection failed"));

        var controller = CreateController(documentRepo: stubRepo);

        var result = await controller.ListPending(1, 20, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<ReviewDocumentDto>>>(statusResult.Value);
        Assert.Equal("DB_ERROR", response.Error!.Code);
    }

    // -----------------------------------------------------------------------
    // GetReview tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetReview_success_returns_200_with_document()
    {
        var doc = CreatePendingReviewDocument();
        var stubRepo = new StubDocumentRepository();
        stubRepo.SetFindByIdResult(doc);

        var controller = CreateController(documentRepo: stubRepo);

        var result = await controller.GetReview(doc.Id.Value, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<ReviewDocumentDto>>(okResult.Value);
        Assert.NotNull(response.Data);
        Assert.Equal(doc.OriginalFileName, response.Data.OriginalFileName);
    }

    [Fact]
    public async Task GetReview_not_found_returns_404()
    {
        var stubRepo = new StubDocumentRepository();
        stubRepo.SetFindByIdResult(null);

        var controller = CreateController(documentRepo: stubRepo);

        var result = await controller.GetReview(Guid.NewGuid(), CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var response = Assert.IsType<ApiResponse<ReviewDocumentDto>>(notFoundResult.Value);
        Assert.Equal("NOT_FOUND", response.Error!.Code);
    }

    [Fact]
    public async Task GetReview_handler_failure_returns_500()
    {
        var stubRepo = new StubDocumentRepository();
        stubRepo.SetFindByIdFailure(new Error("DB_ERROR", "Connection failed"));

        var controller = CreateController(documentRepo: stubRepo);

        var result = await controller.GetReview(Guid.NewGuid(), CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // -----------------------------------------------------------------------
    // StartReview tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task StartReview_success_returns_200()
    {
        var doc = CreatePendingReviewDocument();
        var stubRepo = new StubDocumentRepository();
        stubRepo.SetFindByIdResult(doc);

        var controller = CreateController(documentRepo: stubRepo);
        SetUserClaims(controller);

        var result = await controller.StartReview(doc.Id.Value, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.NotNull(response.Data);
        Assert.Null(response.Error);
    }

    [Fact]
    public async Task StartReview_not_found_returns_404()
    {
        var stubRepo = new StubDocumentRepository();
        stubRepo.SetFindByIdResult(null);

        var controller = CreateController(documentRepo: stubRepo);
        SetUserClaims(controller);

        var result = await controller.StartReview(Guid.NewGuid(), CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
        Assert.Equal("NOT_FOUND", response.Error!.Code);
    }

    [Fact]
    public async Task StartReview_invalid_transition_returns_409()
    {
        // Create a document in Submitted status (not PendingReview), so StartReview will fail
        var doc = IntakeDocument.Submit(new TenantId(TenantGuid), "test.pdf", "key/test.pdf");

        var stubRepo = new StubDocumentRepository();
        stubRepo.SetFindByIdResult(doc);

        var controller = CreateController(documentRepo: stubRepo);
        SetUserClaims(controller);

        var result = await controller.StartReview(doc.Id.Value, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(conflictResult.Value);
        Assert.Equal("INVALID_TRANSITION", response.Error!.Code);
    }

    // -----------------------------------------------------------------------
    // CorrectField tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CorrectField_success_returns_200()
    {
        var doc = CreateInReviewDocumentWithFields();
        var stubRepo = new StubDocumentRepository();
        stubRepo.SetFindByIdResult(doc);

        var controller = CreateController(documentRepo: stubRepo);
        SetUserClaims(controller);

        var request = new CorrectFieldRequest("FullName", "Corrected Name");
        var result = await controller.CorrectField(doc.Id.Value, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.NotNull(response.Data);
        Assert.Null(response.Error);
    }

    [Fact]
    public async Task CorrectField_not_found_returns_404()
    {
        var stubRepo = new StubDocumentRepository();
        stubRepo.SetFindByIdResult(null);

        var controller = CreateController(documentRepo: stubRepo);
        SetUserClaims(controller);

        var request = new CorrectFieldRequest("FullName", "Corrected Name");
        var result = await controller.CorrectField(Guid.NewGuid(), request, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
        Assert.Equal("NOT_FOUND", response.Error!.Code);
    }

    [Fact]
    public async Task CorrectField_field_not_found_returns_404()
    {
        var doc = CreateInReviewDocumentWithFields();
        var stubRepo = new StubDocumentRepository();
        stubRepo.SetFindByIdResult(doc);

        var controller = CreateController(documentRepo: stubRepo);
        SetUserClaims(controller);

        var request = new CorrectFieldRequest("NonExistentField", "SomeValue");
        var result = await controller.CorrectField(doc.Id.Value, request, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
        Assert.Equal("FIELD_NOT_FOUND", response.Error!.Code);
    }

    [Fact]
    public async Task CorrectField_invalid_transition_returns_409()
    {
        // Document in PendingReview status -- not InReview, so CorrectField should fail
        var doc = CreatePendingReviewDocument();
        var stubRepo = new StubDocumentRepository();
        stubRepo.SetFindByIdResult(doc);

        var controller = CreateController(documentRepo: stubRepo);
        SetUserClaims(controller);

        var request = new CorrectFieldRequest("FullName", "Corrected");
        var result = await controller.CorrectField(doc.Id.Value, request, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(conflictResult.Value);
        Assert.Equal("INVALID_TRANSITION", response.Error!.Code);
    }

    // -----------------------------------------------------------------------
    // Finalize tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Finalize_success_returns_200()
    {
        var doc = CreateInReviewDocument();
        var stubRepo = new StubDocumentRepository();
        stubRepo.SetFindByIdResult(doc);

        var controller = CreateController(documentRepo: stubRepo);
        SetUserClaims(controller);

        var result = await controller.Finalize(doc.Id.Value, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.NotNull(response.Data);
        Assert.Null(response.Error);
    }

    [Fact]
    public async Task Finalize_not_found_returns_404()
    {
        var stubRepo = new StubDocumentRepository();
        stubRepo.SetFindByIdResult(null);

        var controller = CreateController(documentRepo: stubRepo);
        SetUserClaims(controller);

        var result = await controller.Finalize(Guid.NewGuid(), CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
        Assert.Equal("NOT_FOUND", response.Error!.Code);
    }

    [Fact]
    public async Task Finalize_invalid_transition_returns_409()
    {
        // Finalized documents cannot be finalized again
        var doc = CreateFinalizedDocument();
        var stubRepo = new StubDocumentRepository();
        stubRepo.SetFindByIdResult(doc);

        var controller = CreateController(documentRepo: stubRepo);
        SetUserClaims(controller);

        var result = await controller.Finalize(doc.Id.Value, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(conflictResult.Value);
        Assert.Equal("INVALID_TRANSITION", response.Error!.Code);
    }

    [Fact]
    public async Task Finalize_from_pending_review_auto_starts_and_succeeds()
    {
        var doc = CreatePendingReviewDocument();
        var stubRepo = new StubDocumentRepository();
        stubRepo.SetFindByIdResult(doc);

        var controller = CreateController(documentRepo: stubRepo);
        SetUserClaims(controller);

        var result = await controller.Finalize(doc.Id.Value, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.Null(response.Error);
    }

    // -----------------------------------------------------------------------
    // GetSimilarCases tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSimilarCases_success_returns_200()
    {
        var doc = CreatePendingReviewDocument();
        var docRepo = new StubDocumentRepository();
        docRepo.SetFindByIdResult(doc);

        var stubRagClient = new StubRagServiceClient();
        stubRagClient.SetResult(new List<SimilarDocumentResult>
        {
            new(Guid.NewGuid(), 0.95, new Dictionary<string, string> { ["Name"] = "John" })
        });
        var stubSummaryPort = new StubSummaryPort();
        stubSummaryPort.SetResult("Test summary");

        var controller = CreateController(documentRepo: docRepo, ragClient: stubRagClient, summaryPort: stubSummaryPort);

        var result = await controller.GetSimilarCases(doc.Id.Value, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<SimilarCasesResultDto>>(okResult.Value);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data.Items);
        Assert.Equal(0.95, response.Data.Items[0].Score);
    }

    [Fact]
    public async Task GetSimilarCases_handler_failure_returns_500()
    {
        var doc = CreatePendingReviewDocument();
        var docRepo = new StubDocumentRepository();
        docRepo.SetFindByIdResult(doc);

        var stubRagClient = new StubRagServiceClient();
        stubRagClient.SetFailure(new Error("RAG_ERROR", "Service unavailable"));

        var controller = CreateController(documentRepo: docRepo, ragClient: stubRagClient);

        var result = await controller.GetSimilarCases(doc.Id.Value, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var response = Assert.IsType<ApiResponse<SimilarCasesResultDto>>(statusResult.Value);
        Assert.Equal("RAG_ERROR", response.Error!.Code);
    }

    // -----------------------------------------------------------------------
    // GetAuditTrail tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAuditTrail_success_returns_200()
    {
        var tenantId = new TenantId(TenantGuid);
        var docId = DocumentId.New();
        var reviewerId = new UserId(UserGuid);

        var entry = AuditLogEntry.RecordReviewStarted(tenantId, docId, reviewerId);

        var stubAuditRepo = new StubAuditLogRepository();
        stubAuditRepo.SetListByDocumentResult(new[] { entry });

        var controller = CreateController(auditRepo: stubAuditRepo);

        var result = await controller.GetAuditTrail(docId.Value, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<AuditLogEntryDto>>>(okResult.Value);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data);
        Assert.Equal("ReviewStarted", response.Data[0].Action);
    }

    [Fact]
    public async Task GetAuditTrail_handler_failure_returns_500()
    {
        var stubAuditRepo = new StubAuditLogRepository();
        stubAuditRepo.SetListByDocumentFailure(new Error("DB_ERROR", "Connection failed"));

        var controller = CreateController(auditRepo: stubAuditRepo);

        var result = await controller.GetAuditTrail(Guid.NewGuid(), CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Helpers: controller creation
    // -----------------------------------------------------------------------

    private static ReviewController CreateControllerWithNullHandlers()
    {
        var tenantContext = new StubTenantContext(TenantGuid);
        return new ReviewController(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            tenantContext);
    }

    private static ReviewController CreateController(
        StubDocumentRepository? documentRepo = null,
        StubAuditLogRepository? auditRepo = null,
        StubRagServiceClient? ragClient = null,
        StubSummaryPort? summaryPort = null)
    {
        var docRepo = documentRepo ?? new StubDocumentRepository();
        var audRepo = auditRepo ?? new StubAuditLogRepository();
        var rag = ragClient ?? new StubRagServiceClient();
        var summary = summaryPort ?? new StubSummaryPort();
        var tenantContext = new StubTenantContext(TenantGuid);

        var listPendingHandler = new ListPendingReviewHandler(docRepo);
        var getReviewHandler = new GetDocumentReviewHandler(docRepo);
        var startReviewHandler = new StartReviewHandler(docRepo, audRepo, NullLogger<StartReviewHandler>.Instance);
        var correctFieldHandler = new CorrectFieldHandler(docRepo, audRepo, NullLogger<CorrectFieldHandler>.Instance);
        var finalizeReviewHandler = new FinalizeReviewHandler(docRepo, audRepo, NullLogger<FinalizeReviewHandler>.Instance);
        var getAuditTrailHandler = new GetAuditTrailHandler(audRepo);
        var similarCasesHandler = new GetSimilarCasesHandler(rag, summary, docRepo);

        return new ReviewController(
            listPendingHandler,
            getReviewHandler,
            startReviewHandler,
            correctFieldHandler,
            finalizeReviewHandler,
            getAuditTrailHandler,
            similarCasesHandler,
            tenantContext);
    }

    // -----------------------------------------------------------------------
    // Helpers: claims setup
    // -----------------------------------------------------------------------

    private static void SetUserClaims(ControllerBase controller)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, UserGuid.ToString())
                }, "test"))
            }
        };
    }

    private static void SetNoUserClaims(ControllerBase controller)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };
    }

    // -----------------------------------------------------------------------
    // Helpers: document factory methods
    // -----------------------------------------------------------------------

    private static IntakeDocument CreatePendingReviewDocument()
    {
        var doc = IntakeDocument.Submit(new TenantId(TenantGuid), "test.pdf", "key/test.pdf");
        doc.MarkProcessing();
        doc.MarkCompleted(new List<ExtractedField>
        {
            new("FullName", "John Doe", 0.95)
        });
        doc.MarkPendingReview();
        return doc;
    }

    private static IntakeDocument CreateInReviewDocument()
    {
        var doc = CreatePendingReviewDocument();
        doc.StartReview(new UserId(UserGuid));
        return doc;
    }

    private static IntakeDocument CreateInReviewDocumentWithFields()
    {
        var doc = IntakeDocument.Submit(new TenantId(TenantGuid), "test.pdf", "key/test.pdf");
        doc.MarkProcessing();
        doc.MarkCompleted(new List<ExtractedField>
        {
            new("FullName", "John Doe", 0.95),
            new("DateOfBirth", "1990-01-01", 0.88)
        });
        doc.MarkPendingReview();
        doc.StartReview(new UserId(UserGuid));
        return doc;
    }

    private static IntakeDocument CreateFinalizedDocument()
    {
        var doc = CreateInReviewDocument();
        doc.Finalize(new UserId(UserGuid));
        return doc;
    }

    // -----------------------------------------------------------------------
    // Stub test doubles
    // -----------------------------------------------------------------------

    private sealed class StubTenantContext : ITenantContext
    {
        public TenantId? TenantId { get; }
        public StubTenantContext(Guid id) => TenantId = new TenantId(id);
    }

    private sealed class StubDocumentRepository : IDocumentRepository
    {
        private Result<IntakeDocument?>? _findByIdResult;
        private Result<IReadOnlyList<IntakeDocument>>? _listByStatusesResult;

        public void SetFindByIdResult(IntakeDocument? doc)
            => _findByIdResult = Result<IntakeDocument?>.Success(doc);

        public void SetFindByIdFailure(Error error)
            => _findByIdResult = Result<IntakeDocument?>.Failure(error);

        public void SetListByStatusesResult(IReadOnlyList<IntakeDocument> docs)
            => _listByStatusesResult = Result<IReadOnlyList<IntakeDocument>>.Success(docs);

        public void SetListByStatusesFailure(Error error)
            => _listByStatusesResult = Result<IReadOnlyList<IntakeDocument>>.Failure(error);

        public Task<Result<IntakeDocument?>> FindByIdAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(_findByIdResult ?? Result<IntakeDocument?>.Success(null));

        public Task<Result<IntakeDocument?>> FindByIdUnfilteredAsync(DocumentId id, CancellationToken ct = default)
            => Task.FromResult(_findByIdResult ?? Result<IntakeDocument?>.Success(null));

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(Result<IReadOnlyList<IntakeDocument>>.Success(Array.Empty<IntakeDocument>() as IReadOnlyList<IntakeDocument>));

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusAsync(TenantId tenantId, DocumentStatus status, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(Result<IReadOnlyList<IntakeDocument>>.Success(Array.Empty<IntakeDocument>() as IReadOnlyList<IntakeDocument>));

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusesAsync(TenantId tenantId, IReadOnlyList<DocumentStatus> statuses, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(_listByStatusesResult ?? Result<IReadOnlyList<IntakeDocument>>.Success(Array.Empty<IntakeDocument>() as IReadOnlyList<IntakeDocument>));

        public Task<Result<Unit>> SaveAsync(IntakeDocument document, CancellationToken ct = default)
            => Task.FromResult(Result<Unit>.Success(Unit.Value));

        public Task<Result<Unit>> UpdateAsync(IntakeDocument document, CancellationToken ct = default)
            => Task.FromResult(Result<Unit>.Success(Unit.Value));

        public Task<Result<Unit>> DeleteAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(Result<Unit>.Success(Unit.Value));

        public Task<Result<(IReadOnlyList<IntakeDocument> Items, int TotalCount)>> SearchAsync(
            TenantId tenantId, string? fileNameContains, DocumentStatus? status,
            DateTimeOffset? submittedAfter, DateTimeOffset? submittedBefore,
            string? extractedFieldContains, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(Result<(IReadOnlyList<IntakeDocument> Items, int TotalCount)>.Success(
                (Array.Empty<IntakeDocument>() as IReadOnlyList<IntakeDocument>, 0)));

        public Task<Result<(int PendingReview, int ProcessedToday, TimeSpan AverageProcessingTime)>> GetStatsAsync(
            TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(Result<(int, int, TimeSpan)>.Success((0, 0, TimeSpan.Zero)));
    }

    private sealed class StubAuditLogRepository : IAuditLogRepository
    {
        private Result<IReadOnlyList<AuditLogEntry>>? _listByDocumentResult;

        public void SetListByDocumentResult(IReadOnlyList<AuditLogEntry> entries)
            => _listByDocumentResult = Result<IReadOnlyList<AuditLogEntry>>.Success(entries);

        public void SetListByDocumentFailure(Error error)
            => _listByDocumentResult = Result<IReadOnlyList<AuditLogEntry>>.Failure(error);

        public Task<Result<Unit>> SaveAsync(AuditLogEntry entry, CancellationToken ct = default)
            => Task.FromResult(Result<Unit>.Success(Unit.Value));

        public Task<Result<IReadOnlyList<AuditLogEntry>>> ListByDocumentAsync(DocumentId documentId, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(_listByDocumentResult ?? Result<IReadOnlyList<AuditLogEntry>>.Success(
                Array.Empty<AuditLogEntry>() as IReadOnlyList<AuditLogEntry>));
    }

    private sealed class StubRagServiceClient : IRagServiceClient
    {
        private Result<IReadOnlyList<SimilarDocumentResult>>? _result;

        public void SetResult(IReadOnlyList<SimilarDocumentResult> results)
            => _result = Result<IReadOnlyList<SimilarDocumentResult>>.Success(results);

        public void SetFailure(Error error)
            => _result = Result<IReadOnlyList<SimilarDocumentResult>>.Failure(error);

        public Task<Result<IReadOnlyList<SimilarDocumentResult>>> FindSimilarByTextAsync(
            string textContent, Guid tenantId, int topK = 5, CancellationToken ct = default)
            => Task.FromResult(_result ?? Result<IReadOnlyList<SimilarDocumentResult>>.Success(
                Array.Empty<SimilarDocumentResult>() as IReadOnlyList<SimilarDocumentResult>));
    }

    private sealed class StubSummaryPort : ISummaryPort
    {
        private string _summary = "Default summary";

        public void SetResult(string summary) => _summary = summary;

        public Task<Result<string>> GenerateSummaryAsync(Dictionary<string, string> fields, CancellationToken ct = default)
            => Task.FromResult(Result<string>.Success(_summary));
    }
}
