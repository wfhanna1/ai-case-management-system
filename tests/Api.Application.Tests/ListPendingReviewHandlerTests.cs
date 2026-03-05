using Api.Application.Queries;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class ListPendingReviewHandlerTests
{
    private readonly StubDocumentRepository _repository = new();
    private readonly ListPendingReviewHandler _handler;

    public ListPendingReviewHandlerTests()
    {
        _handler = new ListPendingReviewHandler(_repository);
    }

    private static IntakeDocument CreateDocumentWithStatus(TenantId tenantId, DocumentStatus status)
    {
        var doc = IntakeDocument.Submit(tenantId, "test.pdf", "storage/key");
        if (status >= DocumentStatus.Processing) doc.MarkProcessing();
        if (status >= DocumentStatus.Completed) doc.MarkCompleted();
        if (status >= DocumentStatus.PendingReview) doc.MarkPendingReview();
        if (status >= DocumentStatus.InReview) doc.StartReview(UserId.New());
        return doc;
    }

    [Fact]
    public async Task HandleAsync_returns_pending_and_in_review_documents()
    {
        var tenantId = TenantId.New();
        var pendingDoc = CreateDocumentWithStatus(tenantId, DocumentStatus.PendingReview);
        var inReviewDoc = CreateDocumentWithStatus(tenantId, DocumentStatus.InReview);

        _repository.PendingDocs = [pendingDoc];
        _repository.InReviewDocs = [inReviewDoc];

        var result = await _handler.HandleAsync(tenantId.Value, 1, 20, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task HandleAsync_empty_queues_returns_empty_list()
    {
        var tenantId = TenantId.New();
        _repository.PendingDocs = [];
        _repository.InReviewDocs = [];

        var result = await _handler.HandleAsync(tenantId.Value, 1, 20, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task HandleAsync_repository_failure_returns_failure()
    {
        _repository.FailOnPending = true;

        var result = await _handler.HandleAsync(Guid.NewGuid(), 1, 20, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DB_ERROR", result.Error.Code);
    }

    private sealed class StubDocumentRepository : IDocumentRepository
    {
        public IReadOnlyList<IntakeDocument> PendingDocs { get; set; } = [];
        public IReadOnlyList<IntakeDocument> InReviewDocs { get; set; } = [];
        public bool FailOnPending { get; set; }

        public Task<Result<IntakeDocument?>> FindByIdAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IntakeDocument?>> FindByIdUnfilteredAsync(DocumentId id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusAsync(
            TenantId tenantId, DocumentStatus status, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusesAsync(
            TenantId tenantId, IReadOnlyList<DocumentStatus> statuses, int page, int pageSize, CancellationToken ct = default)
        {
            if (FailOnPending)
                return Task.FromResult(Result<IReadOnlyList<IntakeDocument>>.Failure(new Error("DB_ERROR", "timeout")));

            IReadOnlyList<IntakeDocument> docs = PendingDocs.Concat(InReviewDocs).ToList();
            return Task.FromResult(Result<IReadOnlyList<IntakeDocument>>.Success(docs));
        }

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
