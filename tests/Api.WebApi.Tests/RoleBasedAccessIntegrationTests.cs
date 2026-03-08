using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api.WebApi.Tests;

/// <summary>
/// Integration tests that hit the real running API (Docker on port 5003) to verify
/// the full auth pipeline: login -> JWT -> tenant middleware -> authorization policy -> controller.
/// These tests require `docker compose up` to be running with seeded demo data.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RoleBasedAccessIntegrationTests : IAsyncLifetime
{
    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri("http://localhost:5003"),
        Timeout = TimeSpan.FromSeconds(5)
    };

    public async Task InitializeAsync()
    {
        try
        {
            await Client.GetAsync("/health");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(
                "Integration tests require the API at http://localhost:5003. " +
                "Run `docker compose up` first.", ex);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<string> LoginAsync(string email, string password = "Demo123!")
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("data").GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage AuthenticatedGet(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task IntakeWorker_can_access_cases_endpoint()
    {
        var token = await LoginAsync("worker@alpha.demo");
        var response = await Client.SendAsync(AuthenticatedGet("/api/cases", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Reviewer_can_access_cases_endpoint()
    {
        var token = await LoginAsync("reviewer@alpha.demo");
        var response = await Client.SendAsync(AuthenticatedGet("/api/cases", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_access_cases_endpoint()
    {
        var token = await LoginAsync("admin@alpha.demo");
        var response = await Client.SendAsync(AuthenticatedGet("/api/cases", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_request_returns_401()
    {
        var response = await Client.GetAsync("/api/cases");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cross_tenant_worker_sees_different_cases_than_other_tenant()
    {
        var alphaToken = await LoginAsync("worker@alpha.demo");
        var betaToken = await LoginAsync("worker@beta.demo");

        var alphaResponse = await Client.SendAsync(AuthenticatedGet("/api/cases", alphaToken));
        var betaResponse = await Client.SendAsync(AuthenticatedGet("/api/cases", betaToken));

        Assert.Equal(HttpStatusCode.OK, alphaResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, betaResponse.StatusCode);

        var alphaJson = await alphaResponse.Content.ReadFromJsonAsync<JsonElement>();
        var betaJson = await betaResponse.Content.ReadFromJsonAsync<JsonElement>();

        var alphaIds = alphaJson.GetProperty("data").GetProperty("items")
            .EnumerateArray().Select(i => i.GetProperty("id").GetString()).ToHashSet();
        var betaIds = betaJson.GetProperty("data").GetProperty("items")
            .EnumerateArray().Select(i => i.GetProperty("id").GetString()).ToHashSet();

        // Tenants must not share any case IDs
        Assert.Empty(alphaIds.Intersect(betaIds));
    }
}
