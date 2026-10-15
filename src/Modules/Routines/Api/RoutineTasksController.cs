using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zpi.Modules.Routines.Application.Tasks;
using Zpi.Modules.Routines.Application.Tasks.Create;
using Zpi.Modules.Routines.Application.Tasks.Delete;
using Zpi.Modules.Routines.Application.Tasks.Get;
using Zpi.Modules.Routines.Application.Tasks.List;
using Zpi.Modules.Routines.Application.Tasks.Update;
using Zpi.Shared.Pagination;
using Zpi.Shared.Versioning;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Routines.Api;

[ApiController]
[Authorize]
[Route("api/v1/seniors/{seniorId}/routine-tasks")]
[Tags("Routines")]
[EnableRateLimiting("routines")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
public sealed class RoutineTasksController : RoutinesController
{
    [HttpGet]
    [ProducesResponseType<PageDto<RoutineTaskDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid seniorId, [FromQuery] ListRoutineTasksRequest request,
        [FromServices] ListRoutineTasksHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new ListRoutineTasksQuery(AccountId(), seniorId, request.Type, request.IsActive,
            request.Search, request.Page, request.Limit), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpGet("{taskId}")]
    [ProducesResponseType<RoutineTaskDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid seniorId, Guid taskId, [FromServices] GetRoutineTaskHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new GetRoutineTaskQuery(AccountId(), seniorId, taskId), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        Response.Headers.ETag = EntityTag.FromVersion(result.Value!.Version);
        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Roles = AccountRoles.Caretaker)]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<RoutineTaskDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Create(Guid seniorId, SaveRoutineTaskRequest request, [FromServices] CreateRoutineTaskHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new CreateRoutineTaskCommand(AccountId(), seniorId, request), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        Response.Headers.ETag = EntityTag.FromVersion(result.Value!.Version);
        return CreatedAtAction(nameof(Get), new { seniorId, taskId = result.Value.TaskId }, result.Value);
    }

    [HttpPut("{taskId}")]
    [Authorize(Roles = AccountRoles.Caretaker)]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<RoutineTaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public async Task<IActionResult> Update(Guid seniorId, Guid taskId, SaveRoutineTaskRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch, [FromServices] UpdateRoutineTaskHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var precondition = CheckVersion(ifMatch, out var version);
        if (precondition is not null) return precondition;
        var result = await handler.Handle(new UpdateRoutineTaskCommand(AccountId(), seniorId, taskId, request, version), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        Response.Headers.ETag = EntityTag.FromVersion(result.Value!.Version);
        return Ok(result.Value);
    }

    [HttpDelete("{taskId}")]
    [Authorize(Roles = AccountRoles.Caretaker)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid seniorId, Guid taskId, [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] DeleteRoutineTaskHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var precondition = CheckVersion(ifMatch, out var version);
        if (precondition is not null) return precondition;
        var result = await handler.Handle(new DeleteRoutineTaskCommand(AccountId(), seniorId, taskId, version), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : NoContent();
    }
}
