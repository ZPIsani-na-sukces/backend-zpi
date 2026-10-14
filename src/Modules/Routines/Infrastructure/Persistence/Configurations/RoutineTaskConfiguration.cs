using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Routines.Domain;
using Zpi.Database;

namespace Zpi.Modules.Routines.Infrastructure.Persistence.Configurations;

internal sealed class RoutineTaskConfiguration : IEntityTypeConfiguration<RoutineTask>
{
    public void Configure(EntityTypeBuilder<RoutineTask> builder)
    {
        builder.ToTable("routine_task", table =>
        {
            table.HasCheckConstraint("ck_routine_task_dates", "ends_on IS NULL OR ends_on >= starts_on");
            table.HasCheckConstraint("ck_routine_task_revision", "schedule_revision >= 1");
            table.HasCheckConstraint("ck_routine_task_version", "version >= 1");
            table.HasCheckConstraint("ck_routine_task_type", "type IN ('medication','meal','hydration')");
            table.HasCheckConstraint("ck_routinetask_title_nonempty", "length(title) >= 1");
            table.HasCheckConstraint("ck_routinetask_instructions_nonempty", "length(instructions) >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.SeniorId).HasColumnName("senior_id");
        builder.Property(x => x.Type).HasColumnName("type").HasConversion(new CamelCaseEnumConverter<RoutineType>()).HasMaxLength(40);
        builder.Property(x => x.Title).HasColumnName("title").IsRequired();
        builder.Property(x => x.Instructions).HasColumnName("instructions").IsRequired();
        builder.Property(x => x.StartsOn).HasColumnName("starts_on");
        builder.Property(x => x.EndsOn).HasColumnName("ends_on");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.ScheduleRevision).HasColumnName("schedule_revision");
        builder.Property(x => x.CreatedByAccountId).HasColumnName("created_by_account_id");
        builder.Property(x => x.UpdatedByAccountId).HasColumnName("updated_by_account_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedByAccountId).HasColumnName("deleted_by_account_id");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => x.SeniorId);
        builder.Property(x => x.Title).HasMaxLength(150);
        builder.Property(x => x.Instructions).HasMaxLength(2000);
    }
}
