using Api.Application.DTOs;
using Api.WebApi.Validation;
using FluentValidation.TestHelper;

namespace Api.WebApi.Tests.Validation;

public sealed class CorrectFieldRequestValidatorTests
{
    private readonly CorrectFieldRequestValidator _validator = new();

    [Fact]
    public void Empty_field_name_fails()
    {
        var request = new CorrectFieldRequest("", "some value");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.FieldName)
              .WithErrorMessage("Field name is required.");
    }

    [Fact]
    public void Field_name_exceeding_max_length_fails()
    {
        var longName = new string('x', 257);
        var request = new CorrectFieldRequest(longName, "some value");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.FieldName)
              .WithErrorMessage("Field name must not exceed 256 characters.");
    }

    [Fact]
    public void Null_new_value_fails()
    {
        var request = new CorrectFieldRequest("FieldName", null!);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.NewValue)
              .WithErrorMessage("New value is required.");
    }

    [Fact]
    public void New_value_exceeding_max_length_fails()
    {
        var longValue = new string('x', 2001);
        var request = new CorrectFieldRequest("FieldName", longValue);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.NewValue)
              .WithErrorMessage("New value must not exceed 2000 characters.");
    }

    [Fact]
    public void Valid_request_passes()
    {
        var request = new CorrectFieldRequest("FieldName", "corrected value");
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_string_new_value_passes()
    {
        var request = new CorrectFieldRequest("FieldName", "");
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.NewValue);
    }

    [Fact]
    public void Field_name_at_max_length_passes()
    {
        var name = new string('x', 256);
        var request = new CorrectFieldRequest(name, "value");
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.FieldName);
    }

    [Fact]
    public void New_value_at_max_length_passes()
    {
        var value = new string('x', 2000);
        var request = new CorrectFieldRequest("FieldName", value);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.NewValue);
    }
}
