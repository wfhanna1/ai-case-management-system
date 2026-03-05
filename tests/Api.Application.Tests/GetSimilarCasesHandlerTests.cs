using Api.Application.DTOs;
using Api.Application.Queries;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class GetSimilarCasesHandlerTests
{
    private static readonly Guid TestTenantId = Guid.NewGuid();

    [Fact]
    public async Task HandleAsync_ReturnsTopResults_WithSummaries()
    {
        var hits = new List<SimilarDocumentResult>
        {
            new(Guid.NewGuid(), 0.95, new Dictionary<string, string> { ["Name"] = "Alice", ["Type"] = "ChildWelfare" }),
            new(Guid.NewGuid(), 0.88, new Dictionary<string, string> { ["Name"] = "Bob", ["Type"] = "AdultProtective" }),
        };
        var ragClient = new StubRagClient(hits);
        var summary = new StubSummaryPort();
        var sut = new GetSimilarCasesHandler(ragClient, summary);

        var result = await sut.HandleAsync(Guid.NewGuid(), TestTenantId, 5);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal(0.95, result.Value.Items[0].Score);
        Assert.Contains("Alice", result.Value.Items[0].Summary);
    }

    [Fact]
    public async Task HandleAsync_WhenRagFails_ReturnsFailure()
    {
        var ragClient = new StubRagClient(null);
        var summary = new StubSummaryPort();
        var sut = new GetSimilarCasesHandler(ragClient, summary);

        var result = await sut.HandleAsync(Guid.NewGuid(), TestTenantId);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task HandleAsync_WhenSummaryFails_UsesFallback()
    {
        var hits = new List<SimilarDocumentResult>
        {
            new(Guid.NewGuid(), 0.9, new Dictionary<string, string> { ["Name"] = "Test" }),
        };
        var ragClient = new StubRagClient(hits);
        var summary = new StubSummaryPort(fail: true);
        var sut = new GetSimilarCasesHandler(ragClient, summary);

        var result = await sut.HandleAsync(Guid.NewGuid(), TestTenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Summary unavailable", result.Value.Items[0].Summary);
    }

    [Fact]
    public async Task HandleAsync_EmptyResults_ReturnsEmptyList()
    {
        var ragClient = new StubRagClient([]);
        var summary = new StubSummaryPort();
        var sut = new GetSimilarCasesHandler(ragClient, summary);

        var result = await sut.HandleAsync(Guid.NewGuid(), TestTenantId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
    }

    // --- Test doubles ---

    private sealed class StubRagClient : IRagServiceClient
    {
        private readonly IReadOnlyList<SimilarDocumentResult>? _results;
        public StubRagClient(IReadOnlyList<SimilarDocumentResult>? results) => _results = results;

        public Task<Result<IReadOnlyList<SimilarDocumentResult>>> FindSimilarAsync(
            Guid documentId, Guid tenantId, int topK = 5, CancellationToken ct = default)
        {
            if (_results is null)
                return Task.FromResult(Result<IReadOnlyList<SimilarDocumentResult>>.Failure(
                    new Error("RAG_ERROR", "RAG service unavailable")));
            return Task.FromResult(Result<IReadOnlyList<SimilarDocumentResult>>.Success(_results));
        }
    }

    private sealed class StubSummaryPort : ISummaryPort
    {
        private readonly bool _fail;
        public StubSummaryPort(bool fail = false) => _fail = fail;

        public Task<Result<string>> GenerateSummaryAsync(
            Dictionary<string, string> fields, CancellationToken ct = default)
        {
            if (_fail)
                return Task.FromResult(Result<string>.Failure(new Error("FAIL", "Summary failed")));
            var summary = string.Join(", ", fields.Select(kv => $"{kv.Key}: {kv.Value}"));
            return Task.FromResult(Result<string>.Success(summary));
        }
    }
}
