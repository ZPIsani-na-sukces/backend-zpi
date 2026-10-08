using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Zpi.Api.ErrorHandling;

internal sealed record ApiProblemResponse(string Type, string Title, int Status, string? Detail, string Instance,
    string Code, string TraceId, IReadOnlyDictionary<string, string[]>? Errors)
{
    public static ApiProblemResponse Create(HttpContext context, int status, string code, string? detail = null,
        IReadOnlyDictionary<string, string[]>? errors = null) => new("about:blank", ReasonPhrases.GetReasonPhrase(status),
            status, detail, context.Request.PathBase + context.Request.Path, code, context.TraceIdentifier, errors);

    [JsonExtensionData]
    public IDictionary<string, object?> Extensions { get; init; } = new Dictionary<string, object?>();

    public static ApiProblemResponse FromProblemDetails(HttpContext context, ProblemDetails problem) => Create(context,
        problem.Status ?? context.Response.StatusCode,
        problem.Extensions.TryGetValue("code", out var code) && code is string errorCode
            ? errorCode : DefaultCode(problem.Status ?? context.Response.StatusCode),
        problem.Detail,
        problem is ValidationProblemDetails validation ? validation.Errors.ToDictionary(pair => pair.Key, pair => pair.Value)
            : problem.Extensions.TryGetValue("errors", out var errors) ? errors as IReadOnlyDictionary<string, string[]> : null) with
        { Extensions = problem.Extensions.Where(pair => pair.Key is not ("code" or "traceId" or "errors"))
            .ToDictionary(pair => pair.Key, pair => pair.Value) };

    private static string DefaultCode(int status) => status switch
    {
        400 => "validationFailed",
        401 => "unauthenticated",
        403 => "operationForbidden",
        404 => "resourceNotFound",
        405 => "methodNotAllowed",
        409 => "invalidState",
        410 => "resourceGone",
        412 => "versionMismatch",
        413 => "payloadTooLarge",
        415 => "unsupportedMediaType",
        416 => "rangeNotSatisfiable",
        428 => "preconditionRequired",
        429 => "rateLimitExceeded",
        503 => "temporarilyUnavailable",
        _ => status >= 500 ? "internalServerError" : "requestFailed"
    };
}
