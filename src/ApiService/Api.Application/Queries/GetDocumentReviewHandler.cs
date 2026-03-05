using Api.Application.DTOs;
using Api.Application.Mappings;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Queries;

public sealed class GetDocumentReviewHandler
{
    private readonly IDocumentRepository _repository;

    public GetDocumentReviewHandler(IDocumentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<ReviewDocumentDto?>> HandleAsync(
        Guid documentId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var tid = new TenantId(tenantId);
        var did = new Domain.Aggregates.DocumentId(documentId);

        var result = await _repository.FindByIdAsync(did, tid, ct);
        if (result.IsFailure)
            return Result<ReviewDocumentDto?>.Failure(result.Error);

        if (result.Value is null)
            return Result<ReviewDocumentDto?>.Success(null);

        return Result<ReviewDocumentDto?>.Success(ReviewMappings.ToDto(result.Value));
    }
}
