using Api.Application.DTOs;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Queries;

public sealed class GetRecentActivitiesHandler
{
    private readonly IAuditLogRepository _auditLogRepo;

    public GetRecentActivitiesHandler(IAuditLogRepository auditLogRepo)
    {
        _auditLogRepo = auditLogRepo;
    }

    public async Task<Result<IReadOnlyList<RecentActivityDto>>> HandleAsync(
        Guid tenantId, int limit, CancellationToken ct)
    {
        var tenant = new TenantId(tenantId);
        var result = await _auditLogRepo.ListRecentByTenantAsync(tenant, limit, ct);

        if (result.IsFailure)
            return Result<IReadOnlyList<RecentActivityDto>>.Failure(result.Error);

        var dtos = result.Value.Select(e => new RecentActivityDto(
            e.Id,
            e.DocumentId.Value,
            e.Action.ToString(),
            e.PerformedBy?.Value,
            e.Timestamp,
            e.FieldName,
            e.PreviousValue,
            e.NewValue)).ToList();

        return Result<IReadOnlyList<RecentActivityDto>>.Success(dtos);
    }
}
