using Api.Application.DTOs;
using Api.WebApi.Validation;
using FluentValidation.TestHelper;

namespace Api.WebApi.Tests.Validation;

public sealed class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Empty_email_fails()
    {
        var request = new LoginRequest(Guid.NewGuid(), "", "ValidPass1");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("Email is required.");
    }

    [Fact]
    public void Invalid_email_format_fails()
    {
        var request = new LoginRequest(Guid.NewGuid(), "not-an-email", "ValidPass1");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("A valid email address is required.");
    }

    [Fact]
    public void Empty_password_fails()
    {
        var request = new LoginRequest(Guid.NewGuid(), "user@example.com", "");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Password is required.");
    }

    [Fact]
    public void Email_exceeding_max_length_fails()
    {
        var longEmail = new string('a', 250) + "@test.com";
        var request = new LoginRequest(Guid.NewGuid(), longEmail, "ValidPass1");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Password_exceeding_max_length_fails()
    {
        var longPassword = new string('x', 129);
        var request = new LoginRequest(Guid.NewGuid(), "user@example.com", longPassword);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Valid_request_passes()
    {
        var request = new LoginRequest(Guid.NewGuid(), "user@example.com", "ValidPass1");
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
