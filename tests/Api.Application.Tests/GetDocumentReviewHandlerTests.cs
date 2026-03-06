using Api.Application.Queries;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class GetDocumentReviewHandlerTests
{
    private readonly StubDocumentRepository _repository = new();
    private readonly GetDocumentReviewHandler _handler;

    public GetDocumentReviewHandlerTests()
    {
        _handler = new GetDocumentReviewHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_repository_failure_propagates()
    {
        _repository.FindResult = Result<IntakeDocument?>.Failure(new Error("DB_ERROR", "timeout"));

        var result = await _handler.HandleAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DB_ERROR", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_document_not_found_returns_success_with_null()
    {
        _repository.FindResult = Result<IntakeDocument?>.Success(null);

        var result = await _handler.HandleAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task HandleAsync_document_found_returns_mapped_dto()
    {
        var tenantId = TenantId.New();
        var doc = IntakeDocument.Submit(tenantId, "intake-form.pdf", "storage/key");
        doc.MarkProcessing();
        doc.MarkCompleted([new ExtractedField("PatientName", "Jane Doe", 0.95)]);
        _repository.FindResult = Result<IntakeDocument?>.Success(doc);

        var result = await _handler.HandleAsync(doc.Id.Value, tenantId.Value, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(doc.Id.Value, result.Value!.Id);
        Assert.Equal(tenantId.Value, result.Value.TenantId);
        Assert.Equal("intake-form.pdf", result.Value.OriginalFileName);
        Assert.Equal("Completed", result.Value.Status);
        Assert.Single(result.Value.ExtractedFields);
        Assert.Equal("PatientName", result.Value.ExtractedFields[0].Name);
        Assert.Equal("Jane Doe", result.Value.ExtractedFields[0].Value);
        Assert.Equal(0.95, result.Value.ExtractedFields[0].Confidence);
    }

    [Fact]
    public async Task HandleAsync_in_review_document_includes_reviewer_info()
    {
        var tenantId = TenantId.New();
        var reviewerId = UserId.New();
        var doc = IntakeDocument.Submit(tenantId, "report.pdf", "storage/key");
        doc.MarkProcessing();
        doc.MarkCompleted();
        doc.MarkPendingReview();
        doc.StartReview(reviewerId);
        _repository.FindResult = Result<IntakeDocument?>.Success(doc);

        var result = await _handler.HandleAsync(doc.Id.Value, tenantId.Value, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("InReview", result.Value!.Status);
        Assert.Equal(reviewerId.Value, result.Value.ReviewedBy);
    }

    private sealed class StubDocumentRepository : IDocumentRepository
    {
        public Result<IntakeDocument?> FindResult { get; set; } = Result<IntakeDocument?>.Success(null);

        public Task<Result<IntakeDocument?>> FindByIdAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(FindResult);

        public Task<Result<IntakeDocument?>> FindByIdUnfilteredAsync(DocumentId id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusAsync(TenantId tenantId, DocumentStatus status, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusesAsync(TenantId tenantId, IReadOnlyList<DocumentStatus> statuses, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<Unit>> SaveAsync(IntakeDocument document, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<Unit>> UpdateAsync(IntakeDocument document, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<Unit>> DeleteAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<(IReadOnlyList<IntakeDocument> Items, int TotalCount)>> SearchAsync(
            TenantId tenantId, string? fileNameContains, DocumentStatus? status,
            DateTimeOffset? submittedAfter, DateTimeOffset? submittedBefore,
            string? extractedFieldContains, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
