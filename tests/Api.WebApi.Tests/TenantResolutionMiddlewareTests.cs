using System.Security.Claims;
using Api.WebApi;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Api.WebApi.Tests;

public sealed class TenantResolutionMiddlewareTests
{
    private readonly TenantResolutionMiddleware _middleware;
    private bool _nextCalled;

    public TenantResolutionMiddlewareTests()
    {
        RequestDelegate next = _ => { _nextCalled = true; return Task.CompletedTask; };
        _middleware = new TenantResolutionMiddleware(next,
            NullLogger<TenantResolutionMiddleware>.Instance);
    }

    [Fact]
    public async Task InvokeAsync_ValidJwtClaim_SetsTenantAndCallsNext()
    {
        var tenantGuid = Guid.NewGuid();
        var context = MakeContext(tenantGuid.ToString());
        var tenantCtx = new RequestTenantContext();

        await _middleware.InvokeAsync(context, tenantCtx);

        Assert.True(_nextCalled);
        Assert.NotNull(tenantCtx.TenantId);
        Assert.Equal(tenantGuid, tenantCtx.TenantId!.Value);
    }

    [Fact]
    public async Task InvokeAsync_HealthPath_SkipsExtractionAndCallsNext()
    {
        var context = MakeContext(null, path: "/health");
        var tenantCtx = new RequestTenantContext();

        await _middleware.InvokeAsync(context, tenantCtx);

        Assert.True(_nextCalled);
        Assert.Null(tenantCtx.TenantId);
    }

    [Fact]
    public async Task InvokeAsync_SwaggerPath_SkipsExtractionAndCallsNext()
    {
        var context = MakeContext(null, path: "/swagger/index.html");
        var tenantCtx = new RequestTenantContext();

        await _middleware.InvokeAsync(context, tenantCtx);

        Assert.True(_nextCalled);
        Assert.Null(tenantCtx.TenantId);
    }

    [Fact]
    public async Task InvokeAsync_AuthPath_SkipsExtractionAndCallsNext()
    {
        var context = MakeContext(null, path: "/api/auth/login");
        var tenantCtx = new RequestTenantContext();

        await _middleware.InvokeAsync(context, tenantCtx);

        Assert.True(_nextCalled);
        Assert.Null(tenantCtx.TenantId);
    }

    [Fact]
    public async Task InvokeAsync_MetricsPath_SkipsExtractionAndCallsNext()
    {
        var context = MakeContext(null, path: "/metrics");
        var tenantCtx = new RequestTenantContext();

        await _middleware.InvokeAsync(context, tenantCtx);

        Assert.True(_nextCalled);
        Assert.Null(tenantCtx.TenantId);
    }

    [Fact]
    public async Task InvokeAsync_MissingClaim_Returns401()
    {
        var context = MakeContext(null);
        var tenantCtx = new RequestTenantContext();

        await _middleware.InvokeAsync(context, tenantCtx);

        Assert.False(_nextCalled);
        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_MalformedClaim_Returns401()
    {
        var context = MakeContext("not-a-guid");
        var tenantCtx = new RequestTenantContext();

        await _middleware.InvokeAsync(context, tenantCtx);

        Assert.False(_nextCalled);
        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_EmptyGuidClaim_Returns401()
    {
        var context = MakeContext(Guid.Empty.ToString());
        var tenantCtx = new RequestTenantContext();

        await _middleware.InvokeAsync(context, tenantCtx);

        Assert.False(_nextCalled);
        Assert.Equal(401, context.Response.StatusCode);
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
}
