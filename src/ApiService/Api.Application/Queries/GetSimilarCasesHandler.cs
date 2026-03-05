using Api.Application.DTOs;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Queries;

public sealed class GetSimilarCasesHandler
{
    private readonly IRagServiceClient _ragClient;
    private readonly ISummaryPort _summaryPort;
    private readonly IDocumentRepository _documentRepository;

    public GetSimilarCasesHandler(
        IRagServiceClient ragClient,
        ISummaryPort summaryPort,
        IDocumentRepository documentRepository)
    {
        _ragClient = ragClient;
        _summaryPort = summaryPort;
        _documentRepository = documentRepository;
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

        var sourceFields = await GetSourceFieldsAsync(documentId, tenantId, ct);

        var items = new List<SimilarCaseDto>();
        foreach (var hit in searchResult.Value)
        {
            var summaryResult = await _summaryPort.GenerateSummaryAsync(hit.Metadata, ct);
            var summary = summaryResult.IsSuccess ? summaryResult.Value : "Summary unavailable";

            var sharedFields = ComputeSharedFields(sourceFields, hit.Metadata);

            items.Add(new SimilarCaseDto(
                hit.DocumentId,
                hit.Score,
                summary,
                hit.Metadata,
                sharedFields));
        }

        return Result<SimilarCasesResultDto>.Success(new SimilarCasesResultDto(items));
    }

    private async Task<Dictionary<string, string>> GetSourceFieldsAsync(
        Guid documentId, Guid tenantId, CancellationToken ct)
    {
        var docResult = await _documentRepository.FindByIdAsync(
            new DocumentId(documentId), new TenantId(tenantId), ct);

        if (docResult.IsFailure || docResult.Value is null)
            return new Dictionary<string, string>();

        return docResult.Value.ExtractedFields
            .ToDictionary(
                f => f.Name,
                f => f.CorrectedValue ?? f.Value);
    }

    private static Dictionary<string, string> ComputeSharedFields(
        Dictionary<string, string> sourceFields,
        Dictionary<string, string> similarMetadata)
    {
        var shared = new Dictionary<string, string>();

        foreach (var (fieldName, sourceValue) in sourceFields)
        {
            if (similarMetadata.TryGetValue(fieldName, out var similarValue) &&
                string.Equals(sourceValue, similarValue, StringComparison.OrdinalIgnoreCase))
            {
                shared[fieldName] = similarValue;
            }
        }

        return shared;
    }
}
