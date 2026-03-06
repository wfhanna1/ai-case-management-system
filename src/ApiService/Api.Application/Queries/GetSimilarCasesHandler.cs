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
        // Load the document to get its extracted fields
        var docResult = await _documentRepository.FindByIdAsync(
            new DocumentId(documentId), new TenantId(tenantId), ct);

        if (docResult.IsFailure)
            return Result<SimilarCasesResultDto>.Failure(docResult.Error);

        if (docResult.Value is null)
            return Result<SimilarCasesResultDto>.Failure(
                new Error("NOT_FOUND", $"Document {documentId} not found"));

        var document = docResult.Value;

        // Build text content from extracted fields
        var textContent = BuildTextFromFields(document);
        if (string.IsNullOrWhiteSpace(textContent))
            return Result<SimilarCasesResultDto>.Failure(
                new Error("NO_CONTENT", "Document has no extracted fields for similarity search"));

        // Search by text content (embedding generated on-the-fly by RAG service)
        var searchResult = await _ragClient.FindSimilarByTextAsync(
            textContent, tenantId, topK + 1, ct);

        if (searchResult.IsFailure)
            return Result<SimilarCasesResultDto>.Failure(searchResult.Error);

        // Exclude the queried document itself from results
        var filtered = searchResult.Value
            .Where(h => h.DocumentId != documentId)
            .Take(topK);

        var items = new List<SimilarCaseDto>();
        foreach (var hit in filtered)
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

    private static string BuildTextFromFields(IntakeDocument document)
    {
        if (document.ExtractedFields.Count == 0)
            return string.Empty;

        var parts = document.ExtractedFields
            .Select(f => $"{f.Name}: {f.CorrectedValue ?? f.Value}");
        return string.Join(". ", parts);
    }
}
