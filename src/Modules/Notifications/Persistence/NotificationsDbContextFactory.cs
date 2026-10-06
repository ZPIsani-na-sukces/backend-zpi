using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Zpi.Database;

namespace Zpi.Modules.Notifications.Persistence;

public sealed class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? throw new InvalidOperationException("Set ConnectionStrings__Database before running dotnet ef.");
        var options = new DbContextOptionsBuilder<NotificationsDbContext>();
        DatabaseConfiguration.Configure(options, connectionString, NotificationsDbContext.Schema);
        return new NotificationsDbContext(options.Options);
    }
}
