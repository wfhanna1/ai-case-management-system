using Api.WebApi.Validation;
using FluentValidation.TestHelper;

namespace Api.WebApi.Tests.Validation;

public sealed class SearchDocumentsRequestValidatorTests
{
    private readonly SearchDocumentsRequestValidator _validator = new();

    [Fact]
    public void Future_From_date_fails()
    {
        var futureDate = DateTimeOffset.UtcNow.AddDays(1);
        var request = new SearchDocumentsRequest(null, null, futureDate, null, null);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.From)
              .WithErrorMessage("'From' date cannot be in the future.");
    }

    [Fact]
    public void Future_To_date_fails()
    {
        var futureDate = DateTimeOffset.UtcNow.AddDays(1);
        var request = new SearchDocumentsRequest(null, null, null, futureDate, null);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.To)
              .WithErrorMessage("'To' date cannot be in the future.");
    }

    [Fact]
    public void Past_dates_pass()
    {
        var past = DateTimeOffset.UtcNow.AddDays(-7);
        var now = DateTimeOffset.UtcNow;
        var request = new SearchDocumentsRequest(null, null, past, now, null);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
