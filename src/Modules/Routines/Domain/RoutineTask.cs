using Zpi.Domain;

namespace Zpi.Modules.Routines.Domain;

public sealed class RoutineTask : VersionedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SeniorId { get; set; }
    public RoutineType Type { get; set; }
    public required string Title { get; set; }
    public required string Instructions { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public bool IsActive { get; set; } = true;
    public int ScheduleRevision { get; set; } = 1;
    public Guid CreatedByAccountId { get; set; }
    public Guid? UpdatedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedByAccountId { get; set; }
}
