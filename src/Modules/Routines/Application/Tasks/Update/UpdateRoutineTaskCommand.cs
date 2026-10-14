namespace Zpi.Modules.Routines.Application.Tasks.Update;

public sealed record UpdateRoutineTaskCommand(Guid AccountId, Guid SeniorId, Guid TaskId,
    SaveRoutineTaskRequest Task, int ExpectedVersion);
