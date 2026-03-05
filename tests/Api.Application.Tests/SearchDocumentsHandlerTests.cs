using Api.Application.Queries;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class SearchDocumentsHandlerTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();
    private readonly StubDocumentRepository _repository = new();
    private readonly SearchDocumentsHandler _handler;

    public SearchDocumentsHandlerTests()
    {
        _handler = new SearchDocumentsHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_ReturnsMatchingDocuments()
    {
        var doc = CreateDocument("report.pdf", DocumentStatus.Submitted);
        _repository.SearchResult = Result<(IReadOnlyList<IntakeDocument>, int)>.Success(
            (new List<IntakeDocument> { doc }, 1));

        var result = await _handler.HandleAsync(TenantGuid, "report", null, null, null, null, 1, 10);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Equal(1, result.Value.Page);
        Assert.Equal(10, result.Value.PageSize);
    }

    [Fact]
    public async Task HandleAsync_EmptyResults_ReturnsEmptyList()
    {
        _repository.SearchResult = Result<(IReadOnlyList<IntakeDocument>, int)>.Success(
            (new List<IntakeDocument>(), 0));

        var result = await _handler.HandleAsync(TenantGuid, null, null, null, null, null, 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0, result.Value.TotalCount);
    }

    [Fact]
    public async Task HandleAsync_ClampsPageAndPageSize()
    {
        _repository.SearchResult = Result<(IReadOnlyList<IntakeDocument>, int)>.Success(
            (new List<IntakeDocument>(), 0));

        var result = await _handler.HandleAsync(TenantGuid, null, null, null, null, null, -5, 999);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Page);
        Assert.Equal(100, result.Value.PageSize);
    }

    [Fact]
    public async Task HandleAsync_ParsesStatusFilter()
    {
        _repository.SearchResult = Result<(IReadOnlyList<IntakeDocument>, int)>.Success(
            (new List<IntakeDocument>(), 0));

        await _handler.HandleAsync(TenantGuid, null, "PendingReview", null, null, null, 1, 10);

        Assert.Equal(DocumentStatus.PendingReview, _repository.LastStatusFilter);
    }

    [Fact]
    public async Task HandleAsync_InvalidStatus_PassesNull()
    {
        _repository.SearchResult = Result<(IReadOnlyList<IntakeDocument>, int)>.Success(
            (new List<IntakeDocument>(), 0));

        await _handler.HandleAsync(TenantGuid, null, "InvalidStatus", null, null, null, 1, 10);

        Assert.Null(_repository.LastStatusFilter);
    }

    [Fact]
    public async Task HandleAsync_RepoFailure_ReturnsFailure()
    {
        _repository.SearchResult = Result<(IReadOnlyList<IntakeDocument>, int)>.Failure(
            new Error("DB_ERROR", "connection lost"));

        var result = await _handler.HandleAsync(TenantGuid, null, null, null, null, null, 1, 10);

        Assert.True(result.IsFailure);
        Assert.Equal("DB_ERROR", result.Error.Code);
    }

    private static IntakeDocument CreateDocument(string fileName, DocumentStatus status)
    {
        var doc = IntakeDocument.Submit(new TenantId(TenantGuid), fileName, $"storage/{fileName}");
        if (status >= DocumentStatus.Processing) doc.MarkProcessing();
        if (status >= DocumentStatus.Completed) doc.MarkCompleted();
        if (status >= DocumentStatus.PendingReview) doc.MarkPendingReview();
        return doc;
    }

    private sealed class StubDocumentRepository : IDocumentRepository
    {
        public Result<(IReadOnlyList<IntakeDocument>, int)> SearchResult { get; set; } =
            Result<(IReadOnlyList<IntakeDocument>, int)>.Success((new List<IntakeDocument>(), 0));

        public DocumentStatus? LastStatusFilter { get; private set; }

        public Task<Result<(IReadOnlyList<IntakeDocument> Items, int TotalCount)>> SearchAsync(
            TenantId tenantId, string? fileNameContains, DocumentStatus? status,
            DateTimeOffset? submittedAfter, DateTimeOffset? submittedBefore,
            string? extractedFieldContains, int page, int pageSize, CancellationToken ct = default)
        {
            LastStatusFilter = status;
            return Task.FromResult(SearchResult);
        }

        public Task<Result<IntakeDocument?>> FindByIdAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<IntakeDocument?>> FindByIdUnfilteredAsync(DocumentId id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusAsync(TenantId tenantId, DocumentStatus status, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusesAsync(TenantId tenantId, IReadOnlyList<DocumentStatus> statuses, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Unit>> SaveAsync(IntakeDocument document, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Unit>> UpdateAsync(IntakeDocument document, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Unit>> DeleteAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
