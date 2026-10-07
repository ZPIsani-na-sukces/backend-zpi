using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Zpi.Modules.Identity.Api.Validation;

public sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.ActionArguments.Values.OfType<TRequest>().FirstOrDefault();
        if (request is not null)
        {
            var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
            if (!result.IsValid) throw new ValidationException(result.Errors);
        }
        await next();
    }
}
