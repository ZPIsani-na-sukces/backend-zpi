using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zpi.Modules.Notifications.Application;
using Zpi.Modules.Notifications.Application.Inbox;
using Zpi.Modules.Notifications.Application.Inbox.Get;
using Zpi.Modules.Notifications.Application.Inbox.List;
using Zpi.Modules.Notifications.Application.Inbox.Read;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.Notifications.Api;

[ApiController]
[Authorize]
[Route("api/v1/me/notifications")]
[Tags("Notifications")]
[EnableRateLimiting("notifications")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
public sealed class NotificationsController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<CursorPageDto<NotificationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] ListNotificationsRequest request, [FromServices] ListNotificationsHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await handler.Handle(new ListNotificationsQuery(AccountId(), request), cancellationToken));
    }
    [HttpGet("{notificationId}")]
    [ProducesResponseType<NotificationDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid notificationId, [FromServices] GetNotificationHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return Result(await handler.Handle(new GetNotificationQuery(AccountId(), notificationId), cancellationToken));
    }
    [HttpPut("{notificationId}/read")]
    [ProducesResponseType<NotificationDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Read(Guid notificationId, [FromServices] ReadNotificationHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return Result(await handler.Handle(new ReadNotificationCommand(AccountId(), notificationId), cancellationToken));
    }
    private Guid AccountId() => Guid.Parse(User.FindFirst("sub")!.Value);
    private IActionResult Result(NotificationResult<NotificationDto> result) => result.Failure is null ? Ok(result.Value) :
        Problem(statusCode: 404, detail: "The notification was not found.", extensions: new Dictionary<string, object?> { ["code"] = "resourceNotFound" });
}
