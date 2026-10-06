using Zpi.Domain;

namespace Zpi.Modules.Routines.Domain;

public sealed class TaskOccurrence : VersionedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SeniorId { get; set; }
    public Guid RoutineTaskId { get; set; }
    public int ScheduleRevision { get; set; }
    public DateOnly LocalDate { get; set; }
    public TimeOnly LocalTime { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public required string TimeZone { get; set; }
    public RoutineType Type { get; set; }
    public required string Title { get; set; }
    public required string Instructions { get; set; }
    public OccurrenceStatus Status { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
