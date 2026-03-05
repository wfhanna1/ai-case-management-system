using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.WebApi;

/// <summary>
/// Extracts TenantId from the authenticated user's JWT "tenant_id" claim
/// and populates ITenantContext. Returns HTTP 401 for any non-exempt path
/// that lacks a valid tenant claim.
/// Exempt paths (health check, Swagger, auth endpoints) pass through without requiring a tenant.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private static readonly PathString[] ExemptPrefixes =
    [
        new("/health"),
        new("/swagger"),
        new("/api/auth"),
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
            _logger.LogWarning("Request to {Path} rejected: missing or invalid tenant_id claim",
                context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(
                ApiResponse<object>.Fail("MISSING_TENANT",
                    "A valid JWT with a tenant_id claim is required."));
            return;
        }

        tenantContext.SetTenant(tenantId);
        await _next(context);
    }

    /// <summary>
    /// Reads tenant_id from the authenticated user's JWT claims.
    /// </summary>
    private static TenantId? ResolveTenantId(HttpContext context)
    {
        var claimValue = context.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrWhiteSpace(claimValue))
            return null;

        if (!Guid.TryParse(claimValue, out var guid))
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
