using System.Text;
using Api.Application.Commands;
using Api.Application.Queries;
using Api.Domain.Ports;
using Api.Infrastructure.Auth;
using Api.Infrastructure.Messaging;
using Api.Infrastructure.Persistence;
using Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using FluentValidation;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SharedKernel;
using SharedKernel.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Observability -- OpenTelemetry distributed tracing + structured logging
// ---------------------------------------------------------------------------
var serviceDiagnostics = new ServiceDiagnostics("ApiService");
var appMetrics = new AppMetrics(serviceDiagnostics.ServiceName);
builder.Services.AddSingleton(serviceDiagnostics);
builder.Services.AddSingleton(appMetrics);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(serviceDiagnostics.ServiceName)
        .AddSource("MassTransit")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("MassTransit")
        .AddMeter(serviceDiagnostics.ServiceName)
        .AddPrometheusExporter());

builder.Logging.AddStructuredConsoleLogging();

// ---------------------------------------------------------------------------
// Configuration -- all from environment/config, never hardcoded (12-factor)
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Missing required configuration: ConnectionStrings__DefaultConnection");

var storagePath = builder.Configuration["Storage:BasePath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "storage");

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

// ---------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------
builder.Services.AddControllers(options =>
{
    options.Filters.Add<Api.WebApi.Validation.ValidationFilter>();
});

builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Handwritten Intake Document Processor API",
        Version = "v1",
        Description = "REST API for submitting and tracking handwritten intake documents."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});
builder.Services.AddEndpointsApiExplorer();

var rabbitHost = builder.Configuration["RabbitMQ__Host"]
    ?? builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMQ__Username"]
    ?? builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ__Password"]
    ?? builder.Configuration["RabbitMQ:Password"] ?? "guest";

builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString,
        name: "postgres",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["db", "postgres"])
    .AddRabbitMQ(async _ =>
    {
        var factory = new RabbitMQ.Client.ConnectionFactory
        {
            HostName = rabbitHost,
            UserName = rabbitUser,
            Password = rabbitPass
        };
        return await factory.CreateConnectionAsync();
    }, name: "rabbitmq", tags: ["messaging"])
    .AddUrlGroup(
        new Uri($"{builder.Configuration["OcrWorker:BaseUrl"] ?? "http://ocr-worker:8080"}/health"),
        name: "ocr-worker",
        failureStatus: HealthStatus.Degraded,
        tags: ["downstream"])
    .AddUrlGroup(
        new Uri($"{builder.Configuration["RagService:BaseUrl"] ?? "http://rag-service:8080"}/health"),
        name: "rag-service",
        failureStatus: HealthStatus.Degraded,
        tags: ["downstream"]);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ---------------------------------------------------------------------------
// Authentication -- JWT Bearer
// ---------------------------------------------------------------------------
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Missing required configuration: Jwt");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireIntakeWorker", policy =>
        policy.RequireRole("IntakeWorker", "Admin"))
    .AddPolicy("RequireReviewer", policy =>
        policy.RequireRole("Reviewer", "Admin"))
    .AddPolicy("RequireAdmin", policy =>
        policy.RequireRole("Admin"));

// ---------------------------------------------------------------------------
// Multi-tenancy -- scoped tenant context populated by middleware
// ---------------------------------------------------------------------------
builder.Services.AddScoped<RequestTenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<RequestTenantContext>());

// ---------------------------------------------------------------------------
// Persistence -- EF Core + PostgreSQL
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<IntakeDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddTransient<IDocumentRepository, EfDocumentRepository>();
builder.Services.AddTransient<ICaseRepository, EfCaseRepository>();
builder.Services.AddTransient<IFormTemplateRepository, EfFormTemplateRepository>();
builder.Services.AddTransient<IUserRepository, EfUserRepository>();
builder.Services.AddTransient<IAuditLogRepository, EfAuditLogRepository>();
builder.Services.AddTransient<IFileStoragePort>(_ =>
    new LocalFileStorageAdapter(storagePath));

// ---------------------------------------------------------------------------
// Auth services
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddTransient<ITokenService, JwtTokenService>();

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
builder.Services.AddTransient<SubmitDocumentHandler>();
builder.Services.AddTransient<GetDocumentByIdHandler>();
builder.Services.AddTransient<DownloadDocumentHandler>();
builder.Services.AddTransient<ListDocumentsByTenantHandler>();
builder.Services.AddTransient<RegisterUserHandler>();
builder.Services.AddTransient<LoginHandler>();
builder.Services.AddTransient<RefreshTokenHandler>();
builder.Services.AddTransient<CreateFormTemplateHandler>();
builder.Services.AddTransient<GetFormTemplateByIdHandler>();
builder.Services.AddTransient<ListFormTemplatesByTenantHandler>();
builder.Services.AddTransient<ListPendingReviewHandler>();
builder.Services.AddTransient<GetDocumentReviewHandler>();
builder.Services.AddTransient<StartReviewHandler>();
builder.Services.AddTransient<CorrectFieldHandler>();
builder.Services.AddTransient<FinalizeReviewHandler>();
builder.Services.AddTransient<GetAuditTrailHandler>();
builder.Services.AddTransient<SearchDocumentsHandler>();
builder.Services.AddTransient<ListCasesHandler>();
builder.Services.AddTransient<GetCaseByIdHandler>();
builder.Services.AddTransient<SearchCasesHandler>();
builder.Services.AddTransient<AssignDocumentToCaseHandler>();
builder.Services.AddTransient<CompleteDocumentProcessingHandler>();
builder.Services.AddTransient<GetSimilarCasesHandler>();
builder.Services.AddTransient<GetDashboardStatsHandler>();
builder.Services.AddTransient<GetRecentActivitiesHandler>();

// ---------------------------------------------------------------------------
// RAG Service client + Summary adapter
// ---------------------------------------------------------------------------
var ragServiceUrl = builder.Configuration["RagService:BaseUrl"] ?? "http://rag-service:8080";
builder.Services.AddHttpClient<IRagServiceClient, Api.Infrastructure.RagClient.HttpRagServiceClient>(
    client => client.BaseAddress = new Uri(ragServiceUrl));
builder.Services.AddSingleton<ISummaryPort, Api.Infrastructure.Summary.TemplateSummaryAdapter>();

// ---------------------------------------------------------------------------
// Messaging -- RabbitMQ via MassTransit (12-factor: config from env)
// ---------------------------------------------------------------------------
builder.Services.AddApiMessaging(builder.Configuration);

// ---------------------------------------------------------------------------
// Database seeding (Development only)
// ---------------------------------------------------------------------------
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<Api.WebApi.DevelopmentDbSeeder>();
}

// ---------------------------------------------------------------------------
// Application
// ---------------------------------------------------------------------------
var app = builder.Build();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseMiddleware<Api.WebApi.TenantResolutionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Intake API v1");
    });
}

app.UseAuthorization();
app.MapControllers();
app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        });
        await context.Response.WriteAsync(result);
    }
});

app.Run();

// Exposed for integration testing
public partial class Program { }
