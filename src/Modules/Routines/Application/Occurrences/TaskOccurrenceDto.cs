namespace Zpi.Modules.Routines.Application.Occurrences;

public sealed record TaskOccurrenceDto(Guid OccurrenceId, Guid SeniorId, Guid TaskId, int ScheduleRevision,
    string Type, string Title, string Instructions, DateTimeOffset ScheduledAt, string TimeZone, string Status,
    IReadOnlyList<DateTimeOffset> ReminderAt, TaskDecisionDto? Decision, DateTimeOffset UpdatedAt);

public sealed record TaskDecisionDto(string Decision, DateTimeOffset PerformedAt, DateTimeOffset ReceivedAt, Guid RecordedByUserId);
