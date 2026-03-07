using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RagService.Domain.Ports;
using SharedKernel;
using YamlDotNet.Serialization;

namespace RagService.Tests.Contracts;

/// <summary>
/// Verifies the Swagger/OpenAPI spec generated at runtime matches the committed
/// contract file at contracts/rag-service.openapi.yaml. Any drift causes a test failure,
/// ensuring the contract stays in sync with the implementation.
/// </summary>
public sealed class OpenApiContractTests : IClassFixture<OpenApiContractTests.TestRagFactory>
{
    private readonly HttpClient _client;

    public OpenApiContractTests(TestRagFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Swagger_Returns_Valid_Json()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        Assert.Equal("3.0.1", doc.RootElement.GetProperty("openapi").GetString());
    }

    [Fact]
    public async Task Swagger_Matches_Contract_Spec()
    {
        // Load the committed YAML contract
        var root = GetSolutionRoot();
        var yamlPath = Path.Combine(root, "contracts", "rag-service.openapi.yaml");
        var yamlText = await File.ReadAllTextAsync(yamlPath);

        var deserializer = new DeserializerBuilder().Build();
        var yamlDoc = deserializer.Deserialize<Dictionary<string, object>>(yamlText);
        var yamlPaths = (Dictionary<object, object>)yamlDoc["paths"];

        // Collect expected path+method pairs from the YAML spec
        var expectedEndpoints = new HashSet<string>();
        foreach (var (path, methods) in yamlPaths)
        {
            var methodDict = (Dictionary<object, object>)methods;
            foreach (var method in methodDict.Keys)
            {
                expectedEndpoints.Add($"{method}:{path}");
            }
        }

        // Fetch the runtime-generated Swagger JSON
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        var jsonContent = await response.Content.ReadAsStringAsync();
        var swaggerDoc = JsonDocument.Parse(jsonContent);
        var swaggerPaths = swaggerDoc.RootElement.GetProperty("paths");

        // Collect actual path+method pairs from swagger.json
        var actualEndpoints = new HashSet<string>();
        foreach (var pathProp in swaggerPaths.EnumerateObject())
        {
            foreach (var methodProp in pathProp.Value.EnumerateObject())
            {
                actualEndpoints.Add($"{methodProp.Name}:{pathProp.Name}");
            }
        }

        // Bidirectional comparison
        var missingFromSwagger = expectedEndpoints.Except(actualEndpoints).ToList();
        var extraInSwagger = actualEndpoints.Except(expectedEndpoints).ToList();

        var errors = new List<string>();
        if (missingFromSwagger.Count > 0)
            errors.Add($"In contract but missing from Swagger: {string.Join(", ", missingFromSwagger)}");
        if (extraInSwagger.Count > 0)
            errors.Add($"In Swagger but missing from contract: {string.Join(", ", extraInSwagger)}");

        Assert.True(errors.Count == 0,
            $"OpenAPI contract drift detected:\n{string.Join("\n", errors)}");
    }

    private static string GetSolutionRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "IntakeDocumentProcessor.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not find solution root (looked for IntakeDocumentProcessor.sln).");
    }

    /// <summary>
    /// Custom WebApplicationFactory that replaces RabbitMQ, Qdrant, and other
    /// infrastructure with test doubles so the service can start without external services.
    /// </summary>
    public sealed class TestRagFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.UseSetting("RabbitMQ:Host", "localhost");
            builder.UseSetting("RabbitMQ:Username", "guest");
            builder.UseSetting("RabbitMQ:Password", "guest");
            builder.UseSetting("Qdrant:Host", "localhost");
            builder.UseSetting("Qdrant:Port", "6334");
            builder.UseSetting("Qdrant:HttpPort", "6333");

            builder.ConfigureServices(services =>
            {
                // Remove all MassTransit registrations
                var massTransitDescriptors = services
                    .Where(d =>
                        d.ServiceType.FullName?.Contains("MassTransit") == true ||
                        d.ImplementationType?.FullName?.Contains("MassTransit") == true)
                    .ToList();
                foreach (var d in massTransitDescriptors) services.Remove(d);

                // Remove health check registrations and re-add basic health checks
                var healthCheckDescriptors = services
                    .Where(d => d.ServiceType.FullName?.Contains("HealthCheck") == true)
                    .ToList();
                foreach (var d in healthCheckDescriptors) services.Remove(d);
                services.AddHealthChecks();

                // Remove the RagDataSeeder hosted service
                var seederDescriptor = services.SingleOrDefault(
                    d => d.ImplementationType?.Name == "RagDataSeeder");
                if (seederDescriptor != null) services.Remove(seederDescriptor);

                // Remove the RagWorkerService hosted service
                var workerDescriptor = services.SingleOrDefault(
                    d => d.ImplementationType?.Name == "RagWorkerService");
                if (workerDescriptor != null) services.Remove(workerDescriptor);

                // Remove QdrantClient registration
                var qdrantDescriptor = services.SingleOrDefault(
                    d => d.ServiceType.FullName?.Contains("QdrantClient") == true);
                if (qdrantDescriptor != null) services.Remove(qdrantDescriptor);

                // Replace IEmbeddingPort with a no-op stub
                var embeddingDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IEmbeddingPort));
                if (embeddingDescriptor != null) services.Remove(embeddingDescriptor);
                services.AddSingleton<IEmbeddingPort, StubEmbeddingPort>();

                // Replace IVectorStorePort with a no-op stub
                var vectorDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IVectorStorePort));
                if (vectorDescriptor != null) services.Remove(vectorDescriptor);
                services.AddSingleton<IVectorStorePort, StubVectorStorePort>();
            });
        }
    }

    private sealed class StubEmbeddingPort : IEmbeddingPort
    {
        public Task<Result<float[]>> GenerateEmbeddingAsync(
            string text, CancellationToken ct = default)
            => Task.FromResult(Result<float[]>.Success(new float[384]));
    }

    private sealed class StubVectorStorePort : IVectorStorePort
    {
        public Task<Result<Unit>> UpsertAsync(
            Guid documentId, Guid tenantId, float[] embedding,
            Dictionary<string, string> metadata, CancellationToken ct = default)
            => Task.FromResult(Result<Unit>.Success(Unit.Value));

        public Task<Result<IReadOnlyList<SearchHit>>> SearchAsync(
            Guid tenantId, float[] queryEmbedding, int topK = 5,
            CancellationToken ct = default)
            => Task.FromResult(Result<IReadOnlyList<SearchHit>>.Success(
                Array.Empty<SearchHit>()));

        public Task<Result<float[]>> GetEmbeddingAsync(
            Guid documentId, CancellationToken ct = default)
            => Task.FromResult(Result<float[]>.Success(new float[384]));
    }
}
