using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Zpi.Api.ErrorHandling;

public static class ApiErrorHandling
{
    public static IServiceCollection AddApiErrorHandling(this IServiceCollection services)
    {
        services.AddSingleton<IProblemDetailsWriter, ContractProblemDetailsWriter>();
        services.AddProblemDetails();
        services.AddExceptionHandler<UnhandledExceptionHandler>();
        services.Configure<MvcOptions>(options =>
        {
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            options.Filters.Add<FluentValidationFilter>();
        });
        services.PostConfigure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState.Where(pair => pair.Value!.Errors.Count > 0).ToDictionary(
                pair => JsonNamingPolicy.CamelCase.ConvertName(pair.Key),
                pair => pair.Value!.Errors.Select(error => error.Exception is not null || pair.Key.StartsWith('$')
                    ? "Invalid field value." : error.ErrorMessage).ToArray());
            return ValidationProblem(context.HttpContext, errors);
        });
        return services;
    }

    internal static IActionResult ValidationProblem(HttpContext context, IReadOnlyDictionary<string, string[]> errors) =>
        new BadRequestObjectResult(ApiProblemResponse.Create(context, 400, "validationFailed", "Invalid request data.", errors))
        { ContentTypes = { "application/problem+json" } };

    internal static string DefaultCode(int status) => status switch
    {
        400 => "validationFailed",
        401 => "unauthenticated",
        403 => "operationForbidden",
        404 => "resourceNotFound",
        405 => "methodNotAllowed",
        409 => "invalidState",
        410 => "invitationExpired",
        412 => "versionMismatch",
        413 => "payloadTooLarge",
        415 => "unsupportedMediaType",
        416 => "rangeNotSatisfiable",
        428 => "preconditionRequired",
        429 => "rateLimitExceeded",
        503 => "temporarilyUnavailable",
        _ => "internalServerError"
    };
}
