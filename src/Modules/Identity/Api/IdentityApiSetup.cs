using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Identity.Api.Errors;

namespace Zpi.Modules.Identity.Api;

public static class IdentityApiSetup
{
    public static IServiceCollection AddIdentityApi(this IServiceCollection services)
    {
        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
            .AddApplicationPart(typeof(AuthController).Assembly)
            .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            options.JsonSerializerOptions.RespectRequiredConstructorParameters = true;
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        });
        services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState.Where(pair => pair.Value!.Errors.Count > 0).ToDictionary(
                pair => JsonNamingPolicy.CamelCase.ConvertName(pair.Key),
                pair => pair.Value!.Errors.Select(error => error.Exception is null ? error.ErrorMessage : "Invalid field value.").ToArray());
            return new BadRequestObjectResult(AuthenticationProblem.Create(context.HttpContext, 400, "validationFailed", "Invalid request data.", errors))
            { ContentTypes = { "application/problem+json" } };
        });
        return services;
    }
}
