using Zpi.Modules.Routines.Domain;

namespace Zpi.Modules.Routines.Application.Tasks;

public sealed record RoutineScheduleInputDto(string TimeZone, IReadOnlyList<int> DaysOfWeek, IReadOnlyList<TimeOnly> Times)
{
    public RoutineTaskSchedule[] CreateRows(Guid taskId, int revision) => Times.Order()
        .Select(time => new RoutineTaskSchedule { RoutineTaskId = taskId, Revision = revision,
            TimeZone = TimeZone, DaysOfWeek = DaysOfWeek.Order().ToArray(), TimeOfDay = time }).ToArray();
}
