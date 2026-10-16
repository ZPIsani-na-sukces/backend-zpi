using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Safety.Application.Geozones;
using Zpi.Modules.Safety.Application.Geozones.Get;
using Zpi.Modules.Safety.Application.Geozones.List;
using Zpi.Modules.Safety.Application.Geozones.Create;
using Zpi.Modules.Safety.Application.Geozones.Update;
using Zpi.Modules.Safety.Application.Geozones.Delete;
using Zpi.Shared.Versioning;

namespace Zpi.Modules.Safety.Api;

[ApiController]
[Authorize]
[Route("api/v1/seniors/{seniorId}/geozones")]
[Tags("Safety")]
[EnableRateLimiting("safety")]
[ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
public sealed class GeoZonesController : SafetyController
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GeoZoneDto>>(200)]
    public async Task<IActionResult> List(Guid seniorId, [FromQuery] bool? isActive, [FromServices] ListGeoZonesHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new(AccountId(), seniorId, isActive), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpGet("{zoneId}")]
    [ProducesResponseType<GeoZoneDto>(200)]
    public async Task<IActionResult> Get(Guid seniorId, Guid zoneId, [FromServices] GetGeoZonesHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new(AccountId(), seniorId, zoneId), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        Response.Headers.ETag = EntityTag.FromVersion(result.Value!.Version);
        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Roles = AccountRoles.Caretaker)]
    [RequestSizeLimit(8192)]
    [ProducesResponseType<GeoZoneDto>(201)]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    public async Task<IActionResult> Create(Guid seniorId, SaveGeoZoneRequest request, [FromServices] CreateGeoZoneHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new(AccountId(), seniorId, request), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        Response.Headers.ETag = EntityTag.FromVersion(result.Value!.Version);
        return CreatedAtAction(nameof(Get), new { seniorId, zoneId = result.Value.ZoneId }, result.Value);
    }

    [HttpPut("{zoneId}")]
    [Authorize(Roles = AccountRoles.Caretaker)]
    [RequestSizeLimit(8192)]
    [ProducesResponseType<GeoZoneDto>(200)]
    [ProducesResponseType<ProblemDetails>(412, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(428, "application/problem+json")]
    public async Task<IActionResult> Update(Guid seniorId, Guid zoneId, SaveGeoZoneRequest request, [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] UpdateGeoZoneHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var error = CheckVersion(ifMatch, out var version);
        if (error is not null) return error;
        var result = await handler.Handle(new(AccountId(), seniorId, zoneId, version, request), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        Response.Headers.ETag = EntityTag.FromVersion(result.Value!.Version);
        return Ok(result.Value);
    }

    [HttpDelete("{zoneId}")]
    [Authorize(Roles = AccountRoles.Caretaker)]
    [RequestSizeLimit(8192)]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(412, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(428, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid seniorId, Guid zoneId, [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] DeleteGeoZoneHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var error = CheckVersion(ifMatch, out var version);
        if (error is not null) return error;
        var result = await handler.Handle(new(AccountId(), seniorId, zoneId, version), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : NoContent();
    }
}
