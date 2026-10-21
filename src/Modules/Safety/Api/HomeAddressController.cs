using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zpi.Modules.Safety.Application.Home;
using Zpi.Modules.Safety.Application.Home.Detect;
using Zpi.Modules.Safety.Application.Home.Get;
using Zpi.Modules.Safety.Application.Home.Save;
using Zpi.Shared.Versioning;

namespace Zpi.Modules.Safety.Api;

[ApiController]
[Authorize]
[Route("api/v1/seniors/{seniorId}/home-address")]
[Tags("Safety")]
[EnableRateLimiting("safety")]
[ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
public sealed class HomeAddressController : SafetyController
{
    [HttpGet]
    [ProducesResponseType<HomeAddressDto>(200)]
    public async Task<IActionResult> Get(Guid seniorId, [FromServices] GetHomeAddressHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(new(AccountId(), seniorId), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        Response.Headers.ETag = EntityTag.FromVersion(result.Value!.Version);
        return Ok(result.Value);
    }

    [HttpPost("detect")]
    [RequestSizeLimit(16_384)]
    [ProducesResponseType<DetectedAddressDto>(200)]
    [ProducesResponseType<ProblemDetails>(400, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(503, "application/problem+json")]
    public async Task<IActionResult> Detect(Guid seniorId, DetectAddressRequest request,
        [FromServices] DetectAddressHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.Handle(AccountId(), seniorId, request, cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpPut]
    [RequestSizeLimit(16_384)]
    [ProducesResponseType<HomeAddressDto>(200)]
    [ProducesResponseType<HomeAddressDto>(201)]
    [ProducesResponseType<ProblemDetails>(412, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(428, "application/problem+json")]
    public async Task<IActionResult> Save(Guid seniorId, SaveHomeAddressRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch, [FromHeader(Name = "If-None-Match")] string? ifNoneMatch,
        [FromServices] SaveHomeAddressHandler handler, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (ifNoneMatch is not null && (ifNoneMatch != "*" || ifMatch is not null))
            return Problem(statusCode: 400, detail: "Use either If-None-Match: * or If-Match.",
                extensions: new Dictionary<string, object?> { ["code"] = "validationFailed" });
        int? version = null;
        if (ifNoneMatch is null)
        {
            var error = CheckVersion(ifMatch, out var parsed);
            if (error is not null) return error;
            version = parsed;
        }
        var result = await handler.Handle(new(AccountId(), seniorId, request, version), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        Response.Headers.ETag = EntityTag.FromVersion(result.Value!.Version);
        return version is null ? CreatedAtAction(nameof(Get), new { seniorId }, result.Value) : Ok(result.Value);
    }
}
