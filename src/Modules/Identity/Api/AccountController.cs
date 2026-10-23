using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zpi.Modules.Identity.Application.Accounts;
using Zpi.Modules.Identity.Application.Accounts.CurrentAccount;
using Zpi.Modules.Identity.Application.Accounts.DeleteAccount;
using Zpi.Modules.Identity.Application.Accounts.VerificationStatus;
using Zpi.Shared.Versioning;

namespace Zpi.Modules.Identity.Api;

[ApiController]
[Route("api/v1/me")]
[Tags("Authentication")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public sealed class AccountController : ControllerBase
{
    [HttpDelete]
    [Authorize]
    [EnableRateLimiting("authentication")]
    [RequestSizeLimit(32_768)]
    [ProducesResponseType<AccountDeletionDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> DeleteAccount(DeleteAccountRequest request, [FromServices] DeleteAccountHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new DeleteAccountCommand(AccountId(), request.Password), cancellationToken);
        if (result.Failure is not null) return this.Failure(result);
        RefreshTokenCookie.Clear(Response);
        return Accepted(result.Value);
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType<AccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Me([FromServices] CurrentAccountHandler handler, CancellationToken cancellationToken)
    {
        var account = await handler.Handle(new CurrentAccountQuery(AccountId()), cancellationToken);
        Response.Headers.ETag = EntityTag.FromVersion(account.Version);
        Response.Headers.CacheControl = "no-store";
        return Ok(account);
    }

    [HttpGet("verification-status")]
    [Authorize]
    [ProducesResponseType<AccountVerificationStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> VerificationStatus([FromServices] AccountVerificationStatusHandler handler,
        CancellationToken cancellationToken)
    {
        var status = await handler.Handle(new AccountVerificationStatusQuery(AccountId()), cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(status);
    }

    private Guid AccountId() => Guid.Parse(User.FindFirst("sub")!.Value);
}
