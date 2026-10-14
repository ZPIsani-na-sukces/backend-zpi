namespace Zpi.Modules.Routines.Application.Tasks.Create;

public sealed record CreateRoutineTaskCommand(Guid AccountId, Guid SeniorId, SaveRoutineTaskRequest Task);
