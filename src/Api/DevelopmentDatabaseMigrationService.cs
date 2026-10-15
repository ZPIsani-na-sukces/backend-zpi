using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Infrastructure.Persistence;
using Zpi.Modules.Identity.Infrastructure.Persistence;
using Zpi.Modules.MedicalRecords.Persistence;
using Zpi.Modules.Notifications.Infrastructure.Persistence;
using Zpi.Modules.Routines.Infrastructure.Persistence;
using Zpi.Modules.Safety.Infrastructure.Persistence;
using Zpi.Modules.SeniorProfile.Infrastructure.Persistence;

namespace Zpi.Api;

internal sealed class DevelopmentDatabaseMigrationService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        DbContext[] contexts =
        [
            services.GetRequiredService<IdentityDbContext>(),
            services.GetRequiredService<CareDbContext>(),
            services.GetRequiredService<SeniorProfileDbContext>(),
            services.GetRequiredService<SafetyDbContext>(),
            services.GetRequiredService<RoutinesDbContext>(),
            services.GetRequiredService<MedicalRecordsDbContext>(),
            services.GetRequiredService<NotificationsDbContext>()
        ];

        foreach (var context in contexts)
            await context.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
