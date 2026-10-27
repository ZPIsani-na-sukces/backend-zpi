using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Zpi.Modules.Care.Application.Invitations.Cleanup;

namespace Zpi.Modules.Care.Infrastructure.BackgroundJobs;

internal sealed class InvitationCleanupWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<InvitationCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30), clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<InvitationCleanupProcessor>().ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Invitation cleanup failed. The next run will retry.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
