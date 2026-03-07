using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Queries;

public sealed class DownloadDocumentHandler
{
    private readonly IDocumentRepository _repository;
    private readonly IFileStoragePort _fileStorage;

    public DownloadDocumentHandler(IDocumentRepository repository, IFileStoragePort fileStorage)
    {
        _repository = repository;
        _fileStorage = fileStorage;
    }

    public async Task<Result<(Stream Content, string FileName)>> HandleAsync(
        Guid id, Guid tenantId, CancellationToken ct = default)
    {
        var tenantIdVo = new TenantId(tenantId);
        var findResult = await _repository.FindByIdAsync(new DocumentId(id), tenantIdVo, ct);

        if (findResult.IsFailure)
            return Result<(Stream, string)>.Failure(findResult.Error);

        if (findResult.Value is null)
            return Result<(Stream, string)>.Failure(new Error("NOT_FOUND", "Document not found"));

        var doc = findResult.Value;
        var downloadResult = await _fileStorage.DownloadAsync(doc.StorageKey, tenantIdVo, ct);

        if (downloadResult.IsFailure)
            return Result<(Stream, string)>.Failure(downloadResult.Error);

        return Result<(Stream, string)>.Success((downloadResult.Value, doc.OriginalFileName));
    }
}
