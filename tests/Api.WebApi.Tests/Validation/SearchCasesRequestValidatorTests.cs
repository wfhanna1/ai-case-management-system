using Api.WebApi.Validation;
using FluentValidation.TestHelper;

namespace Api.WebApi.Tests.Validation;

public sealed class SearchCasesRequestValidatorTests
{
    private readonly SearchCasesRequestValidator _validator = new();

    [Fact]
    public void Page_less_than_one_fails()
    {
        var request = new SearchCasesRequest(null, null, null, null, Page: 0);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Page)
              .WithErrorMessage("Page must be at least 1.");
    }

    [Fact]
    public void Negative_page_fails()
    {
        var request = new SearchCasesRequest(null, null, null, null, Page: -1);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Page)
              .WithErrorMessage("Page must be at least 1.");
    }

    [Fact]
    public void PageSize_less_than_one_fails()
    {
        var request = new SearchCasesRequest(null, null, null, null, PageSize: 0);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PageSize)
              .WithErrorMessage("PageSize must be between 1 and 100.");
    }

    [Fact]
    public void PageSize_greater_than_100_fails()
    {
        var request = new SearchCasesRequest(null, null, null, null, PageSize: 101);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PageSize)
              .WithErrorMessage("PageSize must be between 1 and 100.");
    }

    [Fact]
    public void Invalid_status_fails()
    {
        var request = new SearchCasesRequest(null, "InvalidStatus", null, null);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Status)
              .WithErrorMessage("Status must be a valid document status.");
    }

    [Fact]
    public void Valid_status_passes()
    {
        var request = new SearchCasesRequest(null, "Submitted", null, null);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void Valid_status_case_insensitive_passes()
    {
        var request = new SearchCasesRequest(null, "submitted", null, null);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void Null_status_passes()
    {
        var request = new SearchCasesRequest(null, null, null, null);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void From_after_To_fails()
    {
        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(-1);
        var request = new SearchCasesRequest(null, null, from, to);
        var result = _validator.TestValidate(request);
        result.ShouldHaveAnyValidationError()
              .WithErrorMessage("'From' date must not be after 'To' date.");
    }

    [Fact]
    public void From_before_To_passes()
    {
        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(1);
        var request = new SearchCasesRequest(null, null, from, to);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void From_equals_To_passes()
    {
        var date = DateTimeOffset.UtcNow;
        var request = new SearchCasesRequest(null, null, date, date);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void From_without_To_passes()
    {
        var request = new SearchCasesRequest(null, null, DateTimeOffset.UtcNow, null);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void To_without_From_passes()
    {
        var request = new SearchCasesRequest(null, null, null, DateTimeOffset.UtcNow);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("Submitted")]
    [InlineData("Processing")]
    [InlineData("Completed")]
    [InlineData("Failed")]
    [InlineData("PendingReview")]
    [InlineData("InReview")]
    [InlineData("Finalized")]
    public void All_valid_statuses_pass(string status)
    {
        var request = new SearchCasesRequest(null, status, null, null);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void Valid_request_with_defaults_passes()
    {
        var request = new SearchCasesRequest(null, null, null, null);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Valid_request_with_all_fields_passes()
    {
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to = DateTimeOffset.UtcNow;
        var request = new SearchCasesRequest("search term", "Completed", from, to, 2, 50);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
