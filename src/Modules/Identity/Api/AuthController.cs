using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Zpi.Modules.Identity.Api.Errors;
using Zpi.Modules.Identity.Application.Accounts;
using Zpi.Modules.Identity.Application.Accounts.ChangePassword;
using Zpi.Modules.Identity.Application.Accounts.CurrentAccount;
using Zpi.Modules.Identity.Application.Accounts.DeleteAccount;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Application.Authentication.Login;
using Zpi.Modules.Identity.Application.Authentication.Logout;
using Zpi.Modules.Identity.Application.Authentication.EmailVerification;
using Zpi.Modules.Identity.Application.Authentication.PasswordReset;
using Zpi.Modules.Identity.Application.Authentication.PhoneVerification;
using Zpi.Modules.Identity.Application.Authentication.Refresh;
using Zpi.Modules.Identity.Application.Authentication.Register;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;

namespace Zpi.Modules.Identity.Api;

[ApiController]
[Route("api/v1/auth")]
[Tags("Authentication")]
[ProducesResponseType<AuthenticationProblem>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public sealed class AuthController(IOptions<AuthenticationOptions> settings) : ControllerBase
{
    [HttpPost("registrations")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<RegistrationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> Register(RegisterCommand request, [FromServices] RegisterHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(request, cancellationToken);
        return result.Failure is null ? Created("/api/v1/me", result.Value) : Failure(result);
    }

    [HttpPost("phone-verifications")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<ChallengeDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> StartPhoneVerification(StartPhoneVerificationCommand command,
        [FromServices] StartPhoneVerificationHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);
        return result.Failure is null ? Accepted(result.Value) : Failure(result);
    }

    [HttpPost("phone-verifications/confirm")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> ConfirmPhoneVerification(ConfirmPhoneVerificationCommand command,
        [FromServices] ConfirmPhoneVerificationHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);
        return result.Failure is null ? NoContent() : Failure(result);
    }

    [HttpPost("email-verifications")]
    [Authorize]
    [EnableRateLimiting("authentication")]
    [ProducesResponseType<ChallengeDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> StartEmailVerification([FromServices] StartEmailVerificationHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new StartEmailVerificationCommand(AccountId()), cancellationToken);
        return result.Failure is null ? Accepted(result.Value) : Failure(result);
    }

    [HttpPost("email-verifications/confirm")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> ConfirmEmailVerification(ConfirmEmailVerificationCommand command,
        [FromServices] ConfirmEmailVerificationHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);
        return result.Failure is null ? NoContent() : Failure(result);
    }

    [HttpPost("password-reset-requests")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<ChallengeDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> RequestPasswordReset(RequestPasswordResetCommand command,
        [FromServices] RequestPasswordResetHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(command, cancellationToken);
        return result.Failure is null ? Accepted(result.Value) : Failure(result);
    }

    [HttpPost("password-resets")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand command, [FromServices] ResetPasswordHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(command, cancellationToken);
        return result.Failure is null ? NoContent() : Failure(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<PhoneVerificationProblem>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Login(LoginCommand request, [FromServices] LoginHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (request.ClientType == ClientType.Web && !HasAllowedOrigin())
            return ProblemResponse(AuthenticationProblem.Create(HttpContext, 403, "operationForbidden", "The web origin is not allowed."));
        var result = await handler.Handle(request, cancellationToken);
        if (result.Failure is not null) return Failure(result);
        return SessionResponse(result.Value!, request.ClientType);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Refresh(RefreshRequest request, [FromServices] RefreshHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (request.ClientType == ClientType.Web && !HasAllowedOrigin())
            return ProblemResponse(AuthenticationProblem.Create(HttpContext, 403, "operationForbidden", "The web origin is not allowed."));
        var result = await handler.Handle(new RefreshCommand(request, Request.Cookies["zpi_refresh_token"]), cancellationToken);
        if (result.Failure is not null)
        {
            ClearRefreshCookie();
            return Failure(result);
        }
        return SessionResponse(result.Value!, request.ClientType);
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Logout([FromServices] LogoutHandler handler, CancellationToken cancellationToken)
    {
        await handler.Handle(new LogoutCommand(Guid.Parse(User.FindFirst("sid")!.Value)), cancellationToken);
        ClearRefreshCookie();
        return NoContent();
    }

    [HttpPut("password")]
    [Authorize]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, [FromServices] ChangePasswordHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ChangePasswordCommand(AccountId(), request), cancellationToken);
        if (result.Failure is not null) return Failure(result);
        ClearRefreshCookie();
        return NoContent();
    }

    [HttpDelete("/api/v1/me")]
    [Authorize]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<AccountDeletionDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> DeleteAccount(DeleteAccountRequest request, [FromServices] DeleteAccountHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new DeleteAccountCommand(AccountId(), request.Password), cancellationToken);
        if (result.Failure is not null) return Failure(result);
        ClearRefreshCookie();
        return Accepted(result.Value);
    }

    private IActionResult SessionResponse(SessionDto session, ClientType clientType)
    {
        var web = clientType == ClientType.Web;
        if (web)
        {
            Response.Cookies.Append("zpi_refresh_token", session.RefreshToken!, Cookie(session.RefreshTokenExpiresAt));
            session = session with { RefreshToken = null };
        }
        return Ok(session);
    }

    private void ClearRefreshCookie()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Cookies.Delete("zpi_refresh_token", Cookie());
    }

    private static CookieOptions Cookie(DateTimeOffset? expires = null) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api/v1/auth",
        Expires = expires
    };

    private Guid AccountId() => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet("/api/v1/me")]
    [Authorize]
    [ProducesResponseType<AccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AuthenticationProblem>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Me([FromServices] CurrentAccountHandler handler, CancellationToken cancellationToken)
    {
        var id = Guid.Parse(User.FindFirst("sub")!.Value);
        var account = await handler.Handle(new CurrentAccountQuery(id), cancellationToken);
        Response.Headers.ETag = $"\"{account.Version}\"";
        Response.Headers.CacheControl = "no-store";
        return Ok(account);
    }

    private bool HasAllowedOrigin()
    {
        var origin = Request.Headers.Origin.ToString();
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            return false;
        var normalized = uri.GetLeftPart(UriPartial.Authority);
        return string.Equals(normalized, $"{Request.Scheme}://{Request.Host}", StringComparison.OrdinalIgnoreCase) ||
            settings.Value.AllowedWebOrigins.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    private IActionResult Failure<T>(AuthenticationResult<T> result)
    {
        if (result.Failure == AuthenticationFailure.PhoneVerificationRequired)
            return ProblemResponse(new PhoneVerificationProblem(HttpContext, result.Verification!));
        var (status, code, description) = result.Failure switch
        {
            AuthenticationFailure.DuplicateAccount => (409, "invalidState", "An account with these details already exists."),
            AuthenticationFailure.InvalidCredentials => (401, "invalidCredentials", "Invalid credentials."),
            AuthenticationFailure.InvalidCode => (400, "invalidCode", "Invalid or expired verification code or ticket."),
            AuthenticationFailure.InvalidState => (409, "invalidState", "This operation is not available in the current state."),
            AuthenticationFailure.DeliveryUnavailable => (503, "deliveryUnavailable", "Message delivery is unavailable."),
            _ => (403, "operationForbidden", "This operation is not allowed.")
        };
        return ProblemResponse(AuthenticationProblem.Create(HttpContext, status, code, description));
    }

    private ObjectResult ProblemResponse(AuthenticationProblem problem) => new(problem)
    {
        StatusCode = problem.Status,
        ContentTypes = { "application/problem+json" }
    };
}
