using Microsoft.AspNetCore.Mvc;
using Zpi.Shared.Versioning;
using Zpi.Modules.Safety.Application;

namespace Zpi.Modules.Safety.Api;

public abstract class SafetyController : ControllerBase
{
    protected ObjectResult? CheckVersion(string? ifMatch, out int version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(ifMatch)) return Problem(statusCode: 428, detail: "Provide the ETag in If-Match.",
            extensions: new Dictionary<string, object?> { ["code"] = "preconditionRequired" });
        return EntityTag.TryParseVersion(ifMatch, out version) ? null : Problem(statusCode: 400, detail: "If-Match must contain a quoted positive version.",
            extensions: new Dictionary<string, object?> { ["code"] = "validationFailed" });
    }

    protected Guid AccountId() => Guid.Parse(User.FindFirst("sub")!.Value);

    protected ObjectResult Failure(SafetyFailure failure)
    {
        var (status, code, detail) = failure switch
        {
            SafetyFailure.ResourceNotFound => (404, "resourceNotFound", "The resource was not found."),
            SafetyFailure.OperationForbidden => (403, "operationForbidden", "Your role cannot perform this operation."),
            SafetyFailure.ActiveDeviceExists => (409, "activeDeviceExists", "Another device is monitoring. Register without monitoring and switch explicitly."),
            SafetyFailure.DeviceLimitReached => (409, "deviceLimitReached", "Retire a device before registering another one. The limit is 10 devices."),
            SafetyFailure.VersionMismatch => (412, "versionMismatch", "The resource has changed. Reload it before retrying."),
            SafetyFailure.ZoneLimitReached => (409, "zoneLimitReached", "A senior can have at most 20 zones."),
            SafetyFailure.MonitoringDisabled => (409, "monitoringDisabled", "Enable monitoring on this device before uploading telemetry."),
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };
        return Problem(statusCode: status, detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });
    }
}
