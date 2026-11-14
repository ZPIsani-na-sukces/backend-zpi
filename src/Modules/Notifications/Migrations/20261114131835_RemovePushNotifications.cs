using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Notifications.Migrations
{
    public partial class RemovePushNotifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "push_device_token",
                schema: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_delivery_channel",
                schema: "notifications",
                table: "notification_delivery");

            migrationBuilder.Sql("DELETE FROM notifications.notification_delivery WHERE channel = 'push'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_delivery_channel",
                schema: "notifications",
                table: "notification_delivery",
                sql: "channel IN ('sms','inApp')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_delivery_channel",
                schema: "notifications",
                table: "notification_delivery");

            migrationBuilder.CreateTable(
                name: "push_device_token",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_push_device_token", x => x.id);
                    table.CheckConstraint("ck_push_device_token_platform", "platform IN ('android','ios')");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_delivery_channel",
                schema: "notifications",
                table: "notification_delivery",
                sql: "channel IN ('sms','inApp','push')");

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
    }
}
