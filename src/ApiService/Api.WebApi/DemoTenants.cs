namespace Api.WebApi;

/// <summary>
/// Well-known tenant IDs and credentials for local development and demo environments.
/// </summary>
public static class DemoTenants
{
    public static readonly Guid AlphaTenantId = new("a1b2c3d4-0000-0000-0000-000000000001");
    public static readonly Guid BetaTenantId  = new("b2c3d4e5-0000-0000-0000-000000000002");

    public const string DemoPassword = "Demo123!";
}
