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

    /// <summary>
    /// Finds a document by ID without tenant filtering. Used by background consumers
    /// that perform their own tenant verification from the event payload.
    /// </summary>
    Task<Result<IntakeDocument?>> FindByIdUnfilteredAsync(DocumentId id, CancellationToken ct = default);

    Task<Result<IReadOnlyList<IntakeDocument>>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default);
    Task<Result<Unit>> SaveAsync(IntakeDocument document, CancellationToken ct = default);
    Task<Result<Unit>> UpdateAsync(IntakeDocument document, CancellationToken ct = default);
    Task<Result<Unit>> DeleteAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default);
}
