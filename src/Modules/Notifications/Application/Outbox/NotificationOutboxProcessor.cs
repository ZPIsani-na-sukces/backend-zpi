using System.Text.Json;
using Microsoft.Extensions.Logging;
using Zpi.Modules.Notifications.Contracts;
using Zpi.Modules.Notifications.Infrastructure.Persistence;

namespace Zpi.Modules.Notifications.Application.Outbox;

public sealed class NotificationOutboxProcessor(NotificationsDbContext context, INotificationsFacade notifications,
    ILogger<NotificationOutboxProcessor> logger)
{
    public async Task ProcessAsync(IEnumerable<INotificationEventSource> sources, CancellationToken cancellationToken)
    {
        foreach (var source in sources)
        {
            try
            {  
                await source.PrepareAsync(cancellationToken);
                var pending = await source.ReadPendingAsync(100, cancellationToken);
                foreach (var row in pending)
                {
                    context.ChangeTracker.Clear();
                    try
                    {
                        if (row.ContractVersion != 1) throw new InvalidOperationException("Unsupported notification event version.");
                        var message = JsonSerializer.Deserialize<NotificationEvent>(row.Payload)
                            ?? throw new JsonException("Missing notification event.");
                        if (message.EventId != row.Id) throw new JsonException("Invalid notification event id.");
                        if (await source.IsRelevantAsync(message, cancellationToken))
                            await notifications.EnqueueAsync(source.Name, message, cancellationToken);
                        await source.CompleteAsync(row.Id, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception exception)
                    {
                        context.ChangeTracker.Clear();
                        logger.LogError(exception, "Notification event {EventId} from {Source} failed.", row.Id, source.Name);
                        await source.RetryAsync(row.Id, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) { logger.LogError(exception, "Notification source {Source} failed.", source.Name); }
        }
    }
}
