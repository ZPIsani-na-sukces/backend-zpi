using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;

namespace Zpi.Api.ErrorHandling;

internal sealed class ContractProblemDetailsWriter : IProblemDetailsWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool CanWrite(ProblemDetailsContext context) => true;

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var status = problem.Status ?? context.HttpContext.Response.StatusCode;
        var response = new ApiProblemResponse(
            "about:blank",
            ReasonPhrases.GetReasonPhrase(status),
            status,
            problem.Detail,
            context.HttpContext.Request.PathBase + context.HttpContext.Request.Path,
            problem.Extensions.TryGetValue("code", out var code) ? (string)code! : ApiErrorHandling.DefaultCode(status),
            context.HttpContext.TraceIdentifier,
            problem.Extensions.TryGetValue("errors", out var errors) ? errors : null);
        context.HttpContext.Response.StatusCode = status;
        return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(response,
            options: JsonOptions,
            contentType: "application/problem+json", cancellationToken: context.HttpContext.RequestAborted));
    }
}
