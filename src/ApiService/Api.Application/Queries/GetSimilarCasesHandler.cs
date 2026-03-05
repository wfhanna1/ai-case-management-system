using Api.Application.DTOs;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Queries;

public sealed class GetSimilarCasesHandler
{
    private readonly IRagServiceClient _ragClient;
    private readonly ISummaryPort _summaryPort;

    public GetSimilarCasesHandler(IRagServiceClient ragClient, ISummaryPort summaryPort)
    {
        _ragClient = ragClient;
        _summaryPort = summaryPort;
    }

    public async Task<Result<SimilarCasesResultDto>> HandleAsync(
        Guid documentId,
        Guid tenantId,
        int topK = 5,
        CancellationToken ct = default)
    {
        var searchResult = await _ragClient.FindSimilarAsync(
            documentId, tenantId, topK, ct);

        if (searchResult.IsFailure)
            return Result<SimilarCasesResultDto>.Failure(searchResult.Error);

        var items = new List<SimilarCaseDto>();
        foreach (var hit in searchResult.Value)
        {
            var summaryResult = await _summaryPort.GenerateSummaryAsync(hit.Metadata, ct);
            var summary = summaryResult.IsSuccess ? summaryResult.Value : "Summary unavailable";

            items.Add(new SimilarCaseDto(
                hit.DocumentId,
                hit.Score,
                summary,
                hit.Metadata));
        }

        return Result<SimilarCasesResultDto>.Success(new SimilarCasesResultDto(items));
    }
}
