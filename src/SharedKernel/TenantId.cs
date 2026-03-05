namespace SharedKernel;

/// <summary>
/// Strongly-typed identifier for a tenant. Used everywhere multi-tenancy context is required.
/// Rejects Guid.Empty to prevent accidental use of uninitialized values.
/// </summary>
public sealed class TenantId : ValueObject
{
    public Guid Value { get; }

    public TenantId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("TenantId cannot be an empty Guid.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Creates a new TenantId with a freshly generated Guid.
    /// </summary>
    public static TenantId New() => new(Guid.NewGuid());

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
