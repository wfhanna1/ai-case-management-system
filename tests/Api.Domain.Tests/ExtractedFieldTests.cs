using Api.Domain.Aggregates;

namespace Api.Domain.Tests;

public sealed class ExtractedFieldTests
{
    [Fact]
    public void Constructor_stores_all_properties()
    {
        var field = new ExtractedField("PatientName", "John Doe", 0.95);

        Assert.Equal("PatientName", field.Name);
        Assert.Equal("John Doe", field.Value);
        Assert.Equal(0.95, field.Confidence);
        Assert.Null(field.CorrectedValue);
    }

    [Fact]
    public void Constructor_with_correctedValue_stores_it()
    {
        var field = new ExtractedField("PatientName", "John Doe", 0.95, "Jane Doe");

        Assert.Equal("PatientName", field.Name);
        Assert.Equal("John Doe", field.Value);
        Assert.Equal(0.95, field.Confidence);
        Assert.Equal("Jane Doe", field.CorrectedValue);
    }

    [Fact]
    public void WithCorrection_returns_new_instance_with_CorrectedValue_set()
    {
        var original = new ExtractedField("PatientName", "John Doe", 0.95);

        var corrected = original.WithCorrection("Jane Doe");

        Assert.Equal("PatientName", corrected.Name);
        Assert.Equal("John Doe", corrected.Value);
        Assert.Equal(0.95, corrected.Confidence);
        Assert.Equal("Jane Doe", corrected.CorrectedValue);
        Assert.Null(original.CorrectedValue);
    }

    [Fact]
    public void WithCorrection_on_already_corrected_field_replaces_CorrectedValue()
    {
        var original = new ExtractedField("PatientName", "John Doe", 0.95, "Jane Doe");

        var reCorrected = original.WithCorrection("Janet Doe");

        Assert.Equal("Janet Doe", reCorrected.CorrectedValue);
        Assert.Equal("PatientName", reCorrected.Name);
        Assert.Equal("John Doe", reCorrected.Value);
        Assert.Equal(0.95, reCorrected.Confidence);
    }

    [Fact]
    public void Two_fields_with_same_components_are_equal()
    {
        var field1 = new ExtractedField("PatientName", "John Doe", 0.95, "Jane Doe");
        var field2 = new ExtractedField("PatientName", "John Doe", 0.95, "Jane Doe");

        Assert.Equal(field1, field2);
    }

    [Fact]
    public void Two_fields_with_different_components_are_not_equal()
    {
        var field1 = new ExtractedField("PatientName", "John Doe", 0.95);
        var field2 = new ExtractedField("PatientName", "John Doe", 0.80);

        Assert.NotEqual(field1, field2);
    }

    [Fact]
    public void Two_fields_with_different_correctedValue_are_not_equal()
    {
        var field1 = new ExtractedField("PatientName", "John Doe", 0.95, "Jane Doe");
        var field2 = new ExtractedField("PatientName", "John Doe", 0.95, "Janet Doe");

        Assert.NotEqual(field1, field2);
    }
}
