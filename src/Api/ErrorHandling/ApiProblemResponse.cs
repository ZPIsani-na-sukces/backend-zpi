using Microsoft.AspNetCore.WebUtilities;

namespace Zpi.Api.ErrorHandling;

internal sealed record ApiProblemResponse(string Type, string Title, int Status, string? Detail,
    string Instance, string Code, string TraceId, object? Errors)
{
    public static ApiProblemResponse Create(HttpContext context, int status, string code, string? detail = null,
        object? errors = null) => new("about:blank", ReasonPhrases.GetReasonPhrase(status), status, detail,
            context.Request.PathBase + context.Request.Path, code, context.TraceIdentifier, errors);
}
