namespace Zpi.Modules.Routines.Application.Occurrences.Decision;

public sealed record RecordTaskDecisionCommand(Guid AccountId, Guid SeniorId, Guid OccurrenceId, RecordTaskDecisionRequest Request);
