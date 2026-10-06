using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Notifications.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.CreateTable(
                name: "notification",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    integration_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    senior_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    target_kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification", x => x.id);
                    table.CheckConstraint("ck_notification_target_kind", "target_kind IN ('incident','taskOccurrence','careInvitation')");
                    table.CheckConstraint("ck_notification_target_type", "(type IN ('sos','geofenceExit','lowBattery') AND target_kind = 'incident') OR (type = 'routineReminder' AND target_kind = 'taskOccurrence') OR (type = 'careInvitation' AND target_kind = 'careInvitation')");
                    table.CheckConstraint("ck_notification_type", "type IN ('sos','geofenceExit','lowBattery','routineReminder','careInvitation')");
                    table.CheckConstraint("ck_notification_version", "version >= 1");
                });

            migrationBuilder.CreateTable(
                name: "outbox_message",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    contract_version = table.Column<int>(type: "integer", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_message", x => x.id);
                    table.CheckConstraint("ck_outbox_attempts", "attempts >= 0");
                    table.CheckConstraint("ck_outbox_contract_version", "contract_version >= 1");
                });

            migrationBuilder.CreateTable(
                name: "push_device_token",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    token = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_push_device_token", x => x.id);
                    table.CheckConstraint("ck_push_device_token_platform", "platform IN ('android','ios')");
                });

            migrationBuilder.CreateTable(
                name: "notification_delivery",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_delivery", x => x.id);
                    table.CheckConstraint("ck_notification_delivery_attempts", "attempt_count >= 0");
                    table.CheckConstraint("ck_notification_delivery_channel", "channel IN ('sms','inApp','push')");
                    table.CheckConstraint("ck_notification_delivery_delivered", "status <> 'delivered' OR delivered_at IS NOT NULL");
                    table.CheckConstraint("ck_notification_delivery_status", "status IN ('pending','sent','delivered','failed','cancelled')");
                    table.CheckConstraint("ck_notification_delivery_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_notification_delivery_notification_notification_id",
                        column: x => x.notification_id,
                        principalSchema: "notifications",
                        principalTable: "notification",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_integration_event_id_recipient_account_id",
                schema: "notifications",
                table: "notification",
                columns: new[] { "integration_event_id", "recipient_account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_recipient_account_id_created_at",
                schema: "notifications",
                table: "notification",
                columns: new[] { "recipient_account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_delivery_notification_id_channel",
                schema: "notifications",
                table: "notification_delivery",
                columns: new[] { "notification_id", "channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_delivery_status_next_attempt_at",
                schema: "notifications",
                table: "notification_delivery",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_message_next_attempt_at_occurred_at",
                schema: "notifications",
                table: "outbox_message",
                columns: new[] { "next_attempt_at", "occurred_at" },
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_push_device_token_account_id",
                schema: "notifications",
                table: "push_device_token",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_push_device_token_session_id_installation_id",
                schema: "notifications",
                table: "push_device_token",
                columns: new[] { "session_id", "installation_id" },
                unique: true,
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_push_device_token_token_hash",
                schema: "notifications",
                table: "push_device_token",
                column: "token_hash",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_delivery",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "outbox_message",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "push_device_token",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification",
                schema: "notifications");
        }
    }
}
