using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Zpi.Modules.MedicalRecords.Api.Forms;
using Zpi.Modules.MedicalRecords.Application.Files;
using Zpi.Modules.MedicalRecords.Application.Records;

namespace Zpi.Api;

internal static class FileUploadDocumentation
{
    public static async Task ConfigureAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var parameters = context.Description.ActionDescriptor.Parameters;
        var create = parameters.Any(parameter => parameter.ParameterType == typeof(CreateMedicalRecordForm));
        var add = parameters.Any(parameter => parameter.ParameterType == typeof(AddMedicalFileForm));
        if (create || add)
        {
            var binary = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" };
            var properties = new Dictionary<string, IOpenApiSchema>();
            var encoding = new Dictionary<string, OpenApiEncoding>();
            if (create)
            {
                properties["metadata"] = await context.GetOrCreateSchemaAsync(typeof(SaveMedicalRecordMetadataRequest), cancellationToken: cancellationToken);
                properties["files"] = new OpenApiSchema { Type = JsonSchemaType.Array, Items = binary };
                encoding["metadata"] = new OpenApiEncoding { ContentType = "application/json" };
            }
            else properties["file"] = binary;
            encoding[create ? "files" : "file"] = new OpenApiEncoding
            { ContentType = string.Join(", ", MedicalMimeTypes.Pdf, MedicalMimeTypes.Jpeg, MedicalMimeTypes.Png) };
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema { Type = JsonSchemaType.Object, Properties = properties, Required = new HashSet<string> { create ? "metadata" : "file" } },
                        Encoding = encoding
                    }
                }
            };
        }
        foreach (var responseType in context.Description.SupportedResponseTypes.Where(response => response.Type is not null && typeof(FileResult).IsAssignableFrom(response.Type)))
        {
            if (!operation.Responses!.TryGetValue(responseType.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture), out var response)) continue;
            foreach (var media in response.Content?.Values ?? []) media.Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" };
        }
        if (add || context.Description.HttpMethod == "DELETE" && context.Description.RelativePath?.EndsWith("/files/{fileId}", StringComparison.Ordinal) == true)
        {
            foreach (var response in operation.Responses!.Where(pair => pair.Key is "201" or "204").Select(pair => pair.Value).OfType<OpenApiResponse>())
            {
                response.Headers ??= new Dictionary<string, IOpenApiHeader>();
                if (add) response.Headers["Location"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
                response.Headers["ETag"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            }
        }
    }
}
