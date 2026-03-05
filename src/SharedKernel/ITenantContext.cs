namespace SharedKernel;

/// <summary>
/// Ambient per-request container for the current tenant.
/// Populated by middleware (HTTP) or message context (async consumers).
/// </summary>
public interface ITenantContext
{
    TenantId? TenantId { get; }
}

/// <summary>
/// Mutable implementation. Middleware sets TenantId once per request.
/// Registered as Scoped so it is shared across the entire request pipeline.
/// </summary>
public sealed class RequestTenantContext : ITenantContext
{
    public TenantId? TenantId { get; private set; }

    public void SetTenant(TenantId tenantId)
    {
        TenantId = tenantId;
    }
}
