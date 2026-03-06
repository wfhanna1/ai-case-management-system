using System.Net.Http.Json;
using System.Text.Json;
using Api.Domain.Ports;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.Infrastructure.RagClient;

/// <summary>
/// HTTP client adapter that calls the RAG microservice for similar document search.
/// </summary>
public sealed class HttpRagServiceClient : IRagServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpRagServiceClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public HttpRagServiceClient(HttpClient httpClient, ILogger<HttpRagServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<SimilarDocumentResult>>> FindSimilarByTextAsync(
        string textContent, Guid tenantId, int topK = 5, CancellationToken ct = default)
    {
        try
        {
            var requestBody = new { text = textContent, tenantId, topK };
            var response = await _httpClient.PostAsJsonAsync("/api/similar-by-text", requestBody, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "RAG service returned {StatusCode} for text-based similar search",
                    response.StatusCode);
                return Result<IReadOnlyList<SimilarDocumentResult>>.Failure(
                    new Error("RAG_SERVICE_ERROR",
                        $"RAG service returned {(int)response.StatusCode}"));
            }

            var body = await response.Content.ReadFromJsonAsync<RagSearchResponse>(JsonOptions, ct);
            if (body?.Data is null)
                return Result<IReadOnlyList<SimilarDocumentResult>>.Success(
                    Array.Empty<SimilarDocumentResult>());

            var results = body.Data.Select(h => new SimilarDocumentResult(
                h.DocumentId, h.Score, h.Metadata ?? new Dictionary<string, string>()
            )).ToList();

            return Result<IReadOnlyList<SimilarDocumentResult>>.Success(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call RAG service for text-based similar search");
            return Result<IReadOnlyList<SimilarDocumentResult>>.Failure(
                new Error("RAG_SERVICE_ERROR", ex.Message));
        }
    }

    private sealed record RagSearchResponse(IReadOnlyList<RagSearchHit>? Data);
    private sealed record RagSearchHit(Guid DocumentId, double Score, Dictionary<string, string>? Metadata);
}
