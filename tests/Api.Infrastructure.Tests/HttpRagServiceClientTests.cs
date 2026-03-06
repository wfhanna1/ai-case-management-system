using System.Net;
using System.Text.Json;
using Api.Infrastructure.RagClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Infrastructure.Tests;

public sealed class HttpRagServiceClientTests
{
    private readonly StubHttpHandler _handler = new();
    private readonly HttpRagServiceClient _client;

    public HttpRagServiceClientTests()
    {
        var httpClient = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost") };
        _client = new HttpRagServiceClient(httpClient, NullLogger<HttpRagServiceClient>.Instance);
    }

    [Fact]
    public async Task FindSimilarAsync_NonSuccessStatus_ReturnsRagServiceError()
    {
        _handler.Response = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        var result = await _client.FindSimilarAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("RAG_SERVICE_ERROR", result.Error.Code);
        Assert.Contains("500", result.Error.Message);
    }

    [Fact]
    public async Task FindSimilarAsync_NullDataInResponse_ReturnsEmptyList()
    {
        var json = JsonSerializer.Serialize(new { Data = (object?)null });
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

        var result = await _client.FindSimilarAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task FindSimilarAsync_ValidResponse_MapsCorrectly()
    {
        var docId = Guid.NewGuid();
        var payload = new
        {
            Data = new[]
            {
                new { DocumentId = docId, Score = 0.95, Metadata = new Dictionary<string, string> { { "type", "welfare" } } },
                new { DocumentId = Guid.NewGuid(), Score = 0.80, Metadata = (Dictionary<string, string>?)null }
            }
        };
        var json = JsonSerializer.Serialize(payload);
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

        var result = await _client.FindSimilarAsync(Guid.NewGuid(), Guid.NewGuid(), topK: 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(docId, result.Value[0].DocumentId);
        Assert.Equal(0.95, result.Value[0].Score);
        Assert.Equal("welfare", result.Value[0].Metadata["type"]);
        Assert.Empty(result.Value[1].Metadata);
    }

    [Fact]
    public async Task FindSimilarAsync_HttpRequestException_ReturnsRagServiceError()
    {
        _handler.ThrowOnSend = new HttpRequestException("Connection refused");

        var result = await _client.FindSimilarAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("RAG_SERVICE_ERROR", result.Error.Code);
        Assert.Contains("Connection refused", result.Error.Message);
    }

    [Fact]
    public async Task FindSimilarAsync_NotFound_ReturnsRagServiceError()
    {
        _handler.Response = new HttpResponseMessage(HttpStatusCode.NotFound);

        var result = await _client.FindSimilarAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("RAG_SERVICE_ERROR", result.Error.Code);
        Assert.Contains("404", result.Error.Message);
    }

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);
        public HttpRequestException? ThrowOnSend { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (ThrowOnSend is not null)
                throw ThrowOnSend;
            return Task.FromResult(Response);
        }
    }
}
