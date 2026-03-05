using SharedKernel;

namespace Api.Domain.Aggregates;

/// <summary>
/// Strongly-typed identifier for an IntakeDocument aggregate.
/// </summary>
public sealed class DocumentId : ValueObject
{
    public Guid Value { get; }

    public DocumentId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("DocumentId cannot be an empty Guid.", nameof(value));
        Value = value;
    }

    public static DocumentId New() => new(Guid.NewGuid());

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
