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
using SharedKernel;

var builder = WebApplication.CreateBuilder(args);

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
});
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString,
        name: "postgres",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["db", "postgres"]);

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
