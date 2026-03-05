using Api.Application.DTOs;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Queries;

public sealed class ListDocumentsByTenantHandler
{
    private readonly IDocumentRepository _repository;

    public ListDocumentsByTenantHandler(IDocumentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<DocumentDto>>> HandleAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await _repository.ListByTenantAsync(
            new TenantId(tenantId), page, pageSize, ct);

        if (result.IsFailure)
            return Result<IReadOnlyList<DocumentDto>>.Failure(result.Error);

        var dtos = result.Value.Select(doc => new DocumentDto(
            doc.Id.Value, doc.TenantId.Value, doc.OriginalFileName,
            doc.Status.ToString(), doc.SubmittedAt, doc.ProcessedAt)).ToList();

        return Result<IReadOnlyList<DocumentDto>>.Success(dtos);
    }
}
