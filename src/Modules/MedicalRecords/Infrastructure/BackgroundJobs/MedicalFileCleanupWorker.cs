using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Zpi.Modules.MedicalRecords.Application.Files;

namespace Zpi.Modules.MedicalRecords.Infrastructure.BackgroundJobs;

internal sealed class MedicalFileCleanupWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<MedicalFileCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<MedicalFileCleanupProcessor>().ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogError(exception, "Medical file cleanup failed. The next run will retry."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
