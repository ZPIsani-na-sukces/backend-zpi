using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Application.Authentication.Login;
using Zpi.Modules.Identity.Application.Authentication.Logout;
using Zpi.Modules.Identity.Application.Authentication.Refresh;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;

namespace Zpi.Modules.Identity.Api;

[ApiController]
[Route("api/v1/auth")]
[Tags("Authentication")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public sealed class AuthController(IOptions<AuthenticationOptions> settings) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Login(LoginCommand request, [FromServices] LoginHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (request.ClientType == ClientType.Web && !HasAllowedOrigin())
            return ProblemResponse(403, "operationForbidden", "The web origin is not allowed.");
        var result = await handler.Handle(request, cancellationToken);
        if (result.Failure is not null) return this.Failure(result);
        return SessionResponse(result.Value!, request.ClientType);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Refresh(RefreshRequest request, [FromServices] RefreshHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (request.ClientType == ClientType.Web && !HasAllowedOrigin())
            return ProblemResponse(403, "operationForbidden", "The web origin is not allowed.");
        var result = await handler.Handle(new RefreshCommand(request, Request.Cookies[RefreshTokenCookie.Name]), cancellationToken);
        if (result.Failure is not null)
        {
            RefreshTokenCookie.Clear(Response);
            return this.Failure(result);
        }
        return SessionResponse(result.Value!, request.ClientType);
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Logout([FromServices] LogoutHandler handler, CancellationToken cancellationToken)
    {
        await handler.Handle(new LogoutCommand(Guid.Parse(User.FindFirst("sid")!.Value)), cancellationToken);
        RefreshTokenCookie.Clear(Response);
        return NoContent();
    }

    private IActionResult SessionResponse(SessionDto session, ClientType clientType)
    {
        if (clientType != ClientType.Web) return Ok(session);
        RefreshTokenCookie.Append(Response, session.RefreshToken!, session.RefreshTokenExpiresAt);
        return Ok(session with { RefreshToken = null });
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

    private ObjectResult ProblemResponse(int status, string code, string detail) =>
        Problem(statusCode: status, detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });
}
