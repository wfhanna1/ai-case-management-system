using Api.Application.DTOs;
using Api.Application.Queries;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class GetSimilarCasesHandlerTests
{
    private static readonly Guid TestTenantId = Guid.NewGuid();

    [Fact]
    public async Task HandleAsync_LoadsDocumentAndSearchesByText()
    {
        var docId = Guid.NewGuid();
        var doc = CreateDocumentWithFields(docId, TestTenantId, new Dictionary<string, string>
        {
            ["ChildName"] = "Alice Thompson",
            ["ReasonForReferral"] = "Physical abuse suspected"
        });
        var docRepo = new StubDocumentRepository(doc);
        var hits = new List<SimilarDocumentResult>
        {
            new(Guid.NewGuid(), 0.95, new Dictionary<string, string> { ["ChildName"] = "Bob" }),
            new(Guid.NewGuid(), 0.88, new Dictionary<string, string> { ["ChildName"] = "Carol" }),
        };
        var ragClient = new StubRagClient(hits);
        var summary = new StubSummaryPort();
        var sut = new GetSimilarCasesHandler(ragClient, summary, docRepo);

        var result = await sut.HandleAsync(docId, TestTenantId, 5);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal(0.95, result.Value.Items[0].Score);
        // Verify text was sent (not document ID)
        Assert.NotNull(ragClient.LastTextSent);
        Assert.Contains("Alice Thompson", ragClient.LastTextSent);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentNotFound_ReturnsFailure()
    {
        var docRepo = new StubDocumentRepository(null);
        var ragClient = new StubRagClient([]);
        var summary = new StubSummaryPort();
        var sut = new GetSimilarCasesHandler(ragClient, summary, docRepo);

        var result = await sut.HandleAsync(Guid.NewGuid(), TestTenantId, 5);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentHasNoFields_ReturnsFailure()
    {
        var docId = Guid.NewGuid();
        var doc = CreateDocumentWithFields(docId, TestTenantId, new Dictionary<string, string>());
        var docRepo = new StubDocumentRepository(doc);
        var ragClient = new StubRagClient([]);
        var summary = new StubSummaryPort();
        var sut = new GetSimilarCasesHandler(ragClient, summary, docRepo);

        var result = await sut.HandleAsync(docId, TestTenantId, 5);

        Assert.True(result.IsFailure);
        Assert.Equal("NO_CONTENT", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenRagFails_ReturnsFailure()
    {
        var docId = Guid.NewGuid();
        var doc = CreateDocumentWithFields(docId, TestTenantId, new Dictionary<string, string>
        {
            ["Name"] = "Test"
        });
        var docRepo = new StubDocumentRepository(doc);
        var ragClient = new StubRagClient(null);
        var summary = new StubSummaryPort();
        var sut = new GetSimilarCasesHandler(ragClient, summary, docRepo);

        var result = await sut.HandleAsync(docId, TestTenantId);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task HandleAsync_WhenSummaryFails_UsesFallback()
    {
        var docId = Guid.NewGuid();
        var doc = CreateDocumentWithFields(docId, TestTenantId, new Dictionary<string, string>
        {
            ["Name"] = "Test"
        });
        var docRepo = new StubDocumentRepository(doc);
        var hits = new List<SimilarDocumentResult>
        {
            new(Guid.NewGuid(), 0.9, new Dictionary<string, string> { ["Name"] = "Test" }),
        };
        var ragClient = new StubRagClient(hits);
        var summary = new StubSummaryPort(fail: true);
        var sut = new GetSimilarCasesHandler(ragClient, summary, docRepo);

        var result = await sut.HandleAsync(docId, TestTenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Summary unavailable", result.Value.Items[0].Summary);
    }

    [Fact]
    public async Task HandleAsync_ExcludesSelfFromResults()
    {
        var docId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var doc = CreateDocumentWithFields(docId, TestTenantId, new Dictionary<string, string>
        {
            ["Name"] = "Test"
        });
        var docRepo = new StubDocumentRepository(doc);
        var hits = new List<SimilarDocumentResult>
        {
            new(docId, 1.0, new Dictionary<string, string>()),
            new(otherId, 0.9, new Dictionary<string, string> { ["Name"] = "Bob" }),
        };
        var ragClient = new StubRagClient(hits);
        var summary = new StubSummaryPort();
        var sut = new GetSimilarCasesHandler(ragClient, summary, docRepo);

        var result = await sut.HandleAsync(docId, TestTenantId, 5);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal(otherId, result.Value.Items[0].DocumentId);
    }

    [Fact]
    public async Task HandleAsync_UsesCorrectedValueWhenAvailable()
    {
        var docId = Guid.NewGuid();
        var doc = CreateDocumentWithFields(docId, TestTenantId, new Dictionary<string, string>
        {
            ["Name"] = "Original"
        });
        // Add a correction
        doc.MarkPendingReview();
        doc.StartReview(new UserId(Guid.NewGuid()));
        doc.CorrectField("Name", "Corrected", new UserId(Guid.NewGuid()));

        var docRepo = new StubDocumentRepository(doc);
        var ragClient = new StubRagClient([]);
        var summary = new StubSummaryPort();
        var sut = new GetSimilarCasesHandler(ragClient, summary, docRepo);

        var result = await sut.HandleAsync(docId, TestTenantId, 5);

        Assert.True(result.IsSuccess);
        Assert.Contains("Corrected", ragClient.LastTextSent!);
    }

    // --- Helpers ---

    private static IntakeDocument CreateDocumentWithFields(
        Guid docId, Guid tenantId, Dictionary<string, string> fields)
    {
        var doc = IntakeDocument.Submit(
            new TenantId(tenantId), "test.pdf", $"uploads/{docId}.pdf");

        if (fields.Count > 0)
        {
            doc.MarkProcessing();
            var extractedFields = fields.Select(kv =>
                new ExtractedField(kv.Key, kv.Value, 0.95)).ToList();
            doc.MarkCompleted(extractedFields);
        }

        return doc;
    }

    // --- Test doubles ---

    private sealed class StubDocumentRepository : IDocumentRepository
    {
        private readonly IntakeDocument? _document;
        public StubDocumentRepository(IntakeDocument? document) => _document = document;

        public Task<Result<IntakeDocument?>> FindByIdAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(Result<IntakeDocument?>.Success(_document));

        public Task<Result<IntakeDocument?>> FindByIdUnfilteredAsync(DocumentId id, CancellationToken ct = default)
            => Task.FromResult(Result<IntakeDocument?>.Success(_document));
        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusAsync(TenantId tenantId, DocumentStatus status, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<(IReadOnlyList<IntakeDocument> Items, int TotalCount)>> ListByStatusesAsync(TenantId tenantId, IReadOnlyList<DocumentStatus> statuses, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<Unit>> SaveAsync(IntakeDocument document, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<Unit>> UpdateAsync(IntakeDocument document, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<Unit>> DeleteAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<(IReadOnlyList<IntakeDocument> Items, int TotalCount)>> SearchAsync(
            TenantId tenantId, string? fileNameContains, DocumentStatus? status,
            DateTimeOffset? submittedAfter, DateTimeOffset? submittedBefore,
            string? extractedFieldContains, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Result<(int PendingReview, int ProcessedToday, TimeSpan AverageProcessingTime)>> GetStatsAsync(
            TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private sealed class StubRagClient : IRagServiceClient
    {
        private readonly IReadOnlyList<SimilarDocumentResult>? _results;
        public string? LastTextSent { get; private set; }

        public StubRagClient(IReadOnlyList<SimilarDocumentResult>? results) => _results = results;

        public Task<Result<IReadOnlyList<SimilarDocumentResult>>> FindSimilarByTextAsync(
            string textContent, Guid tenantId, int topK = 5, CancellationToken ct = default)
        {
            LastTextSent = textContent;
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
