using Api.Application.DTOs;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Queries;

public sealed class SearchCasesHandler
{
    private readonly ICaseRepository _repository;

    public SearchCasesHandler(ICaseRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<SearchCasesResultDto>> HandleAsync(
        Guid tenantId,
        string? query,
        string? statusFilter,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        DocumentStatus? status = null;
        if (!string.IsNullOrWhiteSpace(statusFilter) &&
            Enum.TryParse<DocumentStatus>(statusFilter, ignoreCase: true, out var parsed))
        {
            status = parsed;
        }

        var result = await _repository.SearchAsync(
            new TenantId(tenantId), query, status, from, to, page, pageSize, ct);

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
