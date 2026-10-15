using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zpi.Modules.Notifications.Application;
using Zpi.Modules.Notifications.Application.PushTokens;
using Zpi.Modules.Notifications.Application.PushTokens.Register;
using Zpi.Modules.Notifications.Application.PushTokens.Revoke;

namespace Zpi.Modules.Notifications.Api;

[ApiController]
[Authorize]
[Route("api/v1/me/push-tokens")]
[Tags("Notifications")]
[EnableRateLimiting("notifications")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
public sealed class PushTokensController : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(16_384)]
    [ProducesResponseType<PushTokenDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Register(RegisterPushTokenRequest request, [FromServices] RegisterPushTokenHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new RegisterPushTokenCommand(AccountId(), SessionId(), request), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }
    [HttpDelete("{registrationId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(Guid registrationId, [FromServices] RevokePushTokenHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new RevokePushTokenCommand(AccountId(), SessionId(), registrationId), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : NoContent();
    }
    private Guid AccountId() => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid SessionId() => Guid.Parse(User.FindFirst("sid")!.Value);
    private ObjectResult Failure(NotificationFailure failure)
    {
        var (status, code, detail) = failure switch
        {
            NotificationFailure.ResourceNotFound => (404, "resourceNotFound", "The registration was not found."),
            NotificationFailure.TokenAlreadyRegistered => (409, "tokenAlreadyRegistered", "This token is already registered."),
            NotificationFailure.TokenLimitReached => (409, "tokenLimitReached", "An account can have at most 10 active push registrations."),
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };
        return Problem(statusCode: status, detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });
    }
}
