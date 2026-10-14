using Zpi.Modules.Routines.Domain;

namespace Zpi.Modules.Routines.Application.Tasks;

public sealed record SaveRoutineTaskRequest(RoutineType Type, string Title, string Instructions, DateOnly StartsOn,
    DateOnly? EndsOn, bool IsActive, RoutineScheduleInputDto Schedule)
{
    public void ApplyTo(RoutineTask task)
    {
        task.Type = Type;
        task.Title = Title.Trim();
        task.Instructions = Instructions.Trim();
        task.StartsOn = StartsOn;
        task.EndsOn = EndsOn;
        task.IsActive = IsActive;
    }

    public bool ChangesDefinition(RoutineTask task, IReadOnlyList<RoutineTaskSchedule> rows) =>
        task.Type != Type || task.Title != Title.Trim() || task.Instructions != Instructions.Trim() ||
        task.StartsOn != StartsOn || task.EndsOn != EndsOn || task.IsActive != IsActive ||
        rows.Count != Schedule.Times.Count || rows.Any(row => row.TimeZone != Schedule.TimeZone ||
            !row.DaysOfWeek.Order().SequenceEqual(Schedule.DaysOfWeek.Order())) ||
        !rows.Select(row => row.TimeOfDay).Order().SequenceEqual(Schedule.Times.Order());
}
