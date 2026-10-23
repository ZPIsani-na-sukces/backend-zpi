using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zpi.Modules.Identity.Application.Authentication.EmailVerification;
using Zpi.Modules.Identity.Application.Authentication.Register;

namespace Zpi.Modules.Identity.Api;

[ApiController]
[Route("api/v1/auth")]
[Tags("Authentication")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public sealed class EmailVerificationController : ControllerBase
{
    [HttpPost("email-verifications")]
    [Authorize]
    [EnableRateLimiting("authentication")]
    [ProducesResponseType<ChallengeDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> StartEmailVerification([FromServices] StartEmailVerificationHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new StartEmailVerificationCommand(AccountId()), cancellationToken);
        return result.Failure is null ? Accepted(result.Value) : this.Failure(result);
    }

    [HttpPost("email-verifications/confirm")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> ConfirmEmailVerification(ConfirmEmailVerificationCommand command,
        [FromServices] ConfirmEmailVerificationHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);
        return result.Failure is null ? NoContent() : this.Failure(result);
    }

    private Guid AccountId() => Guid.Parse(User.FindFirst("sub")!.Value);
}
