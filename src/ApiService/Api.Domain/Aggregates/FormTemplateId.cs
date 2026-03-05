using SharedKernel;

namespace Api.Domain.Aggregates;

public sealed class FormTemplateId : ValueObject
{
    public Guid Value { get; }

    public FormTemplateId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("FormTemplateId cannot be an empty Guid.", nameof(value));
        Value = value;
    }

    public static FormTemplateId New() => new(Guid.NewGuid());

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
