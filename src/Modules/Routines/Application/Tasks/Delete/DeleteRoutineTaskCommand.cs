namespace Zpi.Modules.Routines.Application.Tasks.Delete;

public sealed record DeleteRoutineTaskCommand(Guid AccountId, Guid SeniorId, Guid TaskId, int ExpectedVersion);
