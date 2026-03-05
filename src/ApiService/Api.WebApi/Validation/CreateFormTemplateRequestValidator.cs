using Api.Application.DTOs;
using Api.Domain.Aggregates;
using FluentValidation;

namespace Api.WebApi.Validation;

public sealed class CreateFormTemplateRequestValidator : AbstractValidator<CreateFormTemplateRequest>
{
    public CreateFormTemplateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Template name is required.")
            .MaximumLength(256).WithMessage("Template name must not exceed 256 characters.");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Template type is required.")
            .Must(t => Enum.TryParse<TemplateType>(t, out _))
            .WithMessage("'{PropertyValue}' is not a valid template type.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");

        RuleFor(x => x.Fields)
            .Must(f => f == null || f.Count <= 100)
            .WithMessage("A template may not have more than 100 fields.");

        RuleForEach(x => x.Fields).ChildRules(field =>
        {
            field.RuleFor(f => f.Label)
                .NotEmpty().WithMessage("Field label is required.")
                .MaximumLength(256).WithMessage("Field label must not exceed 256 characters.");

            field.RuleFor(f => f.FieldType)
                .Must(ft => Enum.TryParse<FieldType>(ft, out _))
                .WithMessage("'{PropertyValue}' is not a valid field type.");

            field.RuleFor(f => f.Options)
                .MaximumLength(4000).WithMessage("Field options must not exceed 4000 characters.");
        });
    }
}
