using Api.Domain.Aggregates;
using SharedKernel;

namespace Api.Domain.Ports;

/// <summary>
/// Output port for persisting and retrieving audit log entries.
/// </summary>
public interface IAuditLogRepository
{
    Task<Result<Unit>> SaveAsync(AuditLogEntry entry, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AuditLogEntry>>> ListByDocumentAsync(DocumentId documentId, TenantId tenantId, CancellationToken ct = default);
}
