using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zpi.Modules.SeniorProfile.Application;
using Zpi.Modules.SeniorProfile.Application.Accessibility;
using Zpi.Modules.SeniorProfile.Application.Accessibility.Get;
using Zpi.Modules.SeniorProfile.Application.Accessibility.Save;
using Zpi.Shared.Versioning;

namespace Zpi.Modules.SeniorProfile.Api;

[ApiController]
[Authorize]
[Route("api/v1/seniors/{seniorId}/accessibility-preferences")]
[Tags("SeniorProfile")]
[EnableRateLimiting("seniorProfile")]
public sealed class AccessibilityPreferencesController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AccessibilityPreferencesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(Guid seniorId, [FromServices] GetAccessibilityPreferencesHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new GetAccessibilityPreferencesQuery(AccountId(), seniorId), cancellationToken);
        return ResponseFor(result);
    }

    [HttpPut]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<AccessibilityPreferencesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public async Task<IActionResult> Save(Guid seniorId, SaveAccessibilityPreferencesRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch, [FromServices] SaveAccessibilityPreferencesHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (string.IsNullOrWhiteSpace(ifMatch))
            return Failure(428, "preconditionRequired", "Provide the resource ETag in If-Match.");
        if (!EntityTag.TryParseVersion(ifMatch, out var version))
            return Failure(400, "validationFailed", "If-Match must contain a quoted positive resource version.");
        var result = await handler.Handle(new SaveAccessibilityPreferencesCommand(AccountId(), seniorId,
            request.FontScalePercent, request.HighContrastEnabled, request.LargeButtonsEnabled, version), cancellationToken);
        return ResponseFor(result);
    }

    private IActionResult ResponseFor(SeniorProfileResult<AccessibilityPreferencesDto> result)
    {
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
