using Api.Application.Queries;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class ListDocumentsByTenantHandlerTests
{
    private readonly StubRepository _repository = new();
    private readonly ListDocumentsByTenantHandler _handler;

    public ListDocumentsByTenantHandlerTests()
    {
        _handler = new ListDocumentsByTenantHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_DocumentsExist_ReturnsMappedDtos()
    {
        var tenantId = TenantId.New();
        var doc = IntakeDocument.Submit(tenantId, "file.pdf", "key/1");
        _repository.ListResult = Result<IReadOnlyList<IntakeDocument>>.Success(new[] { doc });

        var result = await _handler.HandleAsync(tenantId.Value, 1, 20, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("file.pdf", result.Value[0].OriginalFileName);
    }

    [Fact]
    public async Task HandleAsync_EmptyList_ReturnsSuccessWithEmptyList()
    {
        _repository.ListResult = Result<IReadOnlyList<IntakeDocument>>.Success(
            Array.Empty<IntakeDocument>());

        var result = await _handler.HandleAsync(Guid.NewGuid(), 1, 20, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task HandleAsync_RepositoryFails_ReturnsFailure()
    {
        _repository.ListResult = Result<IReadOnlyList<IntakeDocument>>.Failure(
            new Error("DB_ERROR", "connection refused"));

        var result = await _handler.HandleAsync(Guid.NewGuid(), 1, 20, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DB_ERROR", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_PageBelowMinimum_ClampedToOne()
    {
        var tenantId = TenantId.New();
        _repository.ListResult = Result<IReadOnlyList<IntakeDocument>>.Success(
            Array.Empty<IntakeDocument>());

        await _handler.HandleAsync(tenantId.Value, -5, 20, CancellationToken.None);

        Assert.Equal(1, _repository.LastPage);
    }

    [Fact]
    public async Task HandleAsync_PageSizeAboveMaximum_ClampedTo100()
    {
        var tenantId = TenantId.New();
        _repository.ListResult = Result<IReadOnlyList<IntakeDocument>>.Success(
            Array.Empty<IntakeDocument>());

        await _handler.HandleAsync(tenantId.Value, 1, 500, CancellationToken.None);

        Assert.Equal(100, _repository.LastPageSize);
    }

    [Fact]
    public async Task HandleAsync_PageSizeBelowMinimum_ClampedToOne()
    {
        var tenantId = TenantId.New();
        _repository.ListResult = Result<IReadOnlyList<IntakeDocument>>.Success(
            Array.Empty<IntakeDocument>());

        await _handler.HandleAsync(tenantId.Value, 1, 0, CancellationToken.None);

        Assert.Equal(1, _repository.LastPageSize);
    }

    private sealed class StubRepository : IDocumentRepository
    {
        public Result<IReadOnlyList<IntakeDocument>> ListResult { get; set; } =
            Result<IReadOnlyList<IntakeDocument>>.Success(Array.Empty<IntakeDocument>());

        public int LastPage { get; private set; }
        public int LastPageSize { get; private set; }

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByTenantAsync(
            TenantId tenantId, int page, int pageSize, CancellationToken ct = default)
        {
            LastPage = page;
            LastPageSize = pageSize;
            return Task.FromResult(ListResult);
        }

        public Task<Result<IntakeDocument?>> FindByIdAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IntakeDocument?>> FindByIdUnfilteredAsync(DocumentId id, CancellationToken ct = default)
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
    }
}
