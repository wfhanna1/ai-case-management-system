using Api.Application.Queries;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class DownloadDocumentHandlerTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();

    private readonly StubRepository _repository = new();
    private readonly StubFileStorage _fileStorage = new();
    private readonly DownloadDocumentHandler _handler;

    public DownloadDocumentHandlerTests()
    {
        _handler = new DownloadDocumentHandler(_repository, _fileStorage);
    }

    [Fact]
    public async Task HandleAsync_DocumentExists_ReturnsStreamAndFileName()
    {
        var doc = IntakeDocument.Submit(new TenantId(TenantGuid), "scan.pdf", "tenant/guid/scan.pdf");
        _repository.DocumentToReturn = doc;
        _fileStorage.StreamToReturn = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await _handler.HandleAsync(doc.Id.Value, TenantGuid, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("scan.pdf", result.Value.FileName);
        Assert.NotNull(result.Value.Content);
        Assert.Equal("tenant/guid/scan.pdf", _fileStorage.LastDownloadedKey);
    }

    [Fact]
    public async Task HandleAsync_DocumentNotFound_ReturnsNotFoundError()
    {
        _repository.DocumentToReturn = null;

        var result = await _handler.HandleAsync(Guid.NewGuid(), TenantGuid, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_RepositoryFails_ReturnsFailure()
    {
        _repository.FindResult = Result<IntakeDocument?>.Failure(new Error("DB_ERROR", "connection lost"));

        var result = await _handler.HandleAsync(Guid.NewGuid(), TenantGuid, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DB_ERROR", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_FileStorageFails_ReturnsFailure()
    {
        var doc = IntakeDocument.Submit(new TenantId(TenantGuid), "scan.pdf", "tenant/guid/scan.pdf");
        _repository.DocumentToReturn = doc;
        _fileStorage.DownloadResult = Result<Stream>.Failure(new Error("NOT_FOUND", "File not found"));

        var result = await _handler.HandleAsync(doc.Id.Value, TenantGuid, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    // --- Test Doubles ---

    private sealed class StubRepository : IDocumentRepository
    {
        public IntakeDocument? DocumentToReturn { get; set; }
        public Result<IntakeDocument?>? FindResult { get; set; }

        public Task<Result<IntakeDocument?>> FindByIdAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
        {
            if (FindResult is not null)
                return Task.FromResult(FindResult);
            return Task.FromResult(Result<IntakeDocument?>.Success(DocumentToReturn));
        }

        public Task<Result<Unit>> SaveAsync(IntakeDocument document, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<IntakeDocument?>> FindByIdUnfilteredAsync(DocumentId id, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusAsync(TenantId tenantId, DocumentStatus status, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<(IReadOnlyList<IntakeDocument> Items, int TotalCount)>> ListByStatusesAsync(TenantId tenantId, IReadOnlyList<DocumentStatus> statuses, int page, int pageSize, CancellationToken ct = default)
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

    private sealed class StubFileStorage : IFileStoragePort
    {
        public Stream StreamToReturn { get; set; } = new MemoryStream(new byte[] { 1 });
        public Result<Stream>? DownloadResult { get; set; }
        public string? LastDownloadedKey { get; private set; }

        public Task<Result<Stream>> DownloadAsync(string storageKey, TenantId tenantId, CancellationToken ct = default)
        {
            LastDownloadedKey = storageKey;
            if (DownloadResult is not null)
                return Task.FromResult(DownloadResult);
            return Task.FromResult(Result<Stream>.Success(StreamToReturn));
        }

        public Task<Result<string>> UploadAsync(Stream content, string fileName, string contentType, TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<Unit>> DeleteAsync(string storageKey, TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
