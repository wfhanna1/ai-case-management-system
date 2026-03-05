using Api.Application.DTOs;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Queries;

public sealed class GetDocumentByIdHandler
{
    private readonly IDocumentRepository _repository;

    public GetDocumentByIdHandler(IDocumentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<DocumentDto?>> HandleAsync(
        Guid id, Guid tenantId, CancellationToken ct = default)
    {
        var result = await _repository.FindByIdAsync(
            new DocumentId(id), new TenantId(tenantId), ct);

        if (result.IsFailure)
            return Result<DocumentDto?>.Failure(result.Error);

        if (result.Value is null)
            return Result<DocumentDto?>.Success(null);

        var doc = result.Value;
        return Result<DocumentDto?>.Success(new DocumentDto(
            doc.Id.Value, doc.TenantId.Value, doc.OriginalFileName,
            doc.Status.ToString(), doc.SubmittedAt, doc.ProcessedAt));
    }
}
