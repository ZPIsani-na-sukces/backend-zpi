using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Notifications.Domain;
using Zpi.Database;

namespace Zpi.Modules.Notifications.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notification", table =>
        {
            table.HasCheckConstraint("ck_notification_target_type", "(type IN ('sos','geofenceExit','lowBattery') AND target_kind = 'incident') OR (type = 'routineReminder' AND target_kind = 'taskOccurrence') OR (type = 'careInvitation' AND target_kind = 'careInvitation')");
            table.HasCheckConstraint("ck_notification_version", "version >= 1");
            table.HasCheckConstraint("ck_notification_type", "type IN ('sos','geofenceExit','lowBattery','routineReminder','careInvitation')");
            table.HasCheckConstraint("ck_notification_target_kind", "target_kind IN ('incident','taskOccurrence','careInvitation')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.IntegrationEventId).HasColumnName("integration_event_id");
        builder.Property(x => x.RecipientAccountId).HasColumnName("recipient_account_id");
        builder.Property(x => x.SeniorId).HasColumnName("senior_id");
        builder.Property(x => x.Type).HasColumnName("type").HasConversion(new CamelCaseEnumConverter<NotificationType>()).HasMaxLength(40);
        builder.Property(x => x.Title).HasColumnName("title").IsRequired();
        builder.Property(x => x.Message).HasColumnName("message").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.ReadAt).HasColumnName("read_at");
        builder.Property(x => x.TargetKind).HasColumnName("target_kind").HasConversion(new CamelCaseEnumConverter<NotificationTargetKind>()).HasMaxLength(40);
        builder.Property(x => x.TargetId).HasColumnName("target_id");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => new { x.IntegrationEventId, x.RecipientAccountId }).IsUnique();
        builder.HasIndex(x => new { x.RecipientAccountId, x.CreatedAt });
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.Message).HasMaxLength(2000);
    }
}
