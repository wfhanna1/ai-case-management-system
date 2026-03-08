using FluentValidation;

namespace Api.WebApi.Validation;

public sealed record SearchDocumentsRequest(
    string? FileName,
    string? Status,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? FieldValue,
    int Page = 1,
    int PageSize = 20);

public sealed class SearchDocumentsRequestValidator : AbstractValidator<SearchDocumentsRequest>
{
    private static readonly string[] ValidStatuses =
        ["Submitted", "Processing", "Completed", "Failed", "PendingReview", "InReview", "Finalized"];

    public SearchDocumentsRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");

        When(x => !string.IsNullOrWhiteSpace(x.Status), () =>
        {
            RuleFor(x => x.Status)
                .Must(s => ValidStatuses.Contains(s, StringComparer.OrdinalIgnoreCase))
                .WithMessage("Status must be a valid document status.");
        });

        RuleFor(x => x.From)
            .Must(d => d is null || d.Value.Date <= DateTimeOffset.UtcNow.Date.AddDays(1))
            .WithMessage("'From' date cannot be in the future.");

        RuleFor(x => x.To)
            .Must(d => d is null || d.Value.Date <= DateTimeOffset.UtcNow.Date.AddDays(1))
            .WithMessage("'To' date cannot be in the future.");

        RuleFor(x => x)
            .Must(x => x.From is null || x.To is null || x.From <= x.To)
            .WithMessage("'From' date must not be after 'To' date.")
            .WithName("From");
    }
}
