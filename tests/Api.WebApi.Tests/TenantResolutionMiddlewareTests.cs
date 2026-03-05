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
    public async Task InvokeAsync_ValidHeader_SetsTenantAndCallsNext()
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
    public async Task InvokeAsync_MissingHeader_Returns400()
    {
        var context = MakeContext(null);
        var tenantCtx = new RequestTenantContext();

        await _middleware.InvokeAsync(context, tenantCtx);

        Assert.False(_nextCalled);
        Assert.Equal(400, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_MalformedHeader_Returns400()
    {
        var context = MakeContext("not-a-guid");
        var tenantCtx = new RequestTenantContext();

        await _middleware.InvokeAsync(context, tenantCtx);

        Assert.False(_nextCalled);
        Assert.Equal(400, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_EmptyGuid_Returns400()
    {
        var context = MakeContext(Guid.Empty.ToString());
        var tenantCtx = new RequestTenantContext();

        await _middleware.InvokeAsync(context, tenantCtx);

        Assert.False(_nextCalled);
        Assert.Equal(400, context.Response.StatusCode);
    }

    private static DefaultHttpContext MakeContext(string? headerValue, string path = "/api/documents")
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = new PathString(path);
        if (headerValue is not null)
            context.Request.Headers["X-Tenant-Id"] = headerValue;
        return context;
    }
}
