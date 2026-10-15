using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Routines.Domain;
using Zpi.Database;

namespace Zpi.Modules.Routines.Infrastructure.Persistence.Configurations;

internal sealed class TaskOccurrenceConfiguration : IEntityTypeConfiguration<TaskOccurrence>
{
    public void Configure(EntityTypeBuilder<TaskOccurrence> builder)
    {
        builder.ToTable("task_occurrence", table =>
        {
            table.HasCheckConstraint("ck_task_occurrence_revision", "schedule_revision >= 1");
            table.HasCheckConstraint("ck_task_occurrence_version", "version >= 1");
            table.HasCheckConstraint("ck_task_occurrence_type", "type IN ('medication','meal','hydration')");
            table.HasCheckConstraint("ck_task_occurrence_status", "status IN ('pending','confirmed','skipped','missed','cancelled')");
            table.HasCheckConstraint("ck_taskoccurrence_title_nonempty", "length(title) >= 1");
            table.HasCheckConstraint("ck_taskoccurrence_instructions_nonempty", "length(instructions) >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.SeniorId).HasColumnName("senior_id");
        builder.Property(x => x.RoutineTaskId).HasColumnName("routine_task_id");
        builder.Property(x => x.ScheduleRevision).HasColumnName("schedule_revision");
        builder.Property(x => x.LocalDate).HasColumnName("local_date");
        builder.Property(x => x.LocalTime).HasColumnName("local_time");
        builder.Property(x => x.ScheduledAt).HasColumnName("scheduled_at");
        builder.Property(x => x.TimeZone).HasColumnName("time_zone").IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").HasConversion(new CamelCaseEnumConverter<RoutineType>()).HasMaxLength(40);
        builder.Property(x => x.Title).HasColumnName("title").IsRequired();
        builder.Property(x => x.Instructions).HasColumnName("instructions").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion(new CamelCaseEnumConverter<OccurrenceStatus>()).HasMaxLength(40);
        builder.Property(x => x.BeforeReminderQueuedAt).HasColumnName("before_reminder_queued_at");
        builder.Property(x => x.DueReminderQueuedAt).HasColumnName("due_reminder_queued_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => new { x.RoutineTaskId, x.ScheduleRevision, x.LocalDate, x.LocalTime }).IsUnique();
        builder.HasIndex(x => new { x.RoutineTaskId, x.ScheduleRevision, x.ScheduledAt }).IsUnique();
        builder.HasIndex(x => new { x.SeniorId, x.ScheduledAt });
        builder.HasIndex(x => x.ScheduledAt).HasFilter("status = 'pending'");
        builder.Property(x => x.TimeZone).HasMaxLength(100);
        builder.Property(x => x.Title).HasMaxLength(150);
        builder.Property(x => x.Instructions).HasMaxLength(2000);
        builder.HasOne<RoutineTask>().WithMany().HasForeignKey(x => x.RoutineTaskId).OnDelete(DeleteBehavior.Restrict);
    }
}
