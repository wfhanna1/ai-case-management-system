using Api.Application.DTOs;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.Application.Commands;

public sealed class SubmitDocumentHandler
{
    private readonly IDocumentRepository _repository;
    private readonly IFileStoragePort _fileStorage;
    private readonly IMessageBusPort _messageBus;
    private readonly ILogger<SubmitDocumentHandler> _logger;

    public SubmitDocumentHandler(
        IDocumentRepository repository,
        IFileStoragePort fileStorage,
        IMessageBusPort messageBus,
        ILogger<SubmitDocumentHandler> logger)
    {
        _repository = repository;
        _fileStorage = fileStorage;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<Result<DocumentDto>> HandleAsync(
        Stream fileContent,
        SubmitDocumentRequest request,
        CancellationToken ct = default)
    {
        var tenantId = new TenantId(request.TenantId);

        var uploadResult = await _fileStorage.UploadAsync(
            fileContent, request.FileName, request.ContentType, tenantId, ct);

        if (uploadResult.IsFailure)
            return Result<DocumentDto>.Failure(uploadResult.Error);

        var document = IntakeDocument.Submit(tenantId, request.FileName, uploadResult.Value);

        var saveResult = await _repository.SaveAsync(document, ct);
        if (saveResult.IsFailure)
        {
            await _fileStorage.DeleteAsync(uploadResult.Value, tenantId, ct);
            return Result<DocumentDto>.Failure(saveResult.Error);
        }

        var publishResult = await _messageBus.PublishDocumentUploadedAsync(
            document.Id, null, tenantId, request.FileName, ct);

        if (publishResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to publish DocumentUploadedEvent for document {DocumentId}: {Error}. Document saved successfully; event will need retry.",
                document.Id.Value, publishResult.Error.Message);
        }

        return Result<DocumentDto>.Success(new DocumentDto(
            document.Id.Value,
            document.TenantId.Value,
            document.OriginalFileName,
            document.Status.ToString(),
            document.SubmittedAt,
            document.ProcessedAt));
    }
}
