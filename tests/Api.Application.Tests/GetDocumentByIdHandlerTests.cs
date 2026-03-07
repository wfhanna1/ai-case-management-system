using Api.Application.Queries;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class GetDocumentByIdHandlerTests
{
    private readonly StubRepository _repository = new();
    private readonly GetDocumentByIdHandler _handler;

    public GetDocumentByIdHandlerTests()
    {
        _handler = new GetDocumentByIdHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_DocumentFound_ReturnsSuccessWithDto()
    {
        var tenantId = TenantId.New();
        var doc = IntakeDocument.Submit(tenantId, "found.pdf", "storage/key");
        _repository.FindResult = Result<IntakeDocument?>.Success(doc);

        var result = await _handler.HandleAsync(doc.Id.Value, tenantId.Value, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("found.pdf", result.Value!.OriginalFileName);
        Assert.Equal("Submitted", result.Value.Status);
    }

    [Fact]
    public async Task HandleAsync_DocumentNotFound_ReturnsSuccessWithNull()
    {
        _repository.FindResult = Result<IntakeDocument?>.Success(null);

        var result = await _handler.HandleAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task HandleAsync_RepositoryFails_ReturnsFailure()
    {
        _repository.FindResult = Result<IntakeDocument?>.Failure(new Error("DB_ERROR", "timeout"));

        var result = await _handler.HandleAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DB_ERROR", result.Error.Code);
    }

    private sealed class StubRepository : IDocumentRepository
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

        public Task<Result<(int PendingReview, int ProcessedToday, TimeSpan AverageProcessingTime)>> GetStatsAsync(
            TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
