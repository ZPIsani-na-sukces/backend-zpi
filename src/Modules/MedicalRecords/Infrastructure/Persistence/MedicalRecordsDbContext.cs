using Microsoft.EntityFrameworkCore;
using Zpi.Modules.MedicalRecords.Domain;
using Zpi.Database.Outbox;

namespace Zpi.Modules.MedicalRecords.Persistence;

public sealed class MedicalRecordsDbContext(DbContextOptions<MedicalRecordsDbContext> options) : DbContext(options)
{
    public const string Schema = "medical";
    public DbSet<MedicalAuditEntry> MedicalAuditEntries => Set<MedicalAuditEntry>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<MedicalFile> MedicalFiles => Set<MedicalFile>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MedicalRecordsDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
