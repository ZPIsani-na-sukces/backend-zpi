using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Routines.Domain;
using Zpi.Database;

namespace Zpi.Modules.Routines.Infrastructure.Persistence.Configurations;

internal sealed class TaskConfirmationConfiguration : IEntityTypeConfiguration<TaskConfirmation>
{
    public void Configure(EntityTypeBuilder<TaskConfirmation> builder)
    {
        builder.ToTable("task_confirmation", table =>
        {
            table.HasCheckConstraint("ck_task_confirmation_decision", "decision IN ('confirmed','skipped')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TaskOccurrenceId).HasColumnName("task_occurrence_id");
        builder.Property(x => x.Decision).HasColumnName("decision").HasConversion(new CamelCaseEnumConverter<TaskDecision>()).HasMaxLength(40);
        builder.Property(x => x.PerformedAt).HasColumnName("performed_at");
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at");
        builder.Property(x => x.RecordedByAccountId).HasColumnName("recorded_by_account_id");
        builder.HasIndex(x => x.TaskOccurrenceId).IsUnique();
        builder.HasOne<TaskOccurrence>().WithMany().HasForeignKey(x => x.TaskOccurrenceId).OnDelete(DeleteBehavior.Cascade);
    }
}
