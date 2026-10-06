using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Routines.Domain;
using Zpi.Database.Outbox;

namespace Zpi.Modules.Routines.Persistence;

public sealed class RoutinesDbContext(DbContextOptions<RoutinesDbContext> options) : DbContext(options)
{
    public const string Schema = "routines";
    public DbSet<RoutineTask> RoutineTasks => Set<RoutineTask>();
    public DbSet<RoutineTaskSchedule> RoutineTaskSchedules => Set<RoutineTaskSchedule>();
    public DbSet<TaskOccurrence> TaskOccurrences => Set<TaskOccurrence>();
    public DbSet<TaskConfirmation> TaskConfirmations => Set<TaskConfirmation>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RoutinesDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
