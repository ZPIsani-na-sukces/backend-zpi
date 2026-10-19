using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Routines.Domain;
using Zpi.Database;

namespace Zpi.Modules.Routines.Infrastructure.Persistence.Configurations;

internal sealed class RoutineTaskScheduleConfiguration : IEntityTypeConfiguration<RoutineTaskSchedule>
{
    public void Configure(EntityTypeBuilder<RoutineTaskSchedule> builder)
    {
        builder.ToTable("routine_task_schedule", table =>
        {
            table.HasCheckConstraint("ck_routine_task_schedule_revision", "revision >= 1");
            table.HasCheckConstraint("ck_routine_task_schedule_days", "cardinality(days_of_week) BETWEEN 1 AND 7 AND days_of_week <@ ARRAY[1,2,3,4,5,6,7]");
            table.HasCheckConstraint("ck_routinetaskschedule_time_zone_nonempty", "length(time_zone) >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.RoutineTaskId).HasColumnName("routine_task_id");
        builder.Property(x => x.Revision).HasColumnName("revision");
        builder.Property(x => x.TimeZone).HasColumnName("time_zone").IsRequired();
        builder.Property(x => x.DaysOfWeek).HasColumnName("days_of_week");
        builder.Property(x => x.TimeOfDay).HasColumnName("time_of_day");
        builder.HasIndex(x => new { x.RoutineTaskId, x.Revision, x.TimeOfDay }).IsUnique();
        builder.Property(x => x.TimeZone).HasMaxLength(100);
        builder.HasOne<RoutineTask>().WithMany().HasForeignKey(x => x.RoutineTaskId).OnDelete(DeleteBehavior.Cascade);
    }
}
