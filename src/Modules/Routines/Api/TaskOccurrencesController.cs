using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zpi.Modules.Routines.Application.Occurrences;
using Zpi.Modules.Routines.Application.Occurrences.Decision;
using Zpi.Modules.Routines.Application.Occurrences.Get;
using Zpi.Modules.Routines.Application.Occurrences.List;
using Zpi.Shared.Pagination;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Routines.Api;

[ApiController]
[Authorize]
[Route("api/v1/seniors/{seniorId}/task-occurrences")]
[Tags("Routines")]
[EnableRateLimiting("routines")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
public sealed class TaskOccurrencesController : RoutinesController
{
    [HttpGet]
    [ProducesResponseType<PageDto<TaskOccurrenceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid seniorId, [FromQuery] ListOccurrencesRequest request,
        [FromServices] ListOccurrencesHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new ListOccurrencesQuery(AccountId(), seniorId, request.From!.Value,
            request.To!.Value, request.Type, request.Status, request.TaskId, request.Page, request.Limit), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpGet("{occurrenceId}")]
    [ProducesResponseType<TaskOccurrenceDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid seniorId, Guid occurrenceId, [FromServices] GetOccurrenceHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new GetOccurrenceQuery(AccountId(), seniorId, occurrenceId), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpPut("{occurrenceId}/decision")]
    [Authorize(Roles = AccountRoles.Senior)]
    [RequestSizeLimit(16_384)]
    [ProducesResponseType<TaskOccurrenceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Decide(Guid seniorId, Guid occurrenceId, RecordTaskDecisionRequest request,
        [FromServices] RecordTaskDecisionHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new RecordTaskDecisionCommand(AccountId(), seniorId, occurrenceId, request), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }
}
