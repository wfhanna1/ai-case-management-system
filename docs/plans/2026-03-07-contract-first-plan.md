# Contract-First API Specs Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add OpenAPI and AsyncAPI spec files as the source of truth for all service boundaries, with tests that fail when code drifts from the specs.

**Architecture:** Three YAML spec files in `contracts/` (two OpenAPI for HTTP services, one AsyncAPI for messaging). Test-time validation compares runtime output against the checked-in specs. No code generation.

**Tech Stack:** OpenAPI 3.0.3, AsyncAPI 2.6.0, YamlDotNet (for parsing specs in tests), xUnit, WebApplicationFactory, System.Text.Json.

---

### Task 1: Add YamlDotNet to Test Projects

**Files:**
- Modify: `tests/Api.WebApi.Tests/Api.WebApi.Tests.csproj`
- Modify: `tests/Messaging.Tests/Messaging.Tests.csproj`

**Step 1: Add YamlDotNet package to Api.WebApi.Tests**

Run:
```bash
dotnet add tests/Api.WebApi.Tests package YamlDotNet --version 16.3.0
```

**Step 2: Add YamlDotNet package to Messaging.Tests**

Run:
```bash
dotnet add tests/Messaging.Tests package YamlDotNet --version 16.3.0
```

**Step 3: Verify build**

Run: `dotnet build tests/Api.WebApi.Tests && dotnet build tests/Messaging.Tests`
Expected: Build Succeeded

**Step 4: Commit**

```bash
git add tests/Api.WebApi.Tests/Api.WebApi.Tests.csproj tests/Messaging.Tests/Messaging.Tests.csproj
git commit -m "chore: add YamlDotNet to test projects for contract validation"
```

---

### Task 2: Write the ApiService OpenAPI Spec

**Files:**
- Create: `contracts/api-service.openapi.yaml`

**Step 1: Create the spec file**

Write the full OpenAPI 3.0.3 spec covering all 18 ApiService endpoints. The spec must include:

- `info` with title "Handwritten Intake Document Processor API", version "v1"
- `servers` with url `/`
- JWT Bearer security scheme in `components/securityDefinitions`
- All paths with methods, parameters, request bodies, and response codes
- All DTO schemas in `components/schemas`
- Use `$ref` for schema reuse

Endpoints to include (match casing from Swashbuckle output -- Swashbuckle lowercases route templates):

Auth (no auth required):
- `POST /api/auth/register` -- RegisterUserRequest -> ApiResponse<AuthResponse> (201, 400, 422)
- `POST /api/auth/login` -- LoginRequest -> ApiResponse<AuthResponse> (200, 401, 422)
- `POST /api/auth/refresh` -- RefreshTokenRequest -> ApiResponse<AuthResponse> (200, 401, 422)

Documents (JWT required):
- `POST /api/Documents` -- multipart/form-data (file + templateId) -> ApiResponse<DocumentDto> (201, 400, 401, 403)
- `GET /api/Documents` -- query: page, pageSize -> ApiResponse<IReadOnlyList<DocumentDto>> (200, 401, 500)
- `GET /api/Documents/{id}` -- path: id (uuid) -> ApiResponse<DocumentDto> (200, 401, 404, 500)
- `GET /api/Documents/{id}/file` -- path: id (uuid) -> binary stream (200, 401, 404)
- `GET /api/Documents/search` -- query: fileName, status, from, to, fieldValue, page, pageSize -> ApiResponse<SearchDocumentsResultDto> (200, 401, 500)
- `GET /api/Documents/stats` -- -> ApiResponse<DashboardStatsDto> (200, 401, 500)

Cases (JWT required):
- `GET /api/cases` -- query: page, pageSize -> ApiResponse<SearchCasesResultDto> (200, 401, 500)
- `GET /api/cases/{id}` -- path: id (uuid) -> ApiResponse<CaseDetailDto> (200, 401, 404, 500)
- `GET /api/cases/search` -- query: q, status, from, to, page, pageSize -> ApiResponse<SearchCasesResultDto> (200, 401, 500)

