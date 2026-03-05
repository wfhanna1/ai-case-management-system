using Api.Application.DTOs;
using FluentValidation;

namespace Api.WebApi.Validation;

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.")
            .MaximumLength(256).WithMessage("Invalid refresh token.");

        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty).WithMessage("User ID is required.");

        RuleFor(x => x.TenantId)
            .NotEqual(Guid.Empty).WithMessage("Tenant ID is required.");
    }
}
