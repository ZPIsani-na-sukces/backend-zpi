namespace Zpi.Modules.Routines.Domain;

public sealed class TaskConfirmation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TaskOccurrenceId { get; set; }
    public TaskDecision Decision { get; set; }
    public DateTimeOffset PerformedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public Guid RecordedByAccountId { get; set; }
}
