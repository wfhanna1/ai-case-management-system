using System.Security.Claims;
using Api.WebApi;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.WebApi.Tests;

public sealed class TenantResolutionMiddlewareLogScopeTests
{
    [Fact]
    public async Task InvokeAsync_ValidTenant_CreatesLogScopeWithTenantIdAndTraceId()
    {
        var tenantGuid = Guid.NewGuid();
        var logger = new ScopeCapturingLogger();
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new TenantResolutionMiddleware(next, logger);
        var context = MakeContext(tenantGuid.ToString());
        var tenantCtx = new RequestTenantContext();

        await middleware.InvokeAsync(context, tenantCtx);

        Assert.Single(logger.Scopes);
        var scope = logger.Scopes[0] as IReadOnlyDictionary<string, object?>;
        Assert.NotNull(scope);
        Assert.Equal(tenantGuid, scope["TenantId"]);
        Assert.True(scope.ContainsKey("TraceId"));
    }

    [Fact]
    public async Task InvokeAsync_ExemptPath_DoesNotCreateLogScope()
    {
        var logger = new ScopeCapturingLogger();
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new TenantResolutionMiddleware(next, logger);
        var context = MakeContext(null, path: "/health");
        var tenantCtx = new RequestTenantContext();

        await middleware.InvokeAsync(context, tenantCtx);

        Assert.Empty(logger.Scopes);
    }

    private static DefaultHttpContext MakeContext(string? tenantClaimValue, string path = "/api/documents")
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = new PathString(path);

        if (tenantClaimValue is not null)
        {
            var claims = new[] { new Claim("tenant_id", tenantClaimValue) };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }

        return context;
    }

    private sealed class ScopeCapturingLogger : ILogger<TenantResolutionMiddleware>
    {
        public List<object?> Scopes { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            Scopes.Add(state);
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) { }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
