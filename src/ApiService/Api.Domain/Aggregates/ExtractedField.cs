using SharedKernel;

namespace Api.Domain.Aggregates;

/// <summary>
/// Represents a single field extracted by OCR from a document.
/// CorrectedValue is populated when a reviewer corrects the original extraction.
/// </summary>
public sealed class ExtractedField : ValueObject
{
    public string Name { get; private set; }
    public string Value { get; private set; }
    public double Confidence { get; private set; }
    public string? CorrectedValue { get; private set; }

    // Required by EF Core for materialization.
    private ExtractedField()
    {
        Name = string.Empty;
        Value = string.Empty;
    }

    public ExtractedField(string name, string value, double confidence, string? correctedValue = null)
    {
        Name = name;
        Value = value;
        Confidence = confidence;
        CorrectedValue = correctedValue;
    }

    public ExtractedField WithCorrection(string newValue)
        => new(Name, Value, Confidence, newValue);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Name;
        yield return Value;
        yield return Confidence;
        yield return CorrectedValue ?? string.Empty;
    }
}
