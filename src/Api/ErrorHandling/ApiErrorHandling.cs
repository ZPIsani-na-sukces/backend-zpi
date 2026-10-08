using Microsoft.AspNetCore.Mvc;

namespace Zpi.Api.ErrorHandling;

public static class ApiErrorHandling
{
    public static IServiceCollection AddApiErrorHandling(this IServiceCollection services)
    {
        services.AddSingleton<IProblemDetailsWriter, ContractProblemDetailsWriter>();
        services.AddProblemDetails();
        services.AddExceptionHandler<ExpectedExceptionHandler>();
        services.AddExceptionHandler<UnhandledExceptionHandler>();
        services.Configure<MvcOptions>(options =>
        {
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            options.Filters.Add<FluentValidationFilter>();
            options.Filters.Add<ProblemDetailsFilter>();
        });
        services.PostConfigure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState.Where(pair => pair.Value!.Errors.Count > 0).ToDictionary(
                pair => ValidationErrors.FieldName(pair.Key),
                pair => pair.Value!.Errors.Select(_ => "Invalid field value.").Distinct().ToArray());
            return ValidationProblem(context.HttpContext, errors);
        });
        return services;
    }

    internal static IActionResult ValidationProblem(HttpContext context, IReadOnlyDictionary<string, string[]> errors) =>
        new BadRequestObjectResult(ApiProblemResponse.Create(context, 400, "validationFailed", "Invalid request data.", errors))
        { ContentTypes = { "application/problem+json" } };
}
