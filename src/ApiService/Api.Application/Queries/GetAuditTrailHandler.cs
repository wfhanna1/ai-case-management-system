using Api.Application.DTOs;
using Api.Application.Mappings;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Queries;

public sealed class GetAuditTrailHandler
{
    private readonly IAuditLogRepository _auditLogRepository;

    public GetAuditTrailHandler(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<Result<IReadOnlyList<AuditLogEntryDto>>> HandleAsync(
        Guid documentId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var tid = new TenantId(tenantId);
        var did = new DocumentId(documentId);

        var result = await _auditLogRepository.ListByDocumentAsync(did, tid, ct);
        if (result.IsFailure)
            return Result<IReadOnlyList<AuditLogEntryDto>>.Failure(result.Error);

        var dtos = result.Value.Select(ReviewMappings.ToDto).ToList();
        return Result<IReadOnlyList<AuditLogEntryDto>>.Success(dtos);
    }
}
