using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using RagService.Domain.Ports;
using SharedKernel;

namespace RagService.Infrastructure.VectorStore;

/// <summary>
/// Adapter that stores and queries vector embeddings in Qdrant.
/// Uses a single collection per deployment with tenant isolation via payload filtering.
/// </summary>
public sealed class QdrantVectorStoreAdapter : IVectorStorePort
{
    private const string CollectionName = "documents";
    private readonly QdrantClient _client;
    private readonly ILogger<QdrantVectorStoreAdapter> _logger;
    private bool _collectionEnsured;

    public QdrantVectorStoreAdapter(QdrantClient client, ILogger<QdrantVectorStoreAdapter> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<Result<Unit>> UpsertAsync(
        Guid documentId,
        Guid tenantId,
        float[] embedding,
        Dictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        try
        {
            await EnsureCollectionAsync((uint)embedding.Length, ct);

            var payload = new Dictionary<string, Value>
            {
                ["tenant_id"] = tenantId.ToString(),
                ["document_id"] = documentId.ToString(),
            };

            foreach (var (key, value) in metadata)
                payload[$"meta_{key}"] = value;

            var point = new PointStruct
            {
                Id = new PointId { Uuid = documentId.ToString() },
                Vectors = embedding,
                Payload = { payload }
            };

            await _client.UpsertAsync(CollectionName, [point], cancellationToken: ct);

            _logger.LogInformation(
                "Upserted embedding for DocumentId={DocumentId} TenantId={TenantId}",
                documentId, tenantId);

            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert embedding for DocumentId={DocumentId}", documentId);
            return Result<Unit>.Failure(new Error("VECTOR_STORE_ERROR", ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<SearchHit>>> SearchAsync(
        Guid tenantId,
        float[] queryEmbedding,
        int topK = 5,
        CancellationToken ct = default)
    {
        try
        {
            await EnsureCollectionAsync((uint)queryEmbedding.Length, ct);

            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "tenant_id",
                            Match = new Match { Keyword = tenantId.ToString() }
                        }
                    }
                }
            };

            var results = await _client.SearchAsync(
                CollectionName,
                queryEmbedding,
                filter: filter,
                limit: (ulong)topK,
                payloadSelector: true,
                cancellationToken: ct);

            var hits = results.Select(r =>
            {
                var docId = Guid.Parse(r.Payload["document_id"].StringValue);
                var meta = r.Payload
                    .Where(kv => kv.Key.StartsWith("meta_"))
                    .ToDictionary(kv => kv.Key[5..], kv => kv.Value.StringValue);

                return new SearchHit(docId, r.Score, meta);
            }).ToList();

            return Result<IReadOnlyList<SearchHit>>.Success(hits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search vectors for TenantId={TenantId}", tenantId);
            return Result<IReadOnlyList<SearchHit>>.Failure(
                new Error("VECTOR_STORE_ERROR", ex.Message));
        }
    }

    public async Task<Result<float[]>> GetEmbeddingAsync(
        Guid documentId, CancellationToken ct = default)
    {
        try
        {
            var points = await _client.RetrieveAsync(
                CollectionName,
                documentId,
                withPayload: false,
                withVectors: true,
                cancellationToken: ct);

            if (points is null || points.Count == 0)
                return Result<float[]>.Failure(
                    new Error("EMBEDDING_NOT_FOUND",
                        $"No embedding found for document {documentId}"));

            var vector = points[0].Vectors.Vector.Data.ToArray();
            return Result<float[]>.Success(vector);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get embedding for DocumentId={DocumentId}", documentId);
            return Result<float[]>.Failure(new Error("VECTOR_STORE_ERROR", ex.Message));
        }
    }

    private async Task EnsureCollectionAsync(uint vectorSize, CancellationToken ct)
    {
        if (_collectionEnsured)
            return;

        var collections = await _client.ListCollectionsAsync(ct);
        if (collections.All(c => c != CollectionName))
        {
            await _client.CreateCollectionAsync(
                CollectionName,
                new VectorParams { Size = vectorSize, Distance = Distance.Cosine },
                cancellationToken: ct);

            _logger.LogInformation("Created Qdrant collection '{Collection}' with size {Size}",
                CollectionName, vectorSize);
        }

        _collectionEnsured = true;
    }
}
