using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Zpi.Modules.Identity.Application.Accounts;
using Zpi.Modules.Identity.Application.Accounts.UpdateProfile;
using Zpi.Shared.Versioning;

namespace Zpi.Modules.Identity.Api;

[ApiController]
[Authorize]
[Route("api/v1/me/profile")]
[Tags("Identity")]
public sealed class ProfileController : ControllerBase
{
    [HttpPut]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<AccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public async Task<IActionResult> Update(UpdateProfileRequest request, [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] UpdateProfileHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (string.IsNullOrWhiteSpace(ifMatch))
            return Failure(428, "preconditionRequired", "Provide the resource ETag in If-Match.");
        if (!EntityTag.TryParseVersion(ifMatch, out var version))
            return Failure(400, "validationFailed", "If-Match must contain a quoted positive resource version.");
        var result = await handler.Handle(new UpdateProfileCommand(Guid.Parse(User.FindFirst("sub")!.Value),
            request.FirstName, request.LastName, version), cancellationToken);
        if (result.Failure == AccountFailure.ResourceNotFound)
            return Failure(404, "resourceNotFound", "The account is not available.");
        if (result.Failure == AccountFailure.VersionMismatch)
            return Failure(412, "versionMismatch", "The resource has changed. Reload it before retrying.");
        Response.Headers.ETag = EntityTag.FromVersion(result.Value!.Version);
        return Ok(result.Value);
    }

    private ObjectResult Failure(int status, string code, string detail) =>
        Problem(statusCode: status, detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });
}
