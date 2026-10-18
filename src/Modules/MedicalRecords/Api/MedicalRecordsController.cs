using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.MedicalRecords.Api.Forms;
using Zpi.Modules.MedicalRecords.Application;
using Zpi.Modules.MedicalRecords.Application.Files;
using Zpi.Modules.MedicalRecords.Application.Files.Add;
using Zpi.Modules.MedicalRecords.Application.Files.Content;
using Zpi.Modules.MedicalRecords.Application.Files.Delete;
using Zpi.Modules.MedicalRecords.Application.Records;
using Zpi.Modules.MedicalRecords.Application.Records.Create;
using Zpi.Modules.MedicalRecords.Application.Records.Delete;
using Zpi.Modules.MedicalRecords.Application.Records.Get;
using Zpi.Modules.MedicalRecords.Application.Records.List;
using Zpi.Modules.MedicalRecords.Application.Records.Update;
using Zpi.Shared.Pagination;
using Zpi.Shared.Versioning;

namespace Zpi.Modules.MedicalRecords.Api;

[ApiController]
[Authorize]
[Route("api/v1/seniors/{seniorId}/medical-records")]
[Tags("MedicalRecords")]
[EnableRateLimiting("medical")]
[ServiceFilter<MedicalStorageExceptionFilter>]
[ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(503, "application/problem+json")]
public sealed class MedicalRecordsController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PageDto<MedicalRecordDto>>(200)]
    public async Task<IActionResult> List(Guid seniorId, [FromQuery] ListMedicalRecordsRequest request,
        [FromServices] ListMedicalRecordsHandler handler, CancellationToken cancellationToken)
    {
        NoStore();
        var result = await handler.Handle(new(AccountId(), seniorId, request), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : Ok(result.Value);
    }

    [HttpGet("{recordId}")]
    [ProducesResponseType<MedicalRecordDto>(200)]
    public async Task<IActionResult> Get(Guid seniorId, Guid recordId, [FromServices] GetMedicalRecordHandler handler, CancellationToken cancellationToken)
    {
        NoStore();
        var result = await handler.Handle(new(AccountId(), seniorId, recordId), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        Response.Headers.ETag = EntityTag.FromVersion(result.Value!.Version);
        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Roles = AccountRoles.Caretaker)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(220_200_960)]
    [RequestFormLimits(MultipartBodyLengthLimit = 220_200_960)]
    [ProducesResponseType<MedicalRecordDto>(201)]
    [ProducesResponseType<ProblemDetails>(413, "application/problem+json")]
    public async Task<IActionResult> Create(Guid seniorId, [FromForm] CreateMedicalRecordForm form,
        [FromServices] CreateMedicalRecordHandler handler, CancellationToken cancellationToken)
    {
        NoStore();
        var result = await handler.Handle(new(AccountId(), seniorId, form.Metadata!, form.Files.Select(Upload).ToArray()), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        Response.Headers.ETag = EntityTag.FromVersion(result.Value!.Version);
        return CreatedAtAction(nameof(Get), new { seniorId, recordId = result.Value.RecordId }, result.Value);
    }

    [HttpPut("{recordId}")]
    [Authorize(Roles = AccountRoles.Caretaker)]
    [RequestSizeLimit(MedicalRequestLimits.MetadataJsonBytes)]
    [ProducesResponseType<MedicalRecordDto>(200)]
    [ProducesResponseType<ProblemDetails>(413, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(412, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(428, "application/problem+json")]
    public async Task<IActionResult> Update(Guid seniorId, Guid recordId, SaveMedicalRecordMetadataRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch, [FromServices] UpdateMedicalRecordHandler handler, CancellationToken cancellationToken)
    {
        NoStore();
        var error = CheckVersion(ifMatch, out var version);
        if (error is not null) return error;
        var result = await handler.Handle(new(AccountId(), seniorId, recordId, version, request), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        Response.Headers.ETag = EntityTag.FromVersion(result.Value!.Version);
        return Ok(result.Value);
    }

    [HttpDelete("{recordId}")]
    [Authorize(Roles = AccountRoles.Caretaker)]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(412, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(428, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid seniorId, Guid recordId, [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] DeleteMedicalRecordHandler handler, CancellationToken cancellationToken)
    {
        NoStore();
        var error = CheckVersion(ifMatch, out var version);
        if (error is not null) return error;
        var result = await handler.Handle(new(AccountId(), seniorId, recordId, version), cancellationToken);
        return result.Failure is { } failure ? Failure(failure) : NoContent();
    }

    [HttpPost("{recordId}/files")]
    [Authorize(Roles = AccountRoles.Caretaker)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(22_020_096)]
    [RequestFormLimits(MultipartBodyLengthLimit = 22_020_096)]
    [ProducesResponseType<MedicalFileDto>(201)]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(412, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(428, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(413, "application/problem+json")]
    public async Task<IActionResult> AddFile(Guid seniorId, Guid recordId, [FromForm] AddMedicalFileForm form,
        [FromHeader(Name = "If-Match")] string? ifMatch, [FromServices] AddMedicalFileHandler handler, CancellationToken cancellationToken)
    {
        NoStore();
        var error = CheckVersion(ifMatch, out var version);
        if (error is not null) return error;
        var result = await handler.Handle(new(AccountId(), seniorId, recordId, version, Upload(form.File!)), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        Response.Headers.ETag = EntityTag.FromVersion(result.Value!.Version);
        return CreatedAtAction(nameof(Content), new { seniorId, recordId, fileId = result.Value.File.FileId }, result.Value.File);
    }

    [HttpDelete("{recordId}/files/{fileId}")]
    [Authorize(Roles = AccountRoles.Caretaker)]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(412, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(428, "application/problem+json")]
    public async Task<IActionResult> DeleteFile(Guid seniorId, Guid recordId, Guid fileId, [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] DeleteMedicalFileHandler handler, CancellationToken cancellationToken)
    {
        NoStore();
        var error = CheckVersion(ifMatch, out var version);
        if (error is not null) return error;
        var result = await handler.Handle(new(AccountId(), seniorId, recordId, fileId, version), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        Response.Headers.ETag = EntityTag.FromVersion(result.Value);
        return NoContent();
    }

    [HttpGet("{recordId}/files/{fileId}/content")]
    [ProducesResponseType(typeof(FileStreamResult), 200, MedicalMimeTypes.Pdf, MedicalMimeTypes.Jpeg, MedicalMimeTypes.Png)]
    [ProducesResponseType(typeof(FileStreamResult), 206, MedicalMimeTypes.Pdf, MedicalMimeTypes.Jpeg, MedicalMimeTypes.Png)]
    [ProducesResponseType(416)]
    public async Task<IActionResult> Content(Guid seniorId, Guid recordId, Guid fileId, [FromQuery] MedicalContentRequest request,
        [FromServices] GetMedicalFileHandler handler, CancellationToken cancellationToken)
    {
        NoStore();
        var result = await handler.Handle(new(AccountId(), seniorId, recordId, fileId), cancellationToken);
        if (result.Failure is { } failure) return Failure(failure);
        var value = result.Value!;
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue(request.Disposition) { FileNameStar = value.OriginalName }.ToString();
        return File(value.Content, value.MimeType, enableRangeProcessing: true);
    }

    private Guid AccountId() => Guid.Parse(User.FindFirst("sub")!.Value);
    private void NoStore() => Response.Headers.CacheControl = "no-store";
    private static MedicalUploadFile Upload(IFormFile file) => new(file.FileName, file.ContentType, file.Length, file.OpenReadStream);
    private ObjectResult Failure(MedicalFailure failure) => failure switch
    {
        MedicalFailure.ResourceNotFound => Error(404, "resourceNotFound", "The resource was not found."),
        MedicalFailure.VersionMismatch => Error(412, "versionMismatch", "The record has changed. Reload it before retrying."),
        MedicalFailure.FileLimitReached => Error(409, "fileLimitReached", "The record has reached its file limit."),
        _ => throw new ArgumentOutOfRangeException(nameof(failure))
    };
    private ObjectResult? CheckVersion(string? value, out int version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(value)) return Error(428, "preconditionRequired", "Provide the record ETag in If-Match.");
        return EntityTag.TryParseVersion(value, out version) ? null : Error(400, "validationFailed", "If-Match must contain a quoted positive version.");
    }
    private ObjectResult Error(int status, string code, string detail) => Problem(statusCode: status, detail: detail,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
