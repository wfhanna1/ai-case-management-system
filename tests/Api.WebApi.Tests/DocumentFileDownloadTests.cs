using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SharedKernel;

namespace Api.WebApi.Tests;

/// <summary>
/// Integration tests for GET /api/documents/{id}/file.
/// Uses a real WebApplicationFactory with a SQLite file-based DB (temp file per
/// factory instance) so the schema and data persist across HTTP requests without
/// the connection-lifetime complexity of in-memory SQLite.
/// </summary>
public sealed class DocumentFileDownloadTests : IClassFixture<DocumentFileDownloadTests.TestApiFactory>
{
    private const string JwtSecret = "TestSecretKeyThatIsAtLeast32BytesLong!";
    private const string JwtIssuer = "test-issuer";
    private const string JwtAudience = "test-audience";

    private readonly TestApiFactory _factory;

    public DocumentFileDownloadTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    // ───────────────────────────────────────────────────────────────────────
    // Tests
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DownloadFile_ReturnsFile_WhenDocumentExists()
    {
        // Arrange -- upload a document first via the Submit endpoint
        var tenantId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateJwt(tenantId, "IntakeWorker"));

        var fileContent = "fake-pdf-content"u8.ToArray();
        using var form = new MultipartFormDataContent();
        var fileBytes = new ByteArrayContent(fileContent);
        fileBytes.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileBytes, "file", "test-document.pdf");

        var submitResponse = await client.PostAsync("/api/documents", form);
        var submitBody = await submitResponse.Content.ReadAsStringAsync();
        Assert.True(
            submitResponse.StatusCode == HttpStatusCode.Created,
            $"Submit failed ({submitResponse.StatusCode}): {submitBody}");

        var submitJson = System.Text.Json.JsonDocument.Parse(submitBody);
        var documentId = submitJson.RootElement
            .GetProperty("data")
            .GetProperty("id")
            .GetString();
        Assert.NotNull(documentId);

        // Act -- download the file
        var downloadResponse = await client.GetAsync($"/api/documents/{documentId}/file");

        // Assert
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal("application/pdf", downloadResponse.Content.Headers.ContentType?.MediaType);

        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(fileContent.Length, downloadedBytes.Length);
    }

    [Fact]
    public async Task DownloadFile_Returns404_WhenDocumentNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateJwt(tenantId, "IntakeWorker"));

        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/api/documents/{nonExistentId}/file");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DownloadFile_Returns401_WhenNotAuthenticated()
    {
        // Arrange -- no Authorization header
        var client = _factory.CreateClient();
        var anyId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/api/documents/{anyId}/file");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ───────────────────────────────────────────────────────────────────────
    // JWT helper
    // ───────────────────────────────────────────────────────────────────────

    private static string CreateJwt(Guid tenantId, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, "worker@test.com"),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role),
        };

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Test factory
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Boots the API with:
    /// - SQLite file-based DB in a temp directory (avoids in-memory connection
    ///   lifetime issues and the Npgsql/SQLite dual-provider conflict).
    /// - No-op MassTransit / message bus.
    /// - Temp directory for file storage so uploads and downloads work end-to-end.
    /// </summary>
    public sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _tempDir;
        private readonly string _dbPath;
        private readonly string _storagePath;
        private readonly string _sqliteConnectionString;

        public TestApiFactory()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"ai-cms-tests-{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);

            _dbPath = Path.Combine(_tempDir, "test.db");
            _sqliteConnectionString = $"DataSource={_dbPath}";

            _storagePath = Path.Combine(_tempDir, "storage");
            Directory.CreateDirectory(_storagePath);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=test");
            builder.UseSetting("RabbitMQ:Host", "localhost");
            builder.UseSetting("RabbitMQ:Username", "guest");
            builder.UseSetting("RabbitMQ:Password", "guest");
            builder.UseSetting("Jwt:Secret", JwtSecret);
            builder.UseSetting("Jwt:Issuer", JwtIssuer);
            builder.UseSetting("Jwt:Audience", JwtAudience);
            builder.UseSetting("Jwt:AccessTokenExpirationMinutes", "15");
            builder.UseSetting("Jwt:RefreshTokenExpirationDays", "7");
            builder.UseSetting("Storage:BasePath", _storagePath);

            builder.ConfigureServices(services =>
            {
                // Replace PostgreSQL DbContext with a file-based SQLite DB.
                //
                // EF Core registers three layers of descriptors when AddDbContext is called:
                //   1. DbContextOptions<TContext>          -- the typed options object
                //   2. DbContextOptions (non-generic base) -- resolved by some internal code paths
                //   3. IDbContextOptionsConfiguration<TContext> -- the configuration delegate
                //      that applies the provider (UseNpgsql). This is an EF-internal type, so we
                //      match it by full name.
                //
                // If any of these survive, EF detects both Npgsql and SQLite providers and throws
                // "two providers registered". We also strip anything from the Npgsql assembly.
                var npgsqlAssemblyName = "Npgsql.EntityFrameworkCore.PostgreSQL";
                var toRemove = services
                    .Where(d =>
                        d.ServiceType == typeof(DbContextOptions<IntakeDbContext>) ||
                        d.ServiceType == typeof(DbContextOptions) ||
                        d.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration") == true ||
                        d.ServiceType.Assembly.GetName().Name == npgsqlAssemblyName ||
                        (d.ImplementationType?.Assembly.GetName().Name == npgsqlAssemblyName))
                    .ToList();
                foreach (var d in toRemove) services.Remove(d);

                var connectionString = _sqliteConnectionString;
                services.AddDbContext<IntakeDbContext>(options =>
                    options.UseSqlite(connectionString));

                // Remove all MassTransit registrations.
                var massTransitDescriptors = services
                    .Where(d =>
                        d.ServiceType.FullName?.Contains("MassTransit") == true ||
                        d.ImplementationType?.FullName?.Contains("MassTransit") == true)
                    .ToList();
                foreach (var d in massTransitDescriptors) services.Remove(d);

                // Replace IMessageBusPort with no-op.
                var busDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IMessageBusPort));
                if (busDescriptor != null) services.Remove(busDescriptor);

                services.AddSingleton<IMessageBusPort, NoOpMessageBus>();

                // Remove health checks that require live infrastructure.
                var healthCheckDescriptors = services
                    .Where(d => d.ServiceType.FullName?.Contains("HealthCheck") == true)
                    .ToList();
                foreach (var d in healthCheckDescriptors) services.Remove(d);
                services.AddHealthChecks();

                // Remove the DevelopmentDbSeeder to avoid migration side effects.
                var seederDescriptor = services.SingleOrDefault(
                    d => d.ImplementationType == typeof(DevelopmentDbSeeder));
                if (seederDescriptor != null) services.Remove(seederDescriptor);
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);

            // Apply the SQLite schema via EnsureCreated(). EF migrations target
            // PostgreSQL, so we derive the schema directly from the model.
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IntakeDbContext>();
            db.Database.EnsureCreated();

            return host;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }

            base.Dispose(disposing);
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
