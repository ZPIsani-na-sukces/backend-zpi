using Zpi.Modules.Routines.Domain;

namespace Zpi.Modules.Routines.Application.Occurrences.Decision;

public sealed record RecordTaskDecisionRequest(TaskDecision Decision, DateTimeOffset PerformedAt);
