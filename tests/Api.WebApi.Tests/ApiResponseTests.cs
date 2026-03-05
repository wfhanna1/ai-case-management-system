using Api.WebApi;

namespace Api.WebApi.Tests;

public sealed class ApiResponseTests
{
    [Fact]
    public void Fail_with_details_populates_error_details()
    {
        var details = new Dictionary<string, string[]>
        {
            ["Email"] = ["Email is required"],
            ["Password"] = ["Password is required", "Password must be at least 8 characters"]
        };

        var response = ApiResponse<object>.Fail("VALIDATION_ERROR", "One or more fields are invalid.", details);

        Assert.NotNull(response.Error);
        Assert.Equal("VALIDATION_ERROR", response.Error.Code);
        Assert.Equal("One or more fields are invalid.", response.Error.Message);
        Assert.NotNull(response.Error.Details);
        Assert.Equal(2, response.Error.Details.Count);
        Assert.Equal(["Email is required"], response.Error.Details["Email"]);
        Assert.Equal(["Password is required", "Password must be at least 8 characters"], response.Error.Details["Password"]);
    }

    [Fact]
    public void Fail_without_details_leaves_details_null()
    {
        var response = ApiResponse<object>.Fail("SOME_ERROR", "Something failed");

        Assert.NotNull(response.Error);
        Assert.Null(response.Error.Details);
    }

    [Fact]
    public void Ok_has_no_error()
    {
        var response = ApiResponse<string>.Ok("hello");

        Assert.Equal("hello", response.Data);
        Assert.Null(response.Error);
    }
}