Form Templates (JWT required):
- `POST /api/form-templates` -- CreateFormTemplateRequest -> ApiResponse<FormTemplateDto> (201, 400, 401, 403, 422)
- `GET /api/form-templates` -- -> ApiResponse<IReadOnlyList<FormTemplateDto>> (200, 401, 500)
- `GET /api/form-templates/{id}` -- path: id (uuid) -> ApiResponse<FormTemplateDto> (200, 401, 404, 500)

Reviews (JWT required, RequireReviewer):
- `GET /api/reviews/pending` -- query: page, pageSize -> ApiResponse<IReadOnlyList<ReviewDocumentDto>> (200, 401, 403, 500)
- `GET /api/reviews/{documentId}` -- path: documentId (uuid) -> ApiResponse<ReviewDocumentDto> (200, 401, 403, 404, 500)
- `POST /api/reviews/{documentId}/start` -- no body -> ApiResponse<EmptyResponse> (200, 401, 403, 404, 409, 500)
- `POST /api/reviews/{documentId}/correct-field` -- CorrectFieldRequest -> ApiResponse<EmptyResponse> (200, 401, 403, 404, 409, 422, 500)
- `POST /api/reviews/{documentId}/finalize` -- no body -> ApiResponse<EmptyResponse> (200, 401, 403, 404, 409, 500)
- `GET /api/reviews/{documentId}/audit` -- -> ApiResponse<IReadOnlyList<AuditLogEntryDto>> (200, 401, 403, 500)
- `GET /api/reviews/{documentId}/similar-cases` -- -> ApiResponse<SimilarCasesResultDto> (200, 401, 403, 500)

Schemas to define in `components/schemas`:
- ApiResponse (generic wrapper with `success`, `data`, `error` fields)
- ApiError (with `code`, `message`, `details`)
- EmptyResponse (empty object)
- RegisterUserRequest, LoginRequest, RefreshTokenRequest, AuthResponse
- DocumentDto, SearchDocumentsResultDto, DashboardStatsDto
- CaseDto, CaseDetailDto, SearchCasesResultDto
- FormTemplateDto, TemplateFieldDto, CreateFormTemplateRequest
- ReviewDocumentDto, ExtractedFieldDto, CorrectFieldRequest, AuditLogEntryDto
- SimilarCaseDto, SimilarCasesResultDto

**Step 2: Validate YAML syntax**

Run: `dotnet script -e "var yaml = new YamlDotNet.Serialization.Deserializer(); yaml.Deserialize<object>(System.IO.File.ReadAllText(\"contracts/api-service.openapi.yaml\"));"` or simply rely on the test in Task 4 to validate.

**Step 3: Commit**

```bash
git add contracts/api-service.openapi.yaml
git commit -m "feat: add ApiService OpenAPI 3.0.3 contract spec"
```

---

### Task 3: Write the RagService OpenAPI Spec

**Files:**
- Create: `contracts/rag-service.openapi.yaml`

**Step 1: Create the spec file**

Write the OpenAPI 3.0.3 spec covering the 2 RagService minimal API endpoints:

- `GET /api/similar` -- query: documentId (uuid), tenantId (uuid), topK (integer, 1-50) -> `{ data: SearchHit[] }` (200, 400, 500)
- `POST /api/similar-by-text` -- SimilarByTextRequest -> `{ data: SearchHit[] }` (200, 400, 500)

Schemas:
- SimilarByTextRequest: text (string), tenantId (uuid), topK (integer, default 5)
- SearchHit: documentId (uuid), score (number/double), metadata (object, additionalProperties: string)
- SimilarResponse: data (array of SearchHit)

Note: RagService does NOT use the ApiResponse<T> envelope. It returns `{ data: [...] }` directly.

