using Api.Application.DTOs;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Queries;

public sealed class GetDashboardStatsHandler
{
    private readonly IDocumentRepository _documentRepo;
    private readonly ICaseRepository _caseRepo;

    public GetDashboardStatsHandler(IDocumentRepository documentRepo, ICaseRepository caseRepo)
    {
        _documentRepo = documentRepo;
        _caseRepo = caseRepo;
    }

    public async Task<Result<DashboardStatsDto>> HandleAsync(Guid tenantId, CancellationToken ct)
    {
        var tenant = new TenantId(tenantId);

        var statsResult = await _documentRepo.GetStatsAsync(tenant, ct);
        if (statsResult.IsFailure)
            return Result<DashboardStatsDto>.Failure(statsResult.Error);

        var countResult = await _caseRepo.CountByTenantAsync(tenant, ct);
        if (countResult.IsFailure)
            return Result<DashboardStatsDto>.Failure(countResult.Error);

        var (pendingReview, processedToday, avgTime) = statsResult.Value;
        var totalCases = countResult.Value;

        var stats = DashboardStats.Create(totalCases, pendingReview, processedToday, avgTime);

        return Result<DashboardStatsDto>.Success(
            new DashboardStatsDto(stats.TotalCases, stats.PendingReview, stats.ProcessedToday, stats.FormattedProcessingTime));
    }
}
