using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.Mvc;
using Zpi.Api.ErrorHandling;

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
                var problemSchema = await context.GetOrCreateSchemaAsync(typeof(ApiProblemResponse), cancellationToken: cancellationToken);
                operation.Responses ??= new();
                foreach (var responseType in context.Description.SupportedResponseTypes.Where(response =>
                    response.StatusCode >= 400 && response.Type is not null && typeof(ProblemDetails).IsAssignableFrom(response.Type)))
                {
                    operation.Responses[responseType.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture)] =
                        ProblemResponse(responseType.StatusCode, problemSchema);
                }
                AddProblemResponse(operation, "500", problemSchema);
                AddProblemResponse(operation, "503", problemSchema);
                if (context.Description.ParameterDescriptions.Count > 0)
                    AddProblemResponse(operation, "400", problemSchema);
                if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
                {
                    operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [] }];
                    AddProblemResponse(operation, "401", problemSchema);
                    AddProblemResponse(operation, "403", problemSchema);
                }
                if (metadata.OfType<EnableRateLimitingAttribute>().Any())
                    AddProblemResponse(operation, "429", problemSchema);
                foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
                {
                    if (parameter.In == ParameterLocation.Header && parameter.Name == "If-Match")
                    {
                        parameter.Required = true;
                        parameter.Schema = new OpenApiSchema { Type = JsonSchemaType.String, Pattern = "^\"[0-9]+\"$" };
                    }
                }
                foreach (var responseType in context.Description.SupportedResponseTypes.Where(response =>
                    response.StatusCode is 200 or 201 && response.Type?.GetProperty("Version") is not null))
                {
                    var status = responseType.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (operation.Responses.TryGetValue(status, out var success) && success is OpenApiResponse response)
                    {
                        response.Headers ??= new Dictionary<string, IOpenApiHeader>();
                        response.Headers["ETag"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
                        if (responseType.StatusCode == 201)
                            response.Headers["Location"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
                    }
                }
                await FileUploadDocumentation.ConfigureAsync(operation, context, cancellationToken);
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
            });
        });
        return services;
    }

    private static void AddProblemResponse(OpenApiOperation operation, string status, IOpenApiSchema schema)
    {
        if (operation.Responses!.TryGetValue(status, out var existing))
        {
            if (existing.Content is not null && existing.Content.ContainsKey("application/problem+json")) return;
        }
        operation.Responses[status] = ProblemResponse(int.Parse(status), schema);
    }

    private static OpenApiResponse ProblemResponse(int status, IOpenApiSchema schema) => new()
    {
        Description = Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(status),
        Content = new Dictionary<string, OpenApiMediaType>
        {
            ["application/problem+json"] = new() { Schema = schema }
        }
    };
}
