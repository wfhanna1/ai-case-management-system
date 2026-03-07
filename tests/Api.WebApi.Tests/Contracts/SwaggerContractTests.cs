using System.Text.Json;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using YamlDotNet.Serialization;

namespace Api.WebApi.Tests.Contracts;

/// <summary>
/// Verifies the Swagger/OpenAPI contract exposes the expected endpoints and schemas.
/// Uses WebApplicationFactory to boot the API with stubbed infrastructure.
/// </summary>
public sealed class SwaggerContractTests : IClassFixture<SwaggerContractTests.TestApiFactory>
{
    private readonly HttpClient _client;

    public SwaggerContractTests(TestApiFactory factory)
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

    [Theory]
    [InlineData("/api/Documents", "post")]
    [InlineData("/api/Documents", "get")]
    [InlineData("/api/Documents/{id}", "get")]
    [InlineData("/api/Documents/{id}/file", "get")]
    [InlineData("/api/Documents/recent-activity", "get")]
    [InlineData("/api/auth/login", "post")]
    [InlineData("/api/auth/register", "post")]
    [InlineData("/api/form-templates", "post")]
    [InlineData("/api/form-templates", "get")]
    [InlineData("/api/form-templates/{id}", "get")]
    public async Task Swagger_Contains_Expected_Endpoint(string path, string method)
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty(path, out var pathItem),
            $"Missing path: {path}");
        Assert.True(pathItem.TryGetProperty(method, out _),
            $"Missing method {method.ToUpperInvariant()} on {path}");
    }

    [Theory]
    [InlineData("LoginRequest")]
    [InlineData("RegisterUserRequest")]
    [InlineData("CreateFormTemplateRequest")]
    public async Task Swagger_Contains_Expected_Schema(string schemaName)
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        var schemas = doc.RootElement
            .GetProperty("components")
            .GetProperty("schemas");

        Assert.True(schemas.TryGetProperty(schemaName, out _),
            $"Missing schema: {schemaName}");
    }

    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "put", "post", "delete", "options", "head", "patch", "trace"
    };

    [Fact]
    public async Task Swagger_Matches_Contract_Spec()
    {
        // Load the checked-in OpenAPI spec
        var solutionRoot = GetSolutionRoot();
        var specPath = Path.Combine(solutionRoot, "contracts", "api-service.openapi.yaml");
        var yamlContent = await File.ReadAllTextAsync(specPath);
        var deserializer = new DeserializerBuilder().Build();
        var spec = deserializer.Deserialize<Dictionary<object, object>>(yamlContent);

        // Fetch the runtime Swagger output
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        var swaggerJson = await response.Content.ReadAsStringAsync();
        var swaggerDoc = JsonDocument.Parse(swaggerJson);
        var swaggerPaths = swaggerDoc.RootElement.GetProperty("paths");
        var swaggerSchemas = swaggerDoc.RootElement
            .GetProperty("components")
            .GetProperty("schemas");

        // Build sets of path+method from spec
        var specPaths = (Dictionary<object, object>)spec["paths"];
        var specEndpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pathEntry in specPaths)
        {
            var path = (string)pathEntry.Key;
            var methods = (Dictionary<object, object>)pathEntry.Value;
            foreach (var methodKey in methods.Keys)
            {
                var method = (string)methodKey;
                if (!HttpMethods.Contains(method))
                    continue;
                specEndpoints.Add($"{method.ToUpperInvariant()} {path}");
            }
        }

        // Build sets of path+method from Swagger output
        var swaggerEndpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pathProp in swaggerPaths.EnumerateObject())
        {
            foreach (var methodProp in pathProp.Value.EnumerateObject())
            {
                if (!HttpMethods.Contains(methodProp.Name))
                    continue;
                swaggerEndpoints.Add($"{methodProp.Name.ToUpperInvariant()} {pathProp.Name}");
            }
        }

        // Every spec endpoint must exist in Swagger
        var missingFromSwagger = specEndpoints.Except(swaggerEndpoints, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.True(missingFromSwagger.Count == 0,
            $"Spec defines endpoints missing from Swagger output:\n  {string.Join("\n  ", missingFromSwagger)}");

        // Every Swagger endpoint must exist in spec (catches undocumented endpoints)
        var missingFromSpec = swaggerEndpoints.Except(specEndpoints, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.True(missingFromSpec.Count == 0,
            $"Swagger output contains endpoints not in spec:\n  {string.Join("\n  ", missingFromSpec)}");

        // Validate schema names
        var specComponents = (Dictionary<object, object>)spec["components"];
        var specSchemas = (Dictionary<object, object>)specComponents["schemas"];
        var specSchemaNames = specSchemas.Keys.Cast<string>().ToHashSet();

        var swaggerSchemaNames = new HashSet<string>();
        foreach (var schemaProp in swaggerSchemas.EnumerateObject())
        {
            swaggerSchemaNames.Add(schemaProp.Name);
        }

        var schemasNotInSwagger = specSchemaNames.Except(swaggerSchemaNames).ToList();
        Assert.True(schemasNotInSwagger.Count == 0,
            $"Spec defines schemas missing from Swagger output:\n  {string.Join("\n  ", schemasNotInSwagger)}");

        var schemasNotInSpec = swaggerSchemaNames.Except(specSchemaNames).ToList();
        Assert.True(schemasNotInSpec.Count == 0,
            $"Swagger output contains schemas not in spec:\n  {string.Join("\n  ", schemasNotInSpec)}");
    }

    private static string GetSolutionRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "IntakeDocumentProcessor.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not find IntakeDocumentProcessor.sln walking up from " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// Custom WebApplicationFactory that replaces PostgreSQL, RabbitMQ, and other
    /// infrastructure with test doubles so the API can start without external services.
    /// </summary>
    public sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=test");
            builder.UseSetting("RabbitMQ:Host", "localhost");
            builder.UseSetting("RabbitMQ:Username", "guest");
            builder.UseSetting("RabbitMQ:Password", "guest");
            builder.UseSetting("Jwt:Secret", "TestSecretKeyThatIsAtLeast32BytesLong!");
            builder.UseSetting("Jwt:Issuer", "test-issuer");
            builder.UseSetting("Jwt:Audience", "test-audience");
            builder.UseSetting("Jwt:AccessTokenExpirationMinutes", "15");
            builder.UseSetting("Jwt:RefreshTokenExpirationDays", "7");

            builder.ConfigureServices(services =>
            {
                // Remove real DbContext and replace with SQLite in-memory
                var dbDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<IntakeDbContext>));
                if (dbDescriptor != null) services.Remove(dbDescriptor);

                services.AddDbContext<IntakeDbContext>(options =>
                    options.UseSqlite("DataSource=:memory:"));

                // Remove all MassTransit registrations (service types and implementations)
                var massTransitDescriptors = services
                    .Where(d =>
                        d.ServiceType.FullName?.Contains("MassTransit") == true ||
                        d.ImplementationType?.FullName?.Contains("MassTransit") == true)
                    .ToList();
                foreach (var d in massTransitDescriptors) services.Remove(d);

                // Remove existing IMessageBusPort and replace with no-op
                var busDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IMessageBusPort));
                if (busDescriptor != null) services.Remove(busDescriptor);

                services.AddSingleton<IMessageBusPort, NoOpMessageBus>();

                // Remove the NpgSql health check
                var healthCheckDescriptors = services
                    .Where(d => d.ServiceType.FullName?.Contains("HealthCheck") == true)
                    .ToList();
                foreach (var d in healthCheckDescriptors) services.Remove(d);
                services.AddHealthChecks();

                // Remove the DevelopmentDbSeeder to avoid DB migration issues
                var seederDescriptor = services.SingleOrDefault(
                    d => d.ImplementationType == typeof(DevelopmentDbSeeder));
                if (seederDescriptor != null) services.Remove(seederDescriptor);
            });
        }
    }

    private sealed class NoOpMessageBus : IMessageBusPort
    {
        public Task<Result<Unit>> PublishDocumentUploadedAsync(
            DocumentId documentId, Guid? templateId, TenantId tenantId,
            string fileName, string storageKey, CancellationToken ct = default)
            => Task.FromResult(Result<Unit>.Success(Unit.Value));

    }
}
