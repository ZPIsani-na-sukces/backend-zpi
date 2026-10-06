using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Zpi.Modules.Notifications.Persistence;

#nullable disable

namespace Zpi.Modules.Notifications.Migrations
{
    [DbContext(typeof(NotificationsDbContext))]
    [Migration("20261006133309_InitialCreate")]
    partial class InitialCreate
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("notifications")
                .HasAnnotation("ProductVersion", "10.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Zpi.Database.Outbox.OutboxMessage", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<int>("Attempts")
                        .HasColumnType("integer")
                        .HasColumnName("attempts");

                    b.Property<int>("ContractVersion")
                        .HasColumnType("integer")
                        .HasColumnName("contract_version");

                    b.Property<string>("LastError")
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)")
                        .HasColumnName("last_error");

                    b.Property<DateTimeOffset?>("NextAttemptAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("next_attempt_at");

                    b.Property<DateTimeOffset>("OccurredAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("occurred_at");

                    b.Property<string>("Payload")
                        .IsRequired()
                        .HasColumnType("jsonb")
                        .HasColumnName("payload");

                    b.Property<DateTimeOffset?>("ProcessedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("processed_at");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)")
                        .HasColumnName("type");

                    b.HasKey("Id");

                    b.HasIndex("NextAttemptAt", "OccurredAt")
                        .HasFilter("processed_at IS NULL");

                    b.ToTable("outbox_message", "notifications", t =>
                        {
                            t.HasCheckConstraint("ck_outbox_attempts", "attempts >= 0");

                            t.HasCheckConstraint("ck_outbox_contract_version", "contract_version >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Notifications.Domain.Notification", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<Guid>("IntegrationEventId")
                        .HasColumnType("uuid")
                        .HasColumnName("integration_event_id");

                    b.Property<string>("Message")
                        .IsRequired()
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)")
                        .HasColumnName("message");

                    b.Property<DateTimeOffset?>("ReadAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("read_at");

                    b.Property<Guid>("RecipientAccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("recipient_account_id");

                    b.Property<Guid?>("SeniorId")
                        .HasColumnType("uuid")
                        .HasColumnName("senior_id");

                    b.Property<Guid>("TargetId")
                        .HasColumnType("uuid")
                        .HasColumnName("target_id");

                    b.Property<string>("TargetKind")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("target_kind");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("title");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("type");

                    b.Property<int>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("integer")
                        .HasColumnName("version");

                    b.HasKey("Id");

                    b.HasIndex("IntegrationEventId", "RecipientAccountId")
                        .IsUnique();

                    b.HasIndex("RecipientAccountId", "CreatedAt");

                    b.ToTable("notification", "notifications", t =>
                        {
                            t.HasCheckConstraint("ck_notification_target_kind", "target_kind IN ('incident','taskOccurrence','careInvitation')");

                            t.HasCheckConstraint("ck_notification_target_type", "(type IN ('sos','geofenceExit','lowBattery') AND target_kind = 'incident') OR (type = 'routineReminder' AND target_kind = 'taskOccurrence') OR (type = 'careInvitation' AND target_kind = 'careInvitation')");

                            t.HasCheckConstraint("ck_notification_type", "type IN ('sos','geofenceExit','lowBattery','routineReminder','careInvitation')");

                            t.HasCheckConstraint("ck_notification_version", "version >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Notifications.Domain.NotificationDelivery", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<int>("AttemptCount")
                        .HasColumnType("integer")
                        .HasColumnName("attempt_count");

                    b.Property<string>("Channel")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("channel");

                    b.Property<DateTimeOffset?>("DeliveredAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("delivered_at");

                    b.Property<string>("LastErrorCode")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("last_error_code");

                    b.Property<DateTimeOffset?>("NextAttemptAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("next_attempt_at");

                    b.Property<Guid>("NotificationId")
                        .HasColumnType("uuid")
                        .HasColumnName("notification_id");

                    b.Property<string>("ProviderMessageId")
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("provider_message_id");

                    b.Property<DateTimeOffset?>("SentAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("sent_at");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("status");

                    b.Property<int>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("integer")
                        .HasColumnName("version");

                    b.HasKey("Id");

                    b.HasIndex("NotificationId", "Channel")
                        .IsUnique();

                    b.HasIndex("Status", "NextAttemptAt");

                    b.ToTable("notification_delivery", "notifications", t =>
                        {
                            t.HasCheckConstraint("ck_notification_delivery_attempts", "attempt_count >= 0");

                            t.HasCheckConstraint("ck_notification_delivery_channel", "channel IN ('sms','inApp','push')");

                            t.HasCheckConstraint("ck_notification_delivery_delivered", "status <> 'delivered' OR delivered_at IS NOT NULL");

                            t.HasCheckConstraint("ck_notification_delivery_status", "status IN ('pending','sent','delivered','failed','cancelled')");

                            t.HasCheckConstraint("ck_notification_delivery_version", "version >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Notifications.Domain.PushDeviceToken", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("AccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("account_id");

                    b.Property<Guid>("InstallationId")
                        .HasColumnType("uuid")
                        .HasColumnName("installation_id");

                    b.Property<string>("Platform")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("platform");

                    b.Property<DateTimeOffset>("RegisteredAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("registered_at");

                    b.Property<DateTimeOffset?>("RevokedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("revoked_at");

                    b.Property<Guid>("SessionId")
                        .HasColumnType("uuid")
                        .HasColumnName("session_id");

                    b.Property<string>("Token")
                        .IsRequired()
                        .HasMaxLength(4096)
                        .HasColumnType("character varying(4096)")
                        .HasColumnName("token");

                    b.Property<string>("TokenHash")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)")
                        .HasColumnName("token_hash");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.HasKey("Id");

                    b.HasIndex("AccountId");

                    b.HasIndex("TokenHash")
                        .IsUnique();

                    b.HasIndex("SessionId", "InstallationId")
                        .IsUnique()
                        .HasFilter("revoked_at IS NULL");

                    b.ToTable("push_device_token", "notifications", t =>
                        {
                            t.HasCheckConstraint("ck_push_device_token_platform", "platform IN ('android','ios')");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Notifications.Domain.NotificationDelivery", b =>
                {
                    b.HasOne("Zpi.Modules.Notifications.Domain.Notification", null)
                        .WithMany()
                        .HasForeignKey("NotificationId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
