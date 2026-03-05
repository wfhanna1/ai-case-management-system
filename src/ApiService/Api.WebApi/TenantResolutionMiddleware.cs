using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.WebApi;

/// <summary>
/// Extracts TenantId from X-Tenant-Id request header and populates ITenantContext.
/// Returns HTTP 400 for any non-exempt path that lacks a valid tenant header.
/// Exempt paths (health check, Swagger) pass through without requiring a tenant.
///
/// Upgrade path (Issue #8): replace ResolveTenantId() to read from JWT claims.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private static readonly PathString[] ExemptPrefixes =
    [
        new("/health"),
        new("/swagger"),
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestTenantContext tenantContext)
    {
        if (IsExempt(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var tenantId = ResolveTenantId(context);
        if (tenantId is null)
        {
            _logger.LogWarning("Request to {Path} rejected: missing or invalid X-Tenant-Id header",
                context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                ApiResponse<object>.Fail("MISSING_TENANT",
                    "X-Tenant-Id header is required and must be a valid non-empty GUID."));
            return;
        }

        tenantContext.SetTenant(tenantId);
        await _next(context);
    }

    /// <summary>
    /// Issue #8: replace this method body to read from JWT claims.
    /// </summary>
    private static TenantId? ResolveTenantId(HttpContext context)
    {
        var headerValue = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(headerValue))
            return null;

        if (!Guid.TryParse(headerValue, out var guid))
            return null;

        try
        {
            return new TenantId(guid);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool IsExempt(PathString path)
    {
        foreach (var prefix in ExemptPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return path.Equals(new PathString("/health"), StringComparison.OrdinalIgnoreCase);
    }
}
