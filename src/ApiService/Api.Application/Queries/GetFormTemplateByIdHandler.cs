using Api.Application.DTOs;
using Api.Application.Mappings;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Queries;

public sealed class GetFormTemplateByIdHandler
{
    private readonly IFormTemplateRepository _repository;

    public GetFormTemplateByIdHandler(IFormTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<FormTemplateDto?>> HandleAsync(
        Guid id,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var result = await _repository.FindByIdAsync(
            new FormTemplateId(id), new TenantId(tenantId), ct);

        if (result.IsFailure)
            return Result<FormTemplateDto?>.Failure(result.Error);

        if (result.Value is null)
            return Result<FormTemplateDto?>.Success(null);

        return Result<FormTemplateDto?>.Success(FormTemplateMappings.ToDto(result.Value));
    }
}
