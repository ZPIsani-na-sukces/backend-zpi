using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;

namespace Zpi.Api.ErrorHandling;

internal sealed class ContractProblemDetailsWriter : IProblemDetailsWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool CanWrite(ProblemDetailsContext context) => true;

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var status = problem.Status ?? context.HttpContext.Response.StatusCode;
        var response = ApiProblemResponse.FromProblemDetails(context.HttpContext, problem);
        context.HttpContext.Response.StatusCode = status;
        return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(response,
            options: JsonOptions,
            contentType: "application/problem+json", cancellationToken: context.HttpContext.RequestAborted));
    }
}
