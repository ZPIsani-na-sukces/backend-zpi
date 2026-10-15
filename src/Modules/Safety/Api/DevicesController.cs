using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zpi.Modules.Safety.Application.Devices;
using Zpi.Modules.Safety.Application.Devices.List;
using Zpi.Modules.Safety.Application.Devices.Register;
using Zpi.Modules.Safety.Application.Devices.Retire;
using Zpi.Modules.Safety.Application.Devices.Switch;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Safety.Api;

[ApiController]
[Authorize]
[Route("api/v1")]
[Tags("Safety")]
[EnableRateLimiting("safety")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
public sealed class DevicesController : SafetyController
{
    [HttpPost("me/devices")]
    [RequestSizeLimit(16_384)]
    [ProducesResponseType<DeviceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<DeviceDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> Register(RegisterDeviceRequest request, [FromServices] RegisterDeviceHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new RegisterDeviceCommand(AccountId(), request), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        var registration = result.Value!;
        return registration.Created ? Created("/api/v1/me/devices", registration.Device) : Ok(registration.Device);
    }

    [HttpGet("me/devices")]
    [ProducesResponseType<IReadOnlyList<DeviceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromServices] ListDevicesHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new ListDevicesQuery(AccountId()), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpGet("seniors/{seniorId}/devices")]
    [ProducesResponseType<IReadOnlyList<DeviceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSenior(Guid seniorId, [FromServices] ListDevicesHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new ListDevicesQuery(AccountId(), seniorId), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpPost("me/devices/{deviceId}/switch")]
    [Authorize(Roles = AccountRoles.Senior)]
    [ProducesResponseType<DeviceDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Switch(Guid deviceId, [FromServices] SwitchDeviceHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new SwitchDeviceCommand(AccountId(), deviceId), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpDelete("me/devices/{deviceId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Retire(Guid deviceId, [FromServices] RetireDeviceHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new RetireDeviceCommand(AccountId(), deviceId), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : NoContent();
    }
}
