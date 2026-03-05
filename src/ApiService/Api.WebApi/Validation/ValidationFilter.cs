using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Api.WebApi.Validation;

/// <summary>
/// Runs FluentValidation validators for action arguments and returns 422
/// with field-level errors in <see cref="ApiResponse{T}"/> format.
/// Replaces both the deprecated FluentValidation.AspNetCore auto-validation
/// and the built-in [ApiController] model state filter (which is suppressed).
/// Manual guards in controllers (e.g. null file checks) return 400 directly.
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var argumentType = argument.GetType();
            var validatorType = typeof(IValidator<>).MakeGenericType(argumentType);
            var validator = context.HttpContext.RequestServices.GetService(validatorType) as IValidator;

            if (validator is null)
                continue;

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext);

            if (!result.IsValid)
            {
                foreach (var error in result.Errors)
                {
                    context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                }
            }
        }

        if (!context.ModelState.IsValid)
        {
            var details = context.ModelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

            var response = ApiResponse<object>.Fail(
                "VALIDATION_ERROR",
                "One or more fields are invalid.",
                details);

            context.Result = new ObjectResult(response) { StatusCode = StatusCodes.Status422UnprocessableEntity };
            return;
        }

        await next();
    }
}
