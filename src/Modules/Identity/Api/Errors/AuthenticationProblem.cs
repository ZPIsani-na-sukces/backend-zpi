using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Api.Errors;

public record AuthenticationProblem(string Type, string Title, int Status, string? Detail, string Instance,
    string Code, string TraceId, IReadOnlyDictionary<string, string[]>? Errors)
{
    public static AuthenticationProblem Create(HttpContext context, int status, string code, string? detail = null,
        IReadOnlyDictionary<string, string[]>? errors = null) => new("about:blank", ReasonPhrases.GetReasonPhrase(status),
            status, detail, context.Request.Path, code, context.TraceIdentifier, errors);

    public static Task WriteAsync(HttpContext context, int status, string code)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(Create(context, status, code), options: null,
            contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}

public sealed record PhoneVerificationProblem : AuthenticationProblem
{
    public string VerificationTicket { get; }
    public DateTimeOffset TicketExpiresAt { get; }

    public PhoneVerificationProblem(HttpContext context, PhoneVerificationDto verification)
        : base("about:blank", "Forbidden", 403, "Verify your phone number before signing in.", context.Request.Path,
            "phoneVerificationRequired", context.TraceIdentifier, null)
    {
        VerificationTicket = verification.VerificationTicket;
        TicketExpiresAt = verification.TicketExpiresAt;
    }
}
