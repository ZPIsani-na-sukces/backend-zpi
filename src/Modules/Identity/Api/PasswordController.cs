using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zpi.Modules.Identity.Application.Accounts.ChangePassword;
using Zpi.Modules.Identity.Application.Authentication.PasswordReset;
using Zpi.Modules.Identity.Application.Authentication.Register;

namespace Zpi.Modules.Identity.Api;

[ApiController]
[Route("api/v1/auth")]
[Tags("Authentication")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public sealed class PasswordController : ControllerBase
{
    [HttpPost("password-reset-requests")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<ChallengeDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> RequestPasswordReset(RequestPasswordResetCommand command,
        [FromServices] RequestPasswordResetHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(command, cancellationToken);
        return result.Failure is null ? Accepted(result.Value) : this.Failure(result);
    }

    [HttpPost("password-resets/verify")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> VerifyPasswordResetCode(VerifyPasswordResetCodeCommand command,
        [FromServices] VerifyPasswordResetCodeHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(command, cancellationToken);
        return result.Failure is null ? NoContent() : this.Failure(result);
    }

    [HttpPost("password-resets")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand command, [FromServices] ResetPasswordHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(command, cancellationToken);
        return result.Failure is null ? NoContent() : this.Failure(result);
    }

    [HttpPut("password")]
    [Authorize]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, [FromServices] ChangePasswordHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ChangePasswordCommand(AccountId(), request), cancellationToken);
        if (result.Failure is not null) return this.Failure(result);
        RefreshTokenCookie.Clear(Response);
        return NoContent();
    }

    private Guid AccountId() => Guid.Parse(User.FindFirst("sub")!.Value);
}
