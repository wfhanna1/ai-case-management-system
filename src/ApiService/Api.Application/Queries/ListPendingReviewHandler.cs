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

    public async Task<Result<IReadOnlyList<ReviewDocumentDto>>> HandleAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var tid = new TenantId(tenantId);

        // Retrieve both PendingReview and InReview documents.
        var pendingResult = await _repository.ListByStatusAsync(tid, DocumentStatus.PendingReview, page, pageSize, ct);
        if (pendingResult.IsFailure)
            return Result<IReadOnlyList<ReviewDocumentDto>>.Failure(pendingResult.Error);

        var inReviewResult = await _repository.ListByStatusAsync(tid, DocumentStatus.InReview, page, pageSize, ct);
        if (inReviewResult.IsFailure)
            return Result<IReadOnlyList<ReviewDocumentDto>>.Failure(inReviewResult.Error);

        var combined = pendingResult.Value
            .Concat(inReviewResult.Value)
            .OrderByDescending(d => d.ProcessedAt)
            .Select(ReviewMappings.ToDto)
            .ToList();

        return Result<IReadOnlyList<ReviewDocumentDto>>.Success(combined);
    }
}
