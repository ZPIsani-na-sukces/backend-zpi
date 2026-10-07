using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Zpi.Modules.Identity.Api;
using Zpi.Modules.Identity.Api.Errors;

namespace Zpi.Api;

public static class ApiDocumentation
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.RespectRequiredConstructorParameters = true;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase, false));
        });
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info = new()
                {
                    Title = "ZPI API",
                    Version = "v1"
                };
                document.Components ??= new();
                document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                {
                    ["Bearer"] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT"
                    }
                };
                return Task.CompletedTask;
            });
            options.AddOperationTransformer(async (operation, context, cancellationToken) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
                    operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [] }];
                if (context.Description.RelativePath is "api/v1/auth/login" or "api/v1/auth/refresh")
                {
                    operation.Parameters ??= [];
                    operation.Parameters.Add(new OpenApiParameter
                    {
                        Name = "Origin",
                        In = ParameterLocation.Header,
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                    });
                }
                if (context.Description.RelativePath == "api/v1/auth/login" &&
                    operation.Responses?["403"].Content?["application/problem+json"] is { } forbidden)
                {
                    var problem = await context.GetOrCreateSchemaAsync(typeof(AuthenticationProblem), cancellationToken: cancellationToken);
                    forbidden.Schema = new OpenApiSchema { AnyOf = [forbidden.Schema!, problem] };
                }
            });
        });
        return services;
    }
}
