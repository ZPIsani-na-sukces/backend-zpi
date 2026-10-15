using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Notifications.Domain;
using Zpi.Database;

namespace Zpi.Modules.Notifications.Infrastructure.Persistence.Configurations;

internal sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("notification_delivery", table =>
        {
            table.HasCheckConstraint("ck_notification_delivery_attempts", "attempt_count >= 0");
            table.HasCheckConstraint("ck_notification_delivery_delivered", "status <> 'delivered' OR delivered_at IS NOT NULL");
            table.HasCheckConstraint("ck_notification_delivery_version", "version >= 1");
            table.HasCheckConstraint("ck_notification_delivery_channel", "channel IN ('sms','inApp','push')");
            table.HasCheckConstraint("ck_notification_delivery_status", "status IN ('pending','sent','delivered','failed','cancelled')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.NotificationId).HasColumnName("notification_id");
        builder.Property(x => x.Channel).HasColumnName("channel").HasConversion(new CamelCaseEnumConverter<DeliveryChannel>()).HasMaxLength(40);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion(new CamelCaseEnumConverter<DeliveryStatus>()).HasMaxLength(40);
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(x => x.ProviderMessageId).HasColumnName("provider_message_id");
        builder.Property(x => x.LastErrorCode).HasColumnName("last_error_code");
        builder.Property(x => x.SentAt).HasColumnName("sent_at");
        builder.Property(x => x.DeliveredAt).HasColumnName("delivered_at");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => new { x.NotificationId, x.Channel }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
        builder.Property(x => x.ProviderMessageId).HasMaxLength(200);
        builder.Property(x => x.LastErrorCode).HasMaxLength(100);
        builder.HasOne<Notification>().WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Restrict);
    }
}