**Step 2: Commit**

```bash
git add contracts/rag-service.openapi.yaml
git commit -m "feat: add RagService OpenAPI 3.0.3 contract spec"
```

---

### Task 4: Write the AsyncAPI Messaging Spec

**Files:**
- Create: `contracts/messaging.asyncapi.yaml`

**Step 1: Create the spec file**

Write the AsyncAPI 2.6.0 spec covering all 4 MassTransit events. Use AMQP protocol binding for RabbitMQ.

Channels (queue names follow `{service}-{event}` convention):

- `ocrworker-document-uploaded`:
  - subscribe: OcrWorkerService consumes DocumentUploadedEvent
  - publish: ApiService publishes DocumentUploadedEvent

- `apiservice-document-processed`:
  - subscribe: ApiService consumes DocumentProcessedEvent
  - publish: OcrWorkerService publishes DocumentProcessedEvent

- `ragservice-embedding-requested`:
  - subscribe: RagService consumes EmbeddingRequestedEvent
  - publish: ApiService publishes EmbeddingRequestedEvent

- `apiservice-embedding-completed`:
  - subscribe: (none currently)
  - publish: RagService publishes EmbeddingCompletedEvent

Message schemas (match the C# record properties exactly):

DocumentUploadedEvent:
- documentId: string (uuid format)
- templateId: string (uuid format, nullable)
- tenantId: string (uuid format)
- fileName: string
- storageKey: string
- uploadedAt: string (date-time format)

DocumentProcessedEvent:
- documentId: string (uuid format)
- tenantId: string (uuid format)
- extractedFields: object (additionalProperties: ExtractedFieldResult)
- processedAt: string (date-time format)

EmbeddingRequestedEvent:
- documentId: string (uuid format)
- tenantId: string (uuid format)
- textContent: string
- fieldValues: object (additionalProperties: string)
- requestedAt: string (date-time format)

EmbeddingCompletedEvent:
- documentId: string (uuid format)
- tenantId: string (uuid format)
- completedAt: string (date-time format)

ExtractedFieldResult:
- fieldName: string
- value: string
- confidence: number (double format)

Use camelCase property names (matches MassTransit JSON serialization with `JsonNamingPolicy.CamelCase`).

**Step 2: Commit**

```bash
git add contracts/messaging.asyncapi.yaml
git commit -m "feat: add AsyncAPI 2.6.0 messaging contract spec"
```

---

### Task 5: Write OpenAPI Contract Drift Test for ApiService

**Files:**
- Modify: `tests/Api.WebApi.Tests/Contracts/SwaggerContractTests.cs`

This replaces the existing endpoint/schema spot-checks with a comprehensive drift test that compares the runtime Swagger output against the checked-in spec.

**Step 1: Write the failing test**

Add a new test method `Swagger_Matches_Contract_Spec` to the existing `SwaggerContractTests` class:

```csharp
[Fact]
public async Task Swagger_Matches_Contract_Spec()
{
    // Load the checked-in contract spec
    var specPath = Path.Combine(
        GetSolutionRoot(), "contracts", "api-service.openapi.yaml");
    var yamlContent = await File.ReadAllTextAsync(specPath);
    var deserializer = new YamlDotNet.Serialization.Deserializer();
    var specDoc = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

    // Get runtime Swagger output
    var response = await _client.GetAsync("/swagger/v1/swagger.json");
    response.EnsureSuccessStatusCode();
    var swaggerJson = await response.Content.ReadAsStringAsync();
    var swaggerDoc = JsonDocument.Parse(swaggerJson);

    // Compare paths: every path+method in the spec must exist in Swagger
    var specPaths = (Dictionary<object, object>)specDoc["paths"];
    var swaggerPaths = swaggerDoc.RootElement.GetProperty("paths");

    foreach (var pathEntry in specPaths)
    {
        var path = (string)pathEntry.Key;
        Assert.True(swaggerPaths.TryGetProperty(path, out var swaggerPathItem),
            $"Contract spec defines path '{path}' but it is missing from Swagger output");

        var methods = (Dictionary<object, object>)pathEntry.Value;
        foreach (var method in methods.Keys)
        {
            var methodStr = (string)method;
            if (methodStr == "parameters") continue; // skip path-level params
            Assert.True(swaggerPathItem.TryGetProperty(methodStr, out _),
                $"Contract spec defines {methodStr.ToUpperInvariant()} {path} but it is missing from Swagger output");
        }
    }

    // Compare paths: every path+method in Swagger must exist in the spec (no undocumented endpoints)
    foreach (var swaggerPath in swaggerPaths.EnumerateObject())
    {
        Assert.True(specPaths.ContainsKey(swaggerPath.Name),
            $"Swagger exposes path '{swaggerPath.Name}' that is not in the contract spec. Add it to contracts/api-service.openapi.yaml");

        var specMethods = (Dictionary<object, object>)specPaths[swaggerPath.Name];
        foreach (var swaggerMethod in swaggerPath.Value.EnumerateObject())
        {
            if (swaggerMethod.Name == "parameters") continue;
            Assert.True(specMethods.ContainsKey(swaggerMethod.Name),
                $"Swagger exposes {swaggerMethod.Name.ToUpperInvariant()} {swaggerPath.Name} that is not in the contract spec");
        }
    }

    // Compare schemas: every schema in the spec must exist in Swagger
    var specComponents = (Dictionary<object, object>)specDoc["components"];
    var specSchemas = (Dictionary<object, object>)specComponents["schemas"];
    var swaggerSchemas = swaggerDoc.RootElement
        .GetProperty("components").GetProperty("schemas");

    foreach (var schemaName in specSchemas.Keys)
    {
        Assert.True(swaggerSchemas.TryGetProperty((string)schemaName, out _),
            $"Contract spec defines schema '{schemaName}' but it is missing from Swagger output");
    }
}

private static string GetSolutionRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null && !File.Exists(Path.Combine(dir.FullName, "IntakeDocumentProcessor.sln")))
        dir = dir.Parent;
    return dir?.FullName ?? throw new InvalidOperationException("Could not find solution root");
}
```

Add `using YamlDotNet.Serialization;` at the top of the file.

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/Api.WebApi.Tests --filter "DisplayName~Swagger_Matches_Contract_Spec" -v n`
Expected: PASS (the spec was authored to match the existing endpoints)

If it fails, adjust the spec in `contracts/api-service.openapi.yaml` to match exactly what Swashbuckle generates (casing, path parameter format, etc.).

**Step 3: Keep existing tests**

The existing `Swagger_Returns_Valid_Json`, `Swagger_Contains_Expected_Endpoint`, and `Swagger_Contains_Expected_Schema` tests remain as quick sanity checks. The new drift test is the authoritative contract enforcement.

**Step 4: Commit**

```bash
git add tests/Api.WebApi.Tests/Contracts/SwaggerContractTests.cs
git commit -m "feat: add OpenAPI contract drift test for ApiService"
```

---

### Task 6: Write OpenAPI Contract Drift Test for RagService

**Files:**
- Create: `tests/RagService.Tests/Contracts/OpenApiContractTests.cs`
- Modify: `tests/RagService.Tests/RagService.Tests.csproj` (add WebApplicationFactory + YamlDotNet if not present)

RagService uses minimal APIs, so Swagger may not be configured yet. Check if Swashbuckle is in `RagService.Host.csproj`. If not:

**Step 1: Add Swagger to RagService**

Add to `src/RagService/RagService.Host/RagService.Host.csproj`:
```xml
<PackageReference Include="Swashbuckle.AspNetCore" Version="7.3.1" />
```

Add to `src/RagService/RagService.Host/Program.cs` (after `builder.Services` setup):
```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "RAG Service API",
        Version = "v1"
    });
});
```

And after `var app = builder.Build();`:
```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

