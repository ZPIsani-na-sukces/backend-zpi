namespace Zpi.Modules.Notifications.Contracts;

public interface INotificationEventSource
{
    public const string ProcessingFailureCode = "notificationProcessingFailed";

    string Name { get; }
    Task PrepareAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PendingNotificationEvent>> ReadPendingAsync(int limit, CancellationToken cancellationToken);
    Task<bool> IsRelevantAsync(NotificationEvent notification, CancellationToken cancellationToken);
    Task CompleteAsync(Guid eventId, CancellationToken cancellationToken);
    Task RetryAsync(Guid eventId, CancellationToken cancellationToken);
}
public sealed record PendingNotificationEvent(Guid Id, int ContractVersion, string Payload);
