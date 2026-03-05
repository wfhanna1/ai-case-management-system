using Api.Application.DTOs;
using Api.Application.Mappings;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Queries;

public sealed class ListFormTemplatesByTenantHandler
{
    private readonly IFormTemplateRepository _repository;

    public ListFormTemplatesByTenantHandler(IFormTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<FormTemplateDto>>> HandleAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var result = await _repository.ListByTenantAsync(new TenantId(tenantId), ct);

        if (result.IsFailure)
            return Result<IReadOnlyList<FormTemplateDto>>.Failure(result.Error);

        var dtos = result.Value.Select(FormTemplateMappings.ToDto).ToList();

        return Result<IReadOnlyList<FormTemplateDto>>.Success(dtos);
    }
}
