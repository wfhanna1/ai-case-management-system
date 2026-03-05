using SharedKernel;

namespace Api.Domain.Aggregates;

public sealed class TemplateField : ValueObject
{
    public string Label { get; private set; }
    public FieldType FieldType { get; private set; }
    public bool IsRequired { get; private set; }
    public string? Options { get; private set; }

    // Required by EF Core for materialization
    private TemplateField() { Label = string.Empty; }

    public TemplateField(string label, FieldType fieldType, bool isRequired, string? options)
    {
        Label = label;
        FieldType = fieldType;
        IsRequired = isRequired;
        Options = options;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Label;
        yield return FieldType;
        yield return IsRequired;
        yield return Options ?? string.Empty;
    }
}
