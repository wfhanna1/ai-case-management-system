using SharedKernel;

namespace Api.Domain.Aggregates;

public sealed class CaseId : ValueObject
{
    public Guid Value { get; }

    public CaseId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("CaseId cannot be an empty Guid.", nameof(value));
        Value = value;
    }

    public static CaseId New() => new(Guid.NewGuid());

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
