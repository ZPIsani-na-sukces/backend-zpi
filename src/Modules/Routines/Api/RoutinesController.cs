using Microsoft.AspNetCore.Mvc;
using Zpi.Modules.Routines.Application;
using Zpi.Shared.Versioning;

namespace Zpi.Modules.Routines.Api;

public abstract class RoutinesController : ControllerBase
{
    protected Guid AccountId() => Guid.Parse(User.FindFirst("sub")!.Value);
    protected ObjectResult Failure(RoutineFailure failure) => failure switch
    {
        RoutineFailure.ResourceNotFound => Error(404, "resourceNotFound", "The resource was not found."),
        RoutineFailure.VersionMismatch => Error(412, "versionMismatch", "The task has changed. Reload it before retrying."),
        RoutineFailure.TaskLimitReached => Error(409, "taskLimitReached", "A senior can have at most 50 active tasks."),
        RoutineFailure.OccurrenceCancelled => Error(409, "occurrenceCancelled", "This occurrence has been cancelled."),
        RoutineFailure.DecisionAlreadyRecorded => Error(409, "decisionAlreadyRecorded", "A different decision has already been recorded."),
        _ => throw new ArgumentOutOfRangeException(nameof(failure))
    };
    protected ObjectResult? CheckVersion(string? ifMatch, out int version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(ifMatch)) return Error(428, "preconditionRequired", "Provide the task ETag in If-Match.");
        return EntityTag.TryParseVersion(ifMatch, out version) ? null : Error(400, "validationFailed", "If-Match must contain a quoted positive task version.");
    }
    private ObjectResult Error(int status, string code, string detail) =>
        Problem(statusCode: status, detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });
}
