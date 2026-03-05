using Api.Application.DTOs;
using Api.WebApi.Validation;
using FluentValidation.TestHelper;

namespace Api.WebApi.Tests.Validation;

public sealed class RefreshTokenRequestValidatorTests
{
    private readonly RefreshTokenRequestValidator _validator = new();

    [Fact]
    public void Empty_refresh_token_fails()
    {
        var request = new RefreshTokenRequest(Guid.NewGuid(), Guid.NewGuid(), "");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.RefreshToken)
              .WithErrorMessage("Refresh token is required.");
    }

    [Fact]
    public void Empty_user_id_fails()
    {
        var request = new RefreshTokenRequest(Guid.Empty, Guid.NewGuid(), "some-token");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.UserId)
              .WithErrorMessage("User ID is required.");
    }

    [Fact]
    public void Empty_tenant_id_fails()
    {
        var request = new RefreshTokenRequest(Guid.NewGuid(), Guid.Empty, "some-token");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.TenantId)
              .WithErrorMessage("Tenant ID is required.");
    }

    [Fact]
    public void Refresh_token_exceeding_max_length_fails()
    {
        var longToken = new string('x', 257);
        var request = new RefreshTokenRequest(Guid.NewGuid(), Guid.NewGuid(), longToken);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }

    [Fact]
    public void Valid_request_passes()
    {
        var request = new RefreshTokenRequest(Guid.NewGuid(), Guid.NewGuid(), "some-token");
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
