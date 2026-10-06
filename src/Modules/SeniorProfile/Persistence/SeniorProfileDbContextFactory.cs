using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Zpi.Database;

namespace Zpi.Modules.SeniorProfile.Persistence;

public sealed class SeniorProfileDbContextFactory : IDesignTimeDbContextFactory<SeniorProfileDbContext>
{
    public SeniorProfileDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? throw new InvalidOperationException("Set ConnectionStrings__Database before running dotnet ef.");
        var options = new DbContextOptionsBuilder<SeniorProfileDbContext>();
        DatabaseConfiguration.Configure(options, connectionString, SeniorProfileDbContext.Schema);
        return new SeniorProfileDbContext(options.Options);
    }
}
