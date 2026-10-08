using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Zpi.Api.ErrorHandling;

public sealed class FluentValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var failures = new List<ValidationFailure>();
        foreach (var parameter in context.ActionDescriptor.Parameters)
        {
            if (!context.ActionArguments.TryGetValue(parameter.Name!, out var argument) || argument is null) continue;
            var type = parameter.ParameterType;
            var validatorType = typeof(IValidator<>).MakeGenericType(type);
            foreach (var validator in context.HttpContext.RequestServices.GetServices(validatorType).Cast<IValidator>())
            {
                var validationContext = new ValidationContext<object>(argument);
                var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
                failures.AddRange(result.Errors);
            }
        }
        if (failures.Count > 0)
        {
            var errors = ValidationErrors.FromFailures(failures);
            context.Result = ApiErrorHandling.ValidationProblem(context.HttpContext, errors);
            return;
        }
        await next();
    }
}
