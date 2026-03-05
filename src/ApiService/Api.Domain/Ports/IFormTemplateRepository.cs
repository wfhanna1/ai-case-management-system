using Api.Domain.Aggregates;
using SharedKernel;

namespace Api.Domain.Ports;

public interface IFormTemplateRepository
{
    Task<Result<FormTemplate?>> FindByIdAsync(FormTemplateId id, TenantId tenantId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<FormTemplate>>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default);
    Task<Result<Unit>> SaveAsync(FormTemplate template, CancellationToken ct = default);
}
