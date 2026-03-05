using Api.Application.DTOs;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Queries;

public sealed class GetCaseByIdHandler
{
    private readonly ICaseRepository _repository;

    public GetCaseByIdHandler(ICaseRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<CaseDetailDto?>> HandleAsync(
        Guid id, Guid tenantId, CancellationToken ct = default)
    {
        var result = await _repository.FindByIdAsync(
            new CaseId(id), new TenantId(tenantId), ct);

        if (result.IsFailure)
            return Result<CaseDetailDto?>.Failure(result.Error);

        if (result.Value is null)
            return Result<CaseDetailDto?>.Success(null);

        var c = result.Value;
        var docDtos = c.Documents.Select(doc => new DocumentDto(
            doc.Id.Value, doc.TenantId.Value, doc.OriginalFileName,
            doc.Status.ToString(), doc.SubmittedAt, doc.ProcessedAt)).ToList();

        return Result<CaseDetailDto?>.Success(new CaseDetailDto(
            c.Id.Value, c.TenantId.Value, c.SubjectName,
            c.CreatedAt, c.UpdatedAt, docDtos));
    }
}
