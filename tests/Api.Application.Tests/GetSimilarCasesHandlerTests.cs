using Api.Application.DTOs;
using Api.Application.Queries;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class GetSimilarCasesHandlerTests
{
    private static readonly TenantId TestTenantId = new(Guid.NewGuid());

    [Fact]
    public async Task HandleAsync_ReturnsTopResults_WithSummaries()
    {
        var docId = Guid.NewGuid();
        var sourceDoc = CreateDocument(docId, TestTenantId, new Dictionary<string, string>
        {
            ["PatientName"] = "Alice",
            ["Condition"] = "Hypertension"
        });
        var hits = new List<SimilarDocumentResult>
        {
            new(Guid.NewGuid(), 0.95, new Dictionary<string, string> { ["PatientName"] = "Alice", ["Type"] = "ChildWelfare" }),
            new(Guid.NewGuid(), 0.88, new Dictionary<string, string> { ["PatientName"] = "Bob", ["Type"] = "AdultProtective" }),
        };
        var ragClient = new StubRagClient(hits);
        var summary = new StubSummaryPort();
        var docRepo = new StubDocumentRepository(sourceDoc);
        var sut = new GetSimilarCasesHandler(ragClient, summary, docRepo);

        var result = await sut.HandleAsync(docId, TestTenantId.Value, 5);

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
        var docRepo = new StubDocumentRepository(null);
        var sut = new GetSimilarCasesHandler(ragClient, summary, docRepo);

        var result = await sut.HandleAsync(Guid.NewGuid(), TestTenantId.Value);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task HandleAsync_WhenSummaryFails_UsesFallback()
    {
        var docId = Guid.NewGuid();
        var sourceDoc = CreateDocument(docId, TestTenantId, new Dictionary<string, string> { ["Name"] = "Test" });
        var hits = new List<SimilarDocumentResult>
        {
            new(Guid.NewGuid(), 0.9, new Dictionary<string, string> { ["Name"] = "Test" }),
        };
        var ragClient = new StubRagClient(hits);
        var summary = new StubSummaryPort(fail: true);
        var docRepo = new StubDocumentRepository(sourceDoc);
        var sut = new GetSimilarCasesHandler(ragClient, summary, docRepo);

        var result = await sut.HandleAsync(docId, TestTenantId.Value);

        Assert.True(result.IsSuccess);
        Assert.Equal("Summary unavailable", result.Value.Items[0].Summary);
    }

    [Fact]
    public async Task HandleAsync_EmptyResults_ReturnsEmptyList()
    {
        var docId = Guid.NewGuid();
        var sourceDoc = CreateDocument(docId, TestTenantId, new Dictionary<string, string>());
        var ragClient = new StubRagClient([]);
        var summary = new StubSummaryPort();
        var docRepo = new StubDocumentRepository(sourceDoc);
        var sut = new GetSimilarCasesHandler(ragClient, summary, docRepo);

        var result = await sut.HandleAsync(docId, TestTenantId.Value);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
    }

    [Fact]
    public async Task HandleAsync_PopulatesSharedFields_WhenValuesMatch()
    {
        var docId = Guid.NewGuid();
        var sourceDoc = CreateDocument(docId, TestTenantId, new Dictionary<string, string>
        {
            ["PatientName"] = "Alice Smith",
            ["Condition"] = "Hypertension",
            ["DOB"] = "1990-01-01"
        });
        var similarDocId = Guid.NewGuid();
        var hits = new List<SimilarDocumentResult>
        {
            new(similarDocId, 0.92, new Dictionary<string, string>
            {
                ["PatientName"] = "Alice Smith",
                ["Condition"] = "Diabetes",
                ["DOB"] = "1990-01-01"
            }),
        };
        var ragClient = new StubRagClient(hits);
        var summary = new StubSummaryPort();
        var docRepo = new StubDocumentRepository(sourceDoc);
        var sut = new GetSimilarCasesHandler(ragClient, summary, docRepo);

        var result = await sut.HandleAsync(docId, TestTenantId.Value);

        Assert.True(result.IsSuccess);
        var item = result.Value.Items[0];
        Assert.Equal(2, item.SharedFields.Count);
        Assert.Equal("Alice Smith", item.SharedFields["PatientName"]);
        Assert.Equal("1990-01-01", item.SharedFields["DOB"]);
        Assert.False(item.SharedFields.ContainsKey("Condition"));
    }

    [Fact]
    public async Task HandleAsync_SharedFields_CaseInsensitiveValueMatch()
    {
        var docId = Guid.NewGuid();
        var sourceDoc = CreateDocument(docId, TestTenantId, new Dictionary<string, string>
        {
            ["PatientName"] = "alice smith"
        });
        var hits = new List<SimilarDocumentResult>
        {
            new(Guid.NewGuid(), 0.9, new Dictionary<string, string>
            {
                ["PatientName"] = "Alice Smith"
            }),
        };
        var ragClient = new StubRagClient(hits);
        var summary = new StubSummaryPort();
        var docRepo = new StubDocumentRepository(sourceDoc);
        var sut = new GetSimilarCasesHandler(ragClient, summary, docRepo);

        var result = await sut.HandleAsync(docId, TestTenantId.Value);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items[0].SharedFields);
        Assert.Equal("Alice Smith", result.Value.Items[0].SharedFields["PatientName"]);
    }

    [Fact]
    public async Task HandleAsync_SharedFields_Empty_WhenNoFieldsMatch()
    {
        var docId = Guid.NewGuid();
        var sourceDoc = CreateDocument(docId, TestTenantId, new Dictionary<string, string>
        {
            ["PatientName"] = "Alice",
            ["Condition"] = "Hypertension"
        });
        var hits = new List<SimilarDocumentResult>
        {
            new(Guid.NewGuid(), 0.85, new Dictionary<string, string>
            {
                ["PatientName"] = "Bob",
                ["Condition"] = "Diabetes"
            }),
        };
        var ragClient = new StubRagClient(hits);
        var summary = new StubSummaryPort();
        var docRepo = new StubDocumentRepository(sourceDoc);
        var sut = new GetSimilarCasesHandler(ragClient, summary, docRepo);

        var result = await sut.HandleAsync(docId, TestTenantId.Value);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items[0].SharedFields);
    }

    [Fact]
    public async Task HandleAsync_SharedFields_Empty_WhenSourceDocNotFound()
    {
        var hits = new List<SimilarDocumentResult>
        {
            new(Guid.NewGuid(), 0.9, new Dictionary<string, string> { ["Name"] = "Alice" }),
        };
        var ragClient = new StubRagClient(hits);
        var summary = new StubSummaryPort();
        var docRepo = new StubDocumentRepository(null);
        var sut = new GetSimilarCasesHandler(ragClient, summary, docRepo);

        var result = await sut.HandleAsync(Guid.NewGuid(), TestTenantId.Value);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items[0].SharedFields);
    }

    // --- Helpers ---

    private static IntakeDocument CreateDocument(
        Guid docId, TenantId tenantId, Dictionary<string, string> fields)
    {
        var doc = IntakeDocument.Submit(tenantId, "test.pdf", "/path/test.pdf");
        doc.MarkProcessing();
        var extractedFields = fields.Select(kv =>
            new ExtractedField(kv.Key, kv.Value, 0.95)).ToList();
        doc.MarkCompleted(extractedFields);
        return doc;
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

    private sealed class StubDocumentRepository : IDocumentRepository
    {
        private readonly IntakeDocument? _document;
        public StubDocumentRepository(IntakeDocument? document) => _document = document;

        public Task<Result<IntakeDocument?>> FindByIdAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(Result<IntakeDocument?>.Success(_document));

        public Task<Result<IntakeDocument?>> FindByIdUnfilteredAsync(DocumentId id, CancellationToken ct = default)
            => Task.FromResult(Result<IntakeDocument?>.Success(_document));

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(Result<IReadOnlyList<IntakeDocument>>.Success(Array.Empty<IntakeDocument>() as IReadOnlyList<IntakeDocument>));

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusAsync(TenantId tenantId, DocumentStatus status, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(Result<IReadOnlyList<IntakeDocument>>.Success(Array.Empty<IntakeDocument>() as IReadOnlyList<IntakeDocument>));

        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusesAsync(TenantId tenantId, IReadOnlyList<DocumentStatus> statuses, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(Result<IReadOnlyList<IntakeDocument>>.Success(Array.Empty<IntakeDocument>() as IReadOnlyList<IntakeDocument>));

        public Task<Result<Unit>> SaveAsync(IntakeDocument document, CancellationToken ct = default)
            => Task.FromResult(Result<Unit>.Success(Unit.Value));

        public Task<Result<Unit>> UpdateAsync(IntakeDocument document, CancellationToken ct = default)
            => Task.FromResult(Result<Unit>.Success(Unit.Value));

        public Task<Result<Unit>> DeleteAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(Result<Unit>.Success(Unit.Value));

        public Task<Result<(IReadOnlyList<IntakeDocument> Items, int TotalCount)>> SearchAsync(
            TenantId tenantId, string? fileNameContains, DocumentStatus? status,
            DateTimeOffset? submittedAfter, DateTimeOffset? submittedBefore,
            string? extractedFieldContains, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(Result<(IReadOnlyList<IntakeDocument> Items, int TotalCount)>.Success(
                (Array.Empty<IntakeDocument>() as IReadOnlyList<IntakeDocument>, 0)));
    }
}