**Step 2: Add test dependencies to RagService.Tests**

Run:
```bash
dotnet add tests/RagService.Tests package Microsoft.AspNetCore.Mvc.Testing --version 9.0.3
dotnet add tests/RagService.Tests package YamlDotNet --version 16.3.0
```

Add a project reference from RagService.Tests to RagService.Host if not present:
```bash
dotnet add tests/RagService.Tests reference src/RagService/RagService.Host/RagService.Host.csproj
```

**Step 3: Write the test**

Create `tests/RagService.Tests/Contracts/OpenApiContractTests.cs`:

```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RagService.Domain.Ports;
using YamlDotNet.Serialization;

namespace RagService.Tests.Contracts;

public sealed class OpenApiContractTests : IClassFixture<OpenApiContractTests.TestRagFactory>
{
    private readonly HttpClient _client;

    public OpenApiContractTests(TestRagFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Swagger_Matches_Contract_Spec()
    {
        var specPath = Path.Combine(
            GetSolutionRoot(), "contracts", "rag-service.openapi.yaml");
        var yamlContent = await File.ReadAllTextAsync(specPath);
        var deserializer = new Deserializer();
        var specDoc = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        var swaggerJson = await response.Content.ReadAsStringAsync();
        var swaggerDoc = JsonDocument.Parse(swaggerJson);

        var specPaths = (Dictionary<object, object>)specDoc["paths"];
        var swaggerPaths = swaggerDoc.RootElement.GetProperty("paths");

        foreach (var pathEntry in specPaths)
        {
            var path = (string)pathEntry.Key;
            Assert.True(swaggerPaths.TryGetProperty(path, out var swaggerPathItem),
                $"Contract spec defines path '{path}' but it is missing from Swagger output");

            var methods = (Dictionary<object, object>)pathEntry.Value;
            foreach (var method in methods.Keys)
            {
                var methodStr = (string)method;
                if (methodStr == "parameters") continue;
                Assert.True(swaggerPathItem.TryGetProperty(methodStr, out _),
                    $"Contract spec defines {methodStr.ToUpperInvariant()} {path} but it is missing from Swagger output");
            }
        }

        foreach (var swaggerPath in swaggerPaths.EnumerateObject())
        {
            Assert.True(specPaths.ContainsKey(swaggerPath.Name),
                $"Swagger exposes path '{swaggerPath.Name}' that is not in the contract spec");
        }
    }

    private static string GetSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "IntakeDocumentProcessor.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find solution root");
    }

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

            builder.ConfigureServices(services =>
            {
                // Remove MassTransit
                var massTransitDescriptors = services
                    .Where(d =>
                        d.ServiceType.FullName?.Contains("MassTransit") == true ||
                        d.ImplementationType?.FullName?.Contains("MassTransit") == true)
                    .ToList();
                foreach (var d in massTransitDescriptors) services.Remove(d);

                // Remove Qdrant health checks
                var healthCheckDescriptors = services
                    .Where(d => d.ServiceType.FullName?.Contains("HealthCheck") == true)
                    .ToList();
                foreach (var d in healthCheckDescriptors) services.Remove(d);
                services.AddHealthChecks();

                // Stub ports
                var embeddingDesc = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IEmbeddingPort));
                if (embeddingDesc != null) services.Remove(embeddingDesc);

                var vectorDesc = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IVectorStorePort));
                if (vectorDesc != null) services.Remove(vectorDesc);

                services.AddSingleton<IEmbeddingPort, NoOpEmbeddingPort>();
                services.AddSingleton<IVectorStorePort, NoOpVectorStorePort>();

                // Remove seeder
                var seederDescriptors = services
                    .Where(d => d.ImplementationType?.Name.Contains("Seeder") == true)
                    .ToList();
                foreach (var d in seederDescriptors) services.Remove(d);
            });
        }
    }

    private sealed class NoOpEmbeddingPort : IEmbeddingPort
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
            => Task.FromResult(new float[] { 0.1f, 0.2f });
    }

    private sealed class NoOpVectorStorePort : IVectorStorePort
    {
        public Task UpsertAsync(Guid documentId, Guid tenantId, float[] embedding,
            Dictionary<string, string> metadata, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<SearchHit>> SearchAsync(Guid tenantId, float[] queryEmbedding,
            int topK = 5, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SearchHit>>(Array.Empty<SearchHit>());

        public Task<float[]> GetEmbeddingAsync(Guid documentId, CancellationToken ct = default)
            => Task.FromResult(new float[] { 0.1f, 0.2f });
    }
}
```

