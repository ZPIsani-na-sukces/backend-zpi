using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zpi.Modules.Care.Application;
using Zpi.Modules.Care.Application.Invitations;
using Zpi.Modules.Care.Application.Invitations.Accept;
using Zpi.Modules.Care.Application.Invitations.Create;
using Zpi.Modules.Care.Application.Invitations.Get;
using Zpi.Shared.Pagination;
using Zpi.Modules.Care.Application.Relationships;
using Zpi.Modules.Care.Application.Relationships.ListCaretakers;
using Zpi.Modules.Care.Application.Relationships.ListSeniors;
using Zpi.Modules.Care.Application.Relationships.Remove;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Care.Api;

[ApiController]
[Authorize]
[Route("api/v1")]
[Tags("Care")]
[EnableRateLimiting("care")]
public sealed class CareController : ControllerBase
{
    [HttpPost("care-invitations")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<CareInvitationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<CareInvitationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> CreateInvitation([FromServices] CreateInvitationHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CreateInvitationCommand(AccountId()), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        return StatusCode(result.Value!.Created ? StatusCodes.Status201Created : StatusCodes.Status200OK, result.Value.Invitation);
    }

    [HttpGet("care-invitations/{code}")]
    [EnableRateLimiting(CareApiSetup.InvitationCodePolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<InvitationPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> GetInvitation([FromRoute] GetInvitationRequest request,
        [FromServices] GetInvitationHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetInvitationQuery(AccountId(), request.Code), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpPost("care-invitations/accept")]
    [EnableRateLimiting(CareApiSetup.InvitationCodePolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [RequestSizeLimit(8_192)]
    [ProducesResponseType<CareRelationshipDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> AcceptInvitation(InvitationCodeRequest request,
        [FromServices] AcceptInvitationHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new AcceptInvitationCommand(AccountId(), request.Code), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpGet("me/seniors")]
    [Authorize(Roles = AccountRoles.Caretaker)]
    [ProducesResponseType<CursorPageDto<SeniorSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSeniors([FromQuery] ListSeniorsRequest request,
        [FromServices] ListSeniorsHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListSeniorsQuery(AccountId(), request.Search, request.Cursor, request.Limit), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpGet("seniors/{seniorId}/caretakers")]
    [ProducesResponseType<CursorPageDto<CareRelationshipDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> ListCaretakers(Guid seniorId, [FromQuery] ListCaretakersRequest request,
        [FromServices] ListCaretakersHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListCaretakersQuery(AccountId(), seniorId, request.Cursor, request.Limit), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpDelete("care-relationships/{relationshipId}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> RemoveRelationship(Guid relationshipId, [FromServices] RemoveRelationshipHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new RemoveRelationshipCommand(AccountId(), relationshipId), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : NoContent();
    }

    private Guid AccountId() => Guid.Parse(User.FindFirst("sub")!.Value);

    private ObjectResult Failure(CareFailure failure)
    {
        var (status, code, detail) = failure switch
        {
            CareFailure.ResourceNotFound => (404, "resourceNotFound", "The resource was not found."),
            CareFailure.OperationForbidden => (403, "operationForbidden", "This operation is not allowed."),
            CareFailure.InvalidState => (409, "invalidState", "This operation is not available in the current state."),
            CareFailure.RelationshipAlreadyExists => (409, "relationshipAlreadyExists", "An active care relationship already exists."),
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };
        return Problem(statusCode: status, detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });
    }
}
