using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Zpi.Modules.Identity.Application.Accounts.DeleteAccount;
using Zpi.Modules.Identity.Application.Accounts.UnverifiedAccountCleanup;

namespace Zpi.Modules.Identity.Infrastructure.BackgroundJobs;

internal sealed class AccountDeletionWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<AccountDeletionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<UnverifiedAccountCleanupProcessor>().ProcessAsync(stoppingToken);
                await scope.ServiceProvider.GetRequiredService<AccountDeletionProcessor>().ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Account deletion job failed. The next run will retry.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
