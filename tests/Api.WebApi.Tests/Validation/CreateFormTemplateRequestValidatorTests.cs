using Api.Application.DTOs;
using Api.WebApi.Validation;
using FluentValidation.TestHelper;

namespace Api.WebApi.Tests.Validation;

public sealed class CreateFormTemplateRequestValidatorTests
{
    private readonly CreateFormTemplateRequestValidator _validator = new();

    [Fact]
    public void Empty_name_fails()
    {
        var request = new CreateFormTemplateRequest("", "Desc", "ChildWelfare", []);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name)
              .WithErrorMessage("Template name is required.");
    }

    [Fact]
    public void Name_exceeding_max_length_fails()
    {
        var longName = new string('x', 257);
        var request = new CreateFormTemplateRequest(longName, "Desc", "ChildWelfare", []);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Empty_type_fails()
    {
        var request = new CreateFormTemplateRequest("Name", "Desc", "", []);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Type)
              .WithErrorMessage("Template type is required.");
    }

    [Fact]
    public void Description_exceeding_max_length_fails()
    {
        var longDesc = new string('x', 2001);
        var request = new CreateFormTemplateRequest("Name", longDesc, "ChildWelfare", []);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Field_with_empty_label_fails()
    {
        var request = new CreateFormTemplateRequest(
            "Name", "Desc", "ChildWelfare",
            [new TemplateFieldDto("", "Text", true, null)]);
        var result = _validator.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void Field_with_invalid_field_type_fails()
    {
        var request = new CreateFormTemplateRequest(
            "Name", "Desc", "ChildWelfare",
            [new TemplateFieldDto("Label", "InvalidType", true, null)]);
        var result = _validator.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void Field_label_exceeding_max_length_fails()
    {
        var request = new CreateFormTemplateRequest(
            "Name", "Desc", "ChildWelfare",
            [new TemplateFieldDto(new string('x', 257), "Text", true, null)]);
        var result = _validator.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void Field_options_exceeding_max_length_fails()
    {
        var request = new CreateFormTemplateRequest(
            "Name", "Desc", "ChildWelfare",
            [new TemplateFieldDto("Label", "Select", true, new string('x', 4001))]);
        var result = _validator.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void Too_many_fields_fails()
    {
        var fields = Enumerable.Range(0, 101)
            .Select(i => new TemplateFieldDto($"Field{i}", "Text", false, null))
            .ToList();
        var request = new CreateFormTemplateRequest("Name", "Desc", "ChildWelfare", fields);
        var result = _validator.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void Valid_request_passes()
    {
        var request = new CreateFormTemplateRequest(
            "Child Welfare Intake", "Description", "ChildWelfare",
            [new TemplateFieldDto("Name", "Text", true, null)]);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
