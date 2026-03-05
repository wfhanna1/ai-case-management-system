using Api.Application.DTOs;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Queries;

public sealed class ListCasesHandler
{
    private readonly ICaseRepository _repository;

    public ListCasesHandler(ICaseRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<SearchCasesResultDto>> HandleAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await _repository.ListByTenantAsync(
            new TenantId(tenantId), page, pageSize, ct);

        if (result.IsFailure)
            return Result<SearchCasesResultDto>.Failure(result.Error);

        var (items, totalCount) = result.Value;

        var dtos = items.Select(c => new CaseDto(
            c.Id.Value, c.TenantId.Value, c.SubjectName,
            c.CreatedAt, c.UpdatedAt, c.Documents.Count)).ToList();

        return Result<SearchCasesResultDto>.Success(
            new SearchCasesResultDto(dtos, totalCount, page, pageSize));
    }
}
