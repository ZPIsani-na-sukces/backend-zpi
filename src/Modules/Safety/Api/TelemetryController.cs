using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zpi.Modules.Safety.Application.Telemetry.Upload;

namespace Zpi.Modules.Safety.Api;

[ApiController]
[Authorize(Roles = "senior")]
[Route("api/v1/me/devices/{deviceId}/telemetry")]
[Tags("Safety")]
[EnableRateLimiting("telemetry")]
public sealed class TelemetryController : SafetyController
{
    [HttpPost]
    [RequestSizeLimit(262_144)]
    [ProducesResponseType<TelemetryBatchResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Upload(Guid deviceId, UploadTelemetryRequest request,
        [FromServices] UploadTelemetryHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new UploadTelemetryCommand(AccountId(), deviceId, request.Samples), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }
}
