using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zpi.Modules.Care.Application;
using Zpi.Modules.Care.Application.Invitations;
using Zpi.Modules.Care.Application.Invitations.Accept;
using Zpi.Modules.Care.Application.Invitations.Cancel;
using Zpi.Modules.Care.Application.Invitations.Create;
using Zpi.Modules.Care.Application.Invitations.Get;
using Zpi.Modules.Care.Application.Invitations.List;
using Zpi.Modules.Care.Application.Invitations.Reject;
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
    [Authorize(Roles = AccountRoles.Caretaker)]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<CareInvitationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> CreateInvitation(CreateInvitationRequest request,
        [FromServices] CreateInvitationHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CreateInvitationCommand(AccountId(), request.RecipientIdentifier), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : CreatedAtAction(nameof(GetInvitation),
            new { invitationId = result.Value!.InvitationId }, result.Value);
    }

    [HttpGet("care-invitations/{invitationId}")]
    [ProducesResponseType<CareInvitationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetInvitation(Guid invitationId, [FromServices] GetInvitationHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetInvitationQuery(AccountId(), invitationId), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpGet("me/care-invitations")]
    [ProducesResponseType<PageDto<CareInvitationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListInvitations([FromQuery] ListInvitationsRequest request,
        [FromServices] ListInvitationsHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListInvitationsQuery(AccountId(), request.Direction, request.Status, request.Page, request.Limit), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpPost("care-invitations/{invitationId}/accept")]
    [Authorize(Roles = AccountRoles.Senior)]
    [ProducesResponseType<CareRelationshipDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone, "application/problem+json")]
    public async Task<IActionResult> AcceptInvitation(Guid invitationId, [FromServices] AcceptInvitationHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new AcceptInvitationCommand(AccountId(), invitationId), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpPost("care-invitations/{invitationId}/reject")]
    [Authorize(Roles = AccountRoles.Senior)]
    [ProducesResponseType<CareInvitationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone, "application/problem+json")]
    public async Task<IActionResult> RejectInvitation(Guid invitationId, [FromServices] RejectInvitationHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new RejectInvitationCommand(AccountId(), invitationId), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpDelete("care-invitations/{invitationId}")]
    [Authorize(Roles = AccountRoles.Caretaker)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> CancelInvitation(Guid invitationId, [FromServices] CancelInvitationHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CancelInvitationCommand(AccountId(), invitationId), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : NoContent();
    }

    [HttpGet("me/seniors")]
    [Authorize(Roles = AccountRoles.Caretaker)]
    [ProducesResponseType<PageDto<SeniorSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSeniors([FromQuery] ListSeniorsRequest request,
        [FromServices] ListSeniorsHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListSeniorsQuery(AccountId(), request.Search, request.Page, request.Limit), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpGet("seniors/{seniorId}/caretakers")]
    [ProducesResponseType<PageDto<CareRelationshipDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> ListCaretakers(Guid seniorId, [FromQuery] ListCaretakersRequest request,
        [FromServices] ListCaretakersHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListCaretakersQuery(AccountId(), seniorId, request.Page, request.Limit), cancellationToken);
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
            CareFailure.InvitationExpired => (410, "invitationExpired", "The invitation has expired."),
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };
        return Problem(statusCode: status, detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });
    }
}
