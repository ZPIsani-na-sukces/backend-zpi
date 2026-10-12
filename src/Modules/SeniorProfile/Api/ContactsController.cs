using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zpi.Modules.SeniorProfile.Application;
using Zpi.Modules.SeniorProfile.Application.Contacts;
using Zpi.Modules.SeniorProfile.Application.Contacts.Create;
using Zpi.Modules.SeniorProfile.Application.Contacts.Delete;
using Zpi.Modules.SeniorProfile.Application.Contacts.Get;
using Zpi.Modules.SeniorProfile.Application.Contacts.List;
using Zpi.Modules.SeniorProfile.Application.Contacts.Update;
using Zpi.Shared.Pagination;
using Zpi.Shared.Versioning;

namespace Zpi.Modules.SeniorProfile.Api;

[ApiController]
[Authorize]
[Route("api/v1/seniors/{seniorId}/contacts")]
[Tags("SeniorProfile")]
[EnableRateLimiting("seniorProfile")]
public sealed class ContactsController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PageDto<ContactDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> List(Guid seniorId, [FromQuery] ListContactsRequest request,
        [FromServices] ListContactsHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new ListContactsQuery(AccountId(), seniorId, request.Search, request.IsActive,
            request.Sort, request.Page, request.Limit), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpGet("{contactId}")]
    [ProducesResponseType<ContactDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(Guid seniorId, Guid contactId, [FromServices] GetContactHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new GetContactQuery(AccountId(), seniorId, contactId), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        Response.Headers.ETag = EntityTag.FromVersion(result.Value!.Version);
        return Ok(result.Value);
    }

    [HttpPost]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<ContactDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Create(Guid seniorId, SaveContactRequest request,
        [FromServices] CreateContactHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new CreateContactCommand(AccountId(), seniorId, request), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        Response.Headers.ETag = EntityTag.FromVersion(result.Value!.Version);
        return CreatedAtAction(nameof(Get), new { seniorId, contactId = result.Value.ContactId }, result.Value);
    }

    [HttpPut("{contactId}")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<ContactDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public async Task<IActionResult> Update(Guid seniorId, Guid contactId, SaveContactRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch, [FromServices] UpdateContactHandler handler,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var precondition = CheckVersion(ifMatch, out var version);
        if (precondition is not null) return precondition;
        var result = await handler.Handle(new UpdateContactCommand(AccountId(), seniorId, contactId, request, version), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        Response.Headers.ETag = EntityTag.FromVersion(result.Value!.Version);
        return Ok(result.Value);
    }

    [HttpDelete("{contactId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid seniorId, Guid contactId, [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] DeleteContactHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var precondition = CheckVersion(ifMatch, out var version);
        if (precondition is not null) return precondition;
        var result = await handler.Handle(new DeleteContactCommand(AccountId(), seniorId, contactId, version), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : NoContent();
    }

    private ObjectResult? CheckVersion(string? ifMatch, out int version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(ifMatch))
            return ProblemResponse(428, "preconditionRequired", "Provide the resource ETag in If-Match.");
        return EntityTag.TryParseVersion(ifMatch, out version) ? null
            : ProblemResponse(400, "validationFailed", "If-Match must contain a quoted positive resource version.");
    }

    private ObjectResult Failure(SeniorProfileFailure failure) => failure switch
    {
        SeniorProfileFailure.ResourceNotFound => ProblemResponse(404, "resourceNotFound", "The resource was not found."),
        SeniorProfileFailure.VersionMismatch => ProblemResponse(412, "versionMismatch", "The resource has changed. Reload it before retrying."),
        SeniorProfileFailure.SeniorEditingLocked => ProblemResponse(403, "seniorEditingLocked", "A caretaker has locked contact editing for this senior."),
        _ => throw new ArgumentOutOfRangeException(nameof(failure))
    };

    private ObjectResult ProblemResponse(int status, string code, string detail) =>
        Problem(statusCode: status, detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });

    private Guid AccountId() => Guid.Parse(User.FindFirst("sub")!.Value);
}
