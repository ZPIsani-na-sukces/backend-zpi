using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Safety.Migrations
{
    public partial class AutomaticSafetyAlerts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "zones_json",
                schema: "safety",
                table: "geofence_incident",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "automatic_alert_state",
                schema: "safety",
                columns: table => new
                {
                    senior_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_inside = table.Column<bool>(type: "boolean", nullable: true),
                    candidate_inside = table.Column<bool>(type: "boolean", nullable: true),
                    candidate_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    inside_zones_json = table.Column<string>(type: "jsonb", nullable: false),
                    geofence_incident_id = table.Column<Guid>(type: "uuid", nullable: true),
                    geofence_notification_active = table.Column<bool>(type: "boolean", nullable: false),
                    battery_evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    battery_armed = table.Column<bool>(type: "boolean", nullable: false),
                    battery_incident_id = table.Column<Guid>(type: "uuid", nullable: true),
                    battery_notification_active = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automatic_alert_state", x => x.senior_id);
                    table.ForeignKey(
                        name: "FK_automatic_alert_state_device_device_id",
                        column: x => x.device_id,
                        principalSchema: "safety",
                        principalTable: "device",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_low_battery_incident_senior_id",
                schema: "safety",
                table: "low_battery_incident",
                column: "senior_id",
                unique: true,
                filter: "status <> 'resolved'");

            migrationBuilder.CreateIndex(
                name: "IX_geofence_incident_senior_id",
                schema: "safety",
                table: "geofence_incident",
                column: "senior_id",
                unique: true,
                filter: "status <> 'resolved'");

            migrationBuilder.CreateIndex(
                name: "IX_automatic_alert_state_device_id",
                schema: "safety",
                table: "automatic_alert_state",
                column: "device_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "automatic_alert_state",
                schema: "safety");

            migrationBuilder.DropIndex(
                name: "IX_low_battery_incident_senior_id",
                schema: "safety",
                table: "low_battery_incident");

            migrationBuilder.DropIndex(
                name: "IX_geofence_incident_senior_id",
                schema: "safety",
                table: "geofence_incident");

            migrationBuilder.DropColumn(
                name: "zones_json",
                schema: "safety",
                table: "geofence_incident");
        }
    }
}
