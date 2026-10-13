using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zpi.Modules.Safety.Application.Locations.Latest;

namespace Zpi.Modules.Safety.Api;

[ApiController]
[Authorize]
[Route("api/v1/seniors/{seniorId}/locations")]
[Tags("Safety")]
[EnableRateLimiting("safety")]
public sealed class LocationsController : SafetyController
{
    [HttpGet("latest")]
    [ProducesResponseType<LatestLocationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Latest(Guid seniorId, [FromServices] LatestLocationHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new LatestLocationQuery(AccountId(), seniorId), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }
}
