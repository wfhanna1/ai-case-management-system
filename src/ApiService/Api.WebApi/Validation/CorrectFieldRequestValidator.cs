using Api.Application.DTOs;
using FluentValidation;

namespace Api.WebApi.Validation;

public sealed class CorrectFieldRequestValidator : AbstractValidator<CorrectFieldRequest>
{
    public CorrectFieldRequestValidator()
    {
        RuleFor(x => x.FieldName)
            .NotEmpty().WithMessage("Field name is required.")
            .MaximumLength(256).WithMessage("Field name must not exceed 256 characters.");

        RuleFor(x => x.NewValue)
            .NotNull().WithMessage("New value is required.")
            .MaximumLength(2000).WithMessage("New value must not exceed 2000 characters.");
    }
}
