using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zpi.Modules.Identity.Application.Accounts;
using Zpi.Modules.Identity.Application.Accounts.UpdateEmail;
using Zpi.Modules.Identity.Application.Accounts.UpdatePhoneNumber;
using Zpi.Modules.Identity.Application.Accounts.UpdateProfile;
using Zpi.Modules.Identity.Application.Authentication.Register;
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
        var result = await handler.Handle(new UpdateProfileCommand(AccountId(),
            request.FirstName, request.LastName, version), cancellationToken);
        return ResponseFor(result);
    }

    [HttpPost("email/change-requests")]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<ChallengeDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> StartEmailChange(StartEmailChangeRequest request,
        [FromServices] StartEmailChangeHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new StartEmailChangeCommand(AccountId(), request.Email), cancellationToken);
        return result.Failure is null ? Accepted(result.Value) : this.Failure(result);
    }

    [HttpPut("email")]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<AccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public async Task<IActionResult> UpdateEmail(UpdateEmailRequest request, [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] UpdateEmailHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (string.IsNullOrWhiteSpace(ifMatch))
            return Failure(428, "preconditionRequired", "Provide the resource ETag in If-Match.");
        if (!EntityTag.TryParseVersion(ifMatch, out var version))
            return Failure(400, "validationFailed", "If-Match must contain a quoted positive resource version.");
        var result = await handler.Handle(new UpdateEmailCommand(AccountId(), request.ChallengeId, request.Code, version), cancellationToken);
        return ResponseFor(result);
    }

    [HttpPost("phone-number/change-requests")]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<ChallengeDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> StartPhoneNumberChange(StartPhoneNumberChangeRequest request,
        [FromServices] StartPhoneNumberChangeHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new StartPhoneNumberChangeCommand(AccountId(), request.PhoneNumber), cancellationToken);
        return result.Failure is null ? Accepted(result.Value) : this.Failure(result);
    }

    [HttpPut("phone-number")]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<AccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public async Task<IActionResult> UpdatePhoneNumber(UpdatePhoneNumberRequest request, [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] UpdatePhoneNumberHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (string.IsNullOrWhiteSpace(ifMatch))
            return Failure(428, "preconditionRequired", "Provide the resource ETag in If-Match.");
        if (!EntityTag.TryParseVersion(ifMatch, out var version))
            return Failure(400, "validationFailed", "If-Match must contain a quoted positive resource version.");
        var result = await handler.Handle(new UpdatePhoneNumberCommand(AccountId(), request.ChallengeId, request.Code, version), cancellationToken);
        return ResponseFor(result);
    }

    private IActionResult ResponseFor(AccountResult<AccountDto> result)
    {
        if (result.Failure == AccountFailure.ResourceNotFound)
            return Failure(404, "resourceNotFound", "The account is not available.");
        if (result.Failure == AccountFailure.VersionMismatch)
            return Failure(412, "versionMismatch", "The resource has changed. Reload it before retrying.");
        if (result.Failure == AccountFailure.DuplicateAccount)
            return Failure(409, "invalidState", "An account with these details already exists.");
        if (result.Failure == AccountFailure.InvalidCode)
            return Failure(400, "invalidCode", "Invalid or expired verification code or ticket.");
        Response.Headers.ETag = EntityTag.FromVersion(result.Value!.Version);
        return Ok(result.Value);
    }

    private Guid AccountId() => Guid.Parse(User.FindFirst("sub")!.Value);

    private ObjectResult Failure(int status, string code, string detail) =>
        Problem(statusCode: status, detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });
}
