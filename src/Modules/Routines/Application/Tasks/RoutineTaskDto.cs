using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Routines.Application.Tasks;

public sealed record RoutineTaskDto(Guid TaskId, Guid SeniorId, string Type, string Title, string Instructions,
    DateOnly StartsOn, DateOnly? EndsOn, bool IsActive, RoutineScheduleInputDto Schedule, int ScheduleRevision,
    IReadOnlyList<int> ReminderOffsetsMinutes, AccountSummary? CreatedBy, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, int Version);
