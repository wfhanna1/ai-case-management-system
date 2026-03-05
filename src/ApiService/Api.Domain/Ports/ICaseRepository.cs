using Api.Domain.Aggregates;
using SharedKernel;

namespace Api.Domain.Ports;

public interface ICaseRepository
{
    Task<Result<Case?>> FindByIdAsync(CaseId id, TenantId tenantId, CancellationToken ct = default);
    Task<Result<Case?>> FindBySubjectNameAsync(string subjectName, TenantId tenantId, CancellationToken ct = default);
    Task<Result<(IReadOnlyList<Case> Items, int TotalCount)>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default);
    Task<Result<(IReadOnlyList<Case> Items, int TotalCount)>> SearchAsync(TenantId tenantId, string? query, DocumentStatus? status, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken ct = default);
    Task<Result<Unit>> SaveAsync(Case @case, CancellationToken ct = default);
    Task<Result<Unit>> UpdateAsync(Case @case, CancellationToken ct = default);
}
