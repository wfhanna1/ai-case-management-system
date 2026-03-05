using Api.WebApi;
using Api.WebApi.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace Api.WebApi.Tests.Validation;

public sealed class ValidationFilterTests
{
    [Fact]
    public async Task Invalid_model_state_returns_422_with_validation_error()
    {
        var filter = new ValidationFilter();
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Email", "Email is required.");
        var context = CreateContext(modelState);
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(CreateExecutedContext(context));
        });

        Assert.False(nextCalled);
        var objectResult = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(422, objectResult.StatusCode);
        var response = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        Assert.NotNull(response.Error);
        Assert.Equal("VALIDATION_ERROR", response.Error.Code);
    }

    [Fact]
    public async Task Invalid_model_state_includes_field_details()
    {
        var filter = new ValidationFilter();
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Email", "Email is required.");
        modelState.AddModelError("Password", "Password is required.");
        var context = CreateContext(modelState);

        await filter.OnActionExecutionAsync(context, () =>
            Task.FromResult(CreateExecutedContext(context)));

        var objectResult = Assert.IsType<ObjectResult>(context.Result);
        var response = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        Assert.NotNull(response.Error!.Details);
        Assert.Contains("Email", response.Error.Details.Keys);
        Assert.Contains("Password", response.Error.Details.Keys);
        Assert.Equal(["Email is required."], response.Error.Details["Email"]);
    }

    [Fact]
    public async Task Valid_model_state_calls_next()
    {
        var filter = new ValidationFilter();
        var context = CreateContext(new ModelStateDictionary());
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(CreateExecutedContext(context));
        });

        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }

    private static ActionExecutingContext CreateContext(ModelStateDictionary modelState)
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), modelState);
        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: null!);
    }

    private static ActionExecutedContext CreateExecutedContext(ActionExecutingContext context)
    {
        return new ActionExecutedContext(
            context,
            new List<IFilterMetadata>(),
            controller: null!);
    }
}
