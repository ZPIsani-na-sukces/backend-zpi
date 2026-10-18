using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Zpi.Database;

namespace Zpi.Modules.MedicalRecords.Persistence;

public sealed class MedicalRecordsDbContextFactory : IDesignTimeDbContextFactory<MedicalRecordsDbContext>
{
    public MedicalRecordsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? throw new InvalidOperationException("Set ConnectionStrings__Database before running dotnet ef.");
        var options = new DbContextOptionsBuilder<MedicalRecordsDbContext>();
        DatabaseConfiguration.Configure(options, connectionString, MedicalRecordsDbContext.Schema);
        return new MedicalRecordsDbContext(options.Options);
    }
}