Note: The `Program` class must be accessible. Check if RagService.Host has `public partial class Program` or `InternalsVisibleTo`. If not, add `public partial class Program { }` at the bottom of Program.cs or add `[assembly: InternalsVisibleTo("RagService.Tests")]` to the host project.

**Step 4: Run test**

Run: `dotnet test tests/RagService.Tests --filter "DisplayName~Swagger_Matches_Contract_Spec" -v n`
Expected: PASS

**Step 5: Commit**

```bash
git add src/RagService/RagService.Host/ tests/RagService.Tests/ contracts/rag-service.openapi.yaml
git commit -m "feat: add Swagger to RagService and OpenAPI contract drift test"
```

---

### Task 7: Write AsyncAPI Contract Drift Test for Messaging

**Files:**
- Modify: `tests/Messaging.Tests/Contracts/ContractSerializationTests.cs` (or create a new file)
- Create: `tests/Messaging.Tests/Contracts/AsyncApiContractTests.cs`

**Step 1: Write the failing test**

Create `tests/Messaging.Tests/Contracts/AsyncApiContractTests.cs`:

```csharp
using System.Reflection;
using Messaging.Contracts.Events;
using Messaging.Contracts.Models;
using YamlDotNet.Serialization;

namespace Messaging.Tests.Contracts;

public sealed class AsyncApiContractTests
{
    private static readonly Dictionary<string, object> Spec;
    private static readonly Dictionary<object, object> Schemas;

    static AsyncApiContractTests()
    {
        var specPath = Path.Combine(GetSolutionRoot(), "contracts", "messaging.asyncapi.yaml");
        var yaml = File.ReadAllText(specPath);
        var deserializer = new Deserializer();
        Spec = deserializer.Deserialize<Dictionary<string, object>>(yaml);

        var components = (Dictionary<object, object>)Spec["components"];
        var messages = (Dictionary<object, object>)components["messages"];
        Schemas = (Dictionary<object, object>)components["schemas"];
    }

    [Theory]
    [InlineData("DocumentUploadedEvent", typeof(DocumentUploadedEvent))]
    [InlineData("DocumentProcessedEvent", typeof(DocumentProcessedEvent))]
    [InlineData("EmbeddingRequestedEvent", typeof(EmbeddingRequestedEvent))]
    [InlineData("EmbeddingCompletedEvent", typeof(EmbeddingCompletedEvent))]
    [InlineData("ExtractedFieldResult", typeof(ExtractedFieldResult))]
    public void Schema_Properties_Match_CSharp_Record(string schemaName, Type contractType)
    {
        Assert.True(Schemas.ContainsKey(schemaName),
            $"AsyncAPI spec is missing schema '{schemaName}'");

        var schema = (Dictionary<object, object>)Schemas[schemaName];
        var properties = (Dictionary<object, object>)schema["properties"];

        var csharpProps = contractType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => ToCamelCase(p.Name), p => p);

        // Every property in the spec must exist on the C# type
        foreach (var prop in properties.Keys)
        {
            var propName = (string)prop;
            Assert.True(csharpProps.ContainsKey(propName),
                $"AsyncAPI schema '{schemaName}' defines property '{propName}' but it does not exist on {contractType.Name}");
        }

        // Every property on the C# type must exist in the spec
        foreach (var propName in csharpProps.Keys)
        {
            Assert.True(properties.ContainsKey(propName),
                $"{contractType.Name} has property '{propName}' that is not in the AsyncAPI schema '{schemaName}'. Add it to contracts/messaging.asyncapi.yaml");
        }
    }

    [Theory]
    [InlineData("DocumentUploadedEvent", new[] { "documentId", "tenantId", "fileName", "storageKey", "uploadedAt" })]
    [InlineData("DocumentProcessedEvent", new[] { "documentId", "tenantId", "extractedFields", "processedAt" })]
    [InlineData("EmbeddingRequestedEvent", new[] { "documentId", "tenantId", "textContent", "fieldValues", "requestedAt" })]
    [InlineData("EmbeddingCompletedEvent", new[] { "documentId", "tenantId", "completedAt" })]
    public void Schema_Required_Fields_Are_Correct(string schemaName, string[] expectedRequired)
    {
        var schema = (Dictionary<object, object>)Schemas[schemaName];
        var required = ((List<object>)schema["required"]).Cast<string>().ToHashSet();

        foreach (var field in expectedRequired)
        {
            Assert.Contains(field, required);
        }
    }

    [Fact]
    public void All_Channels_Defined()
    {
        var channels = (Dictionary<object, object>)Spec["channels"];

        Assert.True(channels.ContainsKey("ocrworker-document-uploaded"),
            "Missing channel: ocrworker-document-uploaded");
        Assert.True(channels.ContainsKey("apiservice-document-processed"),
            "Missing channel: apiservice-document-processed");
        Assert.True(channels.ContainsKey("ragservice-embedding-requested"),
            "Missing channel: ragservice-embedding-requested");
        Assert.True(channels.ContainsKey("apiservice-embedding-completed"),
            "Missing channel: apiservice-embedding-completed");
    }

    private static string ToCamelCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase)) return pascalCase;
        return char.ToLowerInvariant(pascalCase[0]) + pascalCase[1..];
    }

    private static string GetSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "IntakeDocumentProcessor.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find solution root");
    }
}
```

