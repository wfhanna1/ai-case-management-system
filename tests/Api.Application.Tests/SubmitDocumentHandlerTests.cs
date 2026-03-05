using Api.Application.Commands;
using Api.Application.DTOs;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class SubmitDocumentHandlerTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();

    private readonly StubFileStorage _fileStorage = new();
    private readonly StubRepository _repository = new();
    private readonly StubMessageBus _messageBus = new();
    private readonly SubmitDocumentHandler _handler;

    public SubmitDocumentHandlerTests()
    {
        _handler = new SubmitDocumentHandler(
            _repository, _fileStorage, _messageBus,
            NullLogger<SubmitDocumentHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_AllSucceed_ReturnsSuccessDto()
    {
        var request = new SubmitDocumentRequest(TenantGuid, "test.pdf", "application/pdf");
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await _handler.HandleAsync(stream, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantGuid, result.Value.TenantId);
        Assert.Equal("test.pdf", result.Value.OriginalFileName);
        Assert.Equal("Submitted", result.Value.Status);
        Assert.True(_repository.SaveCalled);
        Assert.True(_messageBus.PublishCalled);
    }

    [Fact]
    public async Task HandleAsync_UploadFails_ReturnsFailureAndDoesNotSave()
    {
        _fileStorage.UploadResult = Result<string>.Failure(new Error("STORAGE_ERROR", "disk full"));
        var request = new SubmitDocumentRequest(TenantGuid, "test.pdf", "application/pdf");
        using var stream = new MemoryStream(new byte[] { 1 });

        var result = await _handler.HandleAsync(stream, request, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("STORAGE_ERROR", result.Error.Code);
        Assert.False(_repository.SaveCalled);
        Assert.False(_messageBus.PublishCalled);
    }

    [Fact]
    public async Task HandleAsync_SaveFails_ReturnsFailureAndCleansUpFile()
    {
        _repository.SaveResult = Result<Unit>.Failure(new Error("DB_ERROR", "connection lost"));
        var request = new SubmitDocumentRequest(TenantGuid, "test.pdf", "application/pdf");
        using var stream = new MemoryStream(new byte[] { 1 });

        var result = await _handler.HandleAsync(stream, request, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DB_ERROR", result.Error.Code);
        Assert.True(_fileStorage.DeleteCalled);
        Assert.False(_messageBus.PublishCalled);
    }

    [Fact]
    public async Task HandleAsync_PublishFails_ReturnsSuccessBecauseNonFatal()
    {
        _messageBus.PublishResult = Result<Unit>.Failure(new Error("BUS_ERROR", "broker down"));
        var request = new SubmitDocumentRequest(TenantGuid, "test.pdf", "application/pdf");
        using var stream = new MemoryStream(new byte[] { 1 });

        var result = await _handler.HandleAsync(stream, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("test.pdf", result.Value.OriginalFileName);
        Assert.True(_repository.SaveCalled);
    }

    // --- Test Doubles ---

    private sealed class StubFileStorage : IFileStoragePort
    {
        public Result<string> UploadResult { get; set; } = Result<string>.Success("tenant/guid/test.pdf");
        public bool DeleteCalled { get; private set; }

        public Task<Result<string>> UploadAsync(
            Stream content, string fileName, string contentType, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(UploadResult);

        public Task<Result<Stream>> DownloadAsync(string storageKey, TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<Unit>> DeleteAsync(string storageKey, TenantId tenantId, CancellationToken ct = default)
        {
            DeleteCalled = true;
            return Task.FromResult(Result<Unit>.Success(Unit.Value));
        }
    }

    private sealed class StubRepository : IDocumentRepository
    {
        public Result<Unit> SaveResult { get; set; } = Result<Unit>.Success(Unit.Value);
        public bool SaveCalled { get; private set; }

        public Task<Result<Unit>> SaveAsync(IntakeDocument document, CancellationToken ct = default)
        {
            SaveCalled = true;
            return Task.FromResult(SaveResult);
        }

        public Task<Result<IntakeDocument?>> FindByIdAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IntakeDocument?>> FindByIdUnfilteredAsync(DocumentId id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusAsync(TenantId tenantId, DocumentStatus status, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusesAsync(TenantId tenantId, IReadOnlyList<DocumentStatus> statuses, int page, int pageSize, CancellationToken ct = default)
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

    private sealed class StubMessageBus : IMessageBusPort
    {
        public Result<Unit> PublishResult { get; set; } = Result<Unit>.Success(Unit.Value);
        public bool PublishCalled { get; private set; }

        public Task<Result<Unit>> PublishDocumentUploadedAsync(
            DocumentId documentId, Guid? templateId, TenantId tenantId, string fileName, CancellationToken ct = default)
        {
            PublishCalled = true;
            return Task.FromResult(PublishResult);
        }

        public Task<Result<Unit>> PublishEmbeddingRequestedAsync(
            DocumentId documentId, TenantId tenantId, string textContent,
            Dictionary<string, string> fieldValues, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
