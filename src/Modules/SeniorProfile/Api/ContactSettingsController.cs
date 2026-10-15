using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zpi.Modules.SeniorProfile.Application;
using Zpi.Modules.SeniorProfile.Application.Contacts.Settings;
using Zpi.Shared.Versioning;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.SeniorProfile.Api;

[ApiController]
[Authorize]
[Route("api/v1/seniors/{seniorId}/contacts/settings")]
[Tags("SeniorProfile")]
[EnableRateLimiting("seniorProfile")]
public sealed class ContactSettingsController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ContactSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(Guid seniorId, [FromServices] GetContactSettingsHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new GetContactSettingsQuery(AccountId(), seniorId), cancellationToken);
        if (result.Failure is not null) return Failure(404, "resourceNotFound", "The senior is not available.");
        Response.Headers.ETag = EntityTag.FromVersion(result.Value!.Version);
        return Ok(result.Value);
    }

    [HttpPut]
    [Authorize(Roles = AccountRoles.Caretaker)]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<ContactSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public async Task<IActionResult> Save(Guid seniorId, SaveContactSettingsRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch, [FromServices] SaveContactSettingsHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (string.IsNullOrWhiteSpace(ifMatch))
            return Failure(428, "preconditionRequired", "Provide the resource ETag in If-Match.");
        if (!EntityTag.TryParseVersion(ifMatch, out var version))
            return Failure(400, "validationFailed", "If-Match must contain a quoted positive resource version.");
        var result = await handler.Handle(new SaveContactSettingsCommand(AccountId(), seniorId,
            request.SeniorEditingLocked, version), cancellationToken);
        if (result.Failure == SeniorProfileFailure.ResourceNotFound)
            return Failure(404, "resourceNotFound", "The senior is not available.");
        if (result.Failure == SeniorProfileFailure.VersionMismatch)
            return Failure(412, "versionMismatch", "The resource has changed. Reload it before retrying.");
        Response.Headers.ETag = EntityTag.FromVersion(result.Value!.Version);
        return Ok(result.Value);
    }

    private Guid AccountId() => Guid.Parse(User.FindFirst("sub")!.Value);

    private ObjectResult Failure(int status, string code, string detail) =>
        Problem(statusCode: status, detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });
}
