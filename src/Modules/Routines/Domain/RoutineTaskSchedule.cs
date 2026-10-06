namespace Zpi.Modules.Routines.Domain;

public sealed class RoutineTaskSchedule
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RoutineTaskId { get; set; }
    public int Revision { get; set; }
    public required string TimeZone { get; set; }
    public int[] DaysOfWeek { get; set; } = [];
    public TimeOnly TimeOfDay { get; set; }
}
