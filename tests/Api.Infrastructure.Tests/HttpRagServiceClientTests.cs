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
    public async Task FindSimilarByTextAsync_PostsTextToEndpoint()
    {
        var docId = Guid.NewGuid();
        var payload = new
        {
            Data = new[]
            {
                new { DocumentId = docId, Score = 0.92, Metadata = new Dictionary<string, string> { { "Name", "Alice" } } }
            }
        };
        var json = JsonSerializer.Serialize(payload);
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

        var result = await _client.FindSimilarByTextAsync("Child welfare case", Guid.NewGuid(), topK: 5);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(docId, result.Value[0].DocumentId);
        // Verify it was a POST request
        Assert.Equal(HttpMethod.Post, _handler.LastRequest?.Method);
    }

    [Fact]
    public async Task FindSimilarByTextAsync_NonSuccessStatus_ReturnsError()
    {
        _handler.Response = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        var result = await _client.FindSimilarByTextAsync("some text", Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("RAG_SERVICE_ERROR", result.Error.Code);
    }

    [Fact]
    public async Task FindSimilarByTextAsync_HttpException_ReturnsError()
    {
        _handler.ThrowOnSend = new HttpRequestException("Connection refused");

        var result = await _client.FindSimilarByTextAsync("some text", Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("RAG_SERVICE_ERROR", result.Error.Code);
    }

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);
        public HttpRequestException? ThrowOnSend { get; set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (ThrowOnSend is not null)
                throw ThrowOnSend;
            return Task.FromResult(Response);
        }
    }
}
