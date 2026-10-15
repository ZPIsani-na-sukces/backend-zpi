using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Zpi.Modules.Notifications.Application.Delivery;

namespace Zpi.Modules.Notifications.Infrastructure.BackgroundJobs;

internal sealed class NotificationWorker(IServiceScopeFactory scopes, NotificationProcessingSignal signal, ILogger<NotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<NotificationProcessor>().ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogError(exception, "Notification processing failed. The next run will retry."); }
            await signal.WaitAsync(stoppingToken);
        }
    }
}
