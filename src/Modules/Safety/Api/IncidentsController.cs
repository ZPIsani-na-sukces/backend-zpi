using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zpi.Modules.Safety.Application.Incidents;
using Zpi.Modules.Safety.Application.Incidents.Acknowledge;
using Zpi.Modules.Safety.Application.Incidents.Get;
using Zpi.Modules.Safety.Application.Incidents.List;
using Zpi.Modules.Safety.Application.Incidents.Resolve;
using Zpi.Modules.Safety.Application.Incidents.Trigger;
using Zpi.Shared.Pagination;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Safety.Api;

[ApiController]
[Authorize]
[Route("api/v1/seniors/{seniorId}")]
[Tags("Safety")]
[EnableRateLimiting("safety")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
public sealed class IncidentsController : SafetyController
{
    [HttpPost("sos-incidents")]
    [Authorize(Roles = AccountRoles.Senior)]
    [RequestSizeLimit(16_384)]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Trigger(Guid seniorId, TriggerSosRequest request, [FromServices] TriggerSosHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new TriggerSosCommand(AccountId(), seniorId, request), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        var value = result.Value!;
        return value.Created ? CreatedAtAction(nameof(Get), new { seniorId, incidentId = value.Incident.IncidentId }, value.Incident) : Ok(value.Incident);
    }

    [HttpGet("incidents")]
    [ProducesResponseType<CursorPageDto<IncidentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid seniorId, [FromQuery] ListIncidentsRequest request, [FromServices] ListIncidentsHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new ListIncidentsQuery(AccountId(), seniorId, request), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpGet("incidents/{incidentId}")]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid seniorId, Guid incidentId, [FromServices] GetIncidentHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new GetIncidentQuery(AccountId(), seniorId, incidentId), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpPost("incidents/{incidentId}/acknowledge")]
    [Authorize(Roles = AccountRoles.Caretaker)]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Acknowledge(Guid seniorId, Guid incidentId, [FromServices] AcknowledgeIncidentHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new AcknowledgeIncidentCommand(AccountId(), seniorId, incidentId), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpPost("incidents/{incidentId}/resolve")]
    [Authorize(Roles = AccountRoles.Caretaker)]
    [RequestSizeLimit(8192)]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Resolve(Guid seniorId, Guid incidentId, ResolveIncidentRequest request,
        [FromServices] ResolveIncidentHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new ResolveIncidentCommand(AccountId(), seniorId, incidentId, request.Note), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }
}