**Step 2: Run tests**

Run: `dotnet test tests/Messaging.Tests --filter "FullyQualifiedName~AsyncApiContractTests" -v n`
Expected: PASS

**Step 3: Commit**

```bash
git add tests/Messaging.Tests/Contracts/AsyncApiContractTests.cs
git commit -m "feat: add AsyncAPI contract drift tests for messaging"
```

---

### Task 8: Run Full Test Suite and Fix Any Drift

**Step 1: Run all tests**

Run: `dotnet test`
Expected: All tests pass (~106+ existing tests + new contract tests)

**Step 2: Fix any discrepancies**

If contract tests fail, the spec files are the source of truth. Adjust code OR adjust the spec if the spec was wrong (e.g., Swashbuckle casing doesn't match). Document any adjustments.

**Step 3: Final commit**

```bash
git add -A
git commit -m "fix: resolve contract drift between specs and runtime output"
```

(Only if changes were needed.)

---

### Task 9: Update CLAUDE.md with Contract-First Convention

**Files:**
- Modify: `CLAUDE.md`

**Step 1: Add to Key Patterns section**

Add after the existing patterns:

```markdown
**Contract-first API design.** YAML spec files in `contracts/` are the source of truth for all service boundaries. `api-service.openapi.yaml` and `rag-service.openapi.yaml` define REST endpoints (OpenAPI 3.0.3). `messaging.asyncapi.yaml` defines MassTransit events (AsyncAPI 2.6.0). Tests validate that runtime output matches the specs; any drift fails the build. When adding or changing endpoints or events, update the spec file first, then implement.
```

**Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: add contract-first convention to CLAUDE.md"
```
