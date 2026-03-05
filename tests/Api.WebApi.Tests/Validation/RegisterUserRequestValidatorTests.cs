using Api.Application.DTOs;
using Api.WebApi.Validation;
using FluentValidation.TestHelper;

namespace Api.WebApi.Tests.Validation;

public sealed class RegisterUserRequestValidatorTests
{
    private readonly RegisterUserRequestValidator _validator = new();

    [Fact]
    public void Empty_email_fails()
    {
        var request = new RegisterUserRequest(Guid.NewGuid(), "", "ValidPass1", ["IntakeWorker"]);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("Email is required.");
    }

    [Fact]
    public void Invalid_email_format_fails()
    {
        var request = new RegisterUserRequest(Guid.NewGuid(), "bad-email", "ValidPass1", ["IntakeWorker"]);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("A valid email address is required.");
    }

    [Fact]
    public void Empty_password_fails()
    {
        var request = new RegisterUserRequest(Guid.NewGuid(), "user@example.com", "", ["IntakeWorker"]);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Password is required.");
    }

    [Fact]
    public void Password_shorter_than_8_chars_fails()
    {
        var request = new RegisterUserRequest(Guid.NewGuid(), "user@example.com", "Ab1xxxx", ["IntakeWorker"]);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Password must be at least 8 characters.");
    }

    [Fact]
    public void Empty_tenant_id_fails()
    {
        var request = new RegisterUserRequest(Guid.Empty, "user@example.com", "ValidPass1", ["IntakeWorker"]);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.TenantId)
              .WithErrorMessage("Tenant ID is required.");
    }

    [Fact]
    public void Password_without_uppercase_fails()
    {
        var request = new RegisterUserRequest(Guid.NewGuid(), "user@example.com", "lowercase1", ["IntakeWorker"]);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Password must include an uppercase letter.");
    }

    [Fact]
    public void Password_without_lowercase_fails()
    {
        var request = new RegisterUserRequest(Guid.NewGuid(), "user@example.com", "UPPERCASE1", ["IntakeWorker"]);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Password must include a lowercase letter.");
    }

    [Fact]
    public void Password_without_digit_fails()
    {
        var request = new RegisterUserRequest(Guid.NewGuid(), "user@example.com", "NoDigitsHere", ["IntakeWorker"]);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Password must include a number.");
    }

    [Fact]
    public void Email_exceeding_max_length_fails()
    {
        var longEmail = new string('a', 250) + "@test.com";
        var request = new RegisterUserRequest(Guid.NewGuid(), longEmail, "ValidPass1", ["IntakeWorker"]);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Password_exceeding_max_length_fails()
    {
        var longPassword = "Aa1" + new string('x', 126);
        var request = new RegisterUserRequest(Guid.NewGuid(), "user@example.com", longPassword, ["IntakeWorker"]);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Valid_request_passes()
    {
        var request = new RegisterUserRequest(Guid.NewGuid(), "user@example.com", "ValidPass1", ["IntakeWorker"]);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
