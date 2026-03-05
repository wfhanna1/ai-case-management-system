using Api.Domain.Aggregates;
using SharedKernel;

namespace Api.Domain.Ports;

/// <summary>
/// Output port (driven adapter interface) for persisting and retrieving intake documents.
/// Implementations live in Api.Infrastructure.
/// </summary>
public interface IDocumentRepository
{
    Task<Result<IntakeDocument?>> FindByIdAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<IntakeDocument>>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default);
    Task<Result<Unit>> SaveAsync(IntakeDocument document, CancellationToken ct = default);
    Task<Result<Unit>> DeleteAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default);
}
