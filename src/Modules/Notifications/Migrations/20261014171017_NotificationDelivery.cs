using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Notifications.Migrations
{
    public partial class NotificationDelivery : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "expires_at",
                schema: "notifications",
                table: "notification",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                schema: "notifications",
                table: "notification",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "legacy");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "expires_at",
                schema: "notifications",
                table: "notification");

            migrationBuilder.DropColumn(
                name: "source",
                schema: "notifications",
                table: "notification");
        }
    }
}
