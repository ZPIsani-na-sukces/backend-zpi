using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Safety.Migrations
{
    public partial class GeofenceEntryNotifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "geofence_entered_at",
                schema: "safety",
                table: "automatic_alert_state",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "geofence_entered_at",
                schema: "safety",
                table: "automatic_alert_state");
        }
    }
}
