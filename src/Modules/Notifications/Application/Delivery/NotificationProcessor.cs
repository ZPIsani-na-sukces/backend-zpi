using Zpi.Modules.Notifications.Application.Outbox;
using Zpi.Modules.Notifications.Contracts;

namespace Zpi.Modules.Notifications.Application.Delivery;

public sealed class NotificationProcessor(IEnumerable<INotificationEventSource> eventSources,
    NotificationOutboxProcessor outbox, NotificationDeliveryProcessor deliveries)
{
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var sources = eventSources.ToDictionary(source => source.Name);
        await outbox.ProcessAsync(sources.Values, cancellationToken);
        await deliveries.ProcessAsync(sources, cancellationToken);
    }
}
