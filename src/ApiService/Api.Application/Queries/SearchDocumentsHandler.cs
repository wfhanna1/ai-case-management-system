using Api.Application.DTOs;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Queries;

public sealed class SearchDocumentsHandler
{
    private readonly IDocumentRepository _repository;

    public SearchDocumentsHandler(IDocumentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<SearchDocumentsResultDto>> HandleAsync(
        Guid tenantId,
        string? fileNameContains,
        string? statusFilter,
        DateTimeOffset? submittedAfter,
        DateTimeOffset? submittedBefore,
        string? extractedFieldContains,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        DocumentStatus? status = null;
        if (!string.IsNullOrWhiteSpace(statusFilter) &&
            Enum.TryParse<DocumentStatus>(statusFilter, ignoreCase: true, out var parsed))
        {
            status = parsed;
        }

        var result = await _repository.SearchAsync(
            new TenantId(tenantId), fileNameContains, status,
            submittedAfter, submittedBefore, extractedFieldContains,
            page, pageSize, ct);

        if (result.IsFailure)
            return Result<SearchDocumentsResultDto>.Failure(result.Error);

        var (items, totalCount) = result.Value;

        var dtos = items.Select(doc => new DocumentDto(
            doc.Id.Value, doc.TenantId.Value, doc.OriginalFileName,
            doc.Status.ToString(), doc.SubmittedAt, doc.ProcessedAt)).ToList();

        return Result<SearchDocumentsResultDto>.Success(
            new SearchDocumentsResultDto(dtos, totalCount, page, pageSize));
    }
}
