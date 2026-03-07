using Api.Application.DTOs;
using Api.Application.Mappings;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Queries;

public sealed class ListPendingReviewHandler
{
    private readonly IDocumentRepository _repository;

    public ListPendingReviewHandler(IDocumentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<PendingReviewResultDto>> HandleAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var tid = new TenantId(tenantId);

        var statuses = new[] { DocumentStatus.PendingReview, DocumentStatus.InReview };
        var result = await _repository.ListByStatusesAsync(tid, statuses, page, pageSize, ct);
        if (result.IsFailure)
            return Result<PendingReviewResultDto>.Failure(result.Error);

        var dtos = result.Value.Items
            .Select(ReviewMappings.ToDto)
            .ToList();

        return Result<PendingReviewResultDto>.Success(
            new PendingReviewResultDto(dtos, result.Value.TotalCount, page, pageSize));
    }
}
