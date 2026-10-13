using Microsoft.AspNetCore.Mvc;
using Zpi.Modules.Safety.Application;

namespace Zpi.Modules.Safety.Api;

public abstract class SafetyController : ControllerBase
{
    protected Guid AccountId() => Guid.Parse(User.FindFirst("sub")!.Value);

    protected ObjectResult Failure(SafetyFailure failure)
    {
        var (status, code, detail) = failure switch
        {
            SafetyFailure.ResourceNotFound => (404, "resourceNotFound", "The resource was not found."),
            SafetyFailure.OperationForbidden => (403, "operationForbidden", "This operation is only available to seniors."),
            SafetyFailure.ActiveDeviceExists => (409, "activeDeviceExists", "Another device is monitoring. Register without monitoring and switch explicitly."),
            SafetyFailure.DeviceLimitReached => (409, "deviceLimitReached", "Retire a device before registering another one. The limit is 10 devices."),
            SafetyFailure.MonitoringDisabled => (409, "monitoringDisabled", "Enable monitoring on this device before uploading telemetry."),
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };
        return Problem(statusCode: status, detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });
    }
}
