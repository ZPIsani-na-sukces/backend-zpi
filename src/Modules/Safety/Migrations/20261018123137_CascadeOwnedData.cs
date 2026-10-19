using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Safety.Migrations
{
    public partial class CascadeOwnedData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_automatic_alert_state_device_device_id",
                schema: "safety",
                table: "automatic_alert_state");

            migrationBuilder.DropForeignKey(
                name: "FK_battery_sample_device_device_id",
                schema: "safety",
                table: "battery_sample");

            migrationBuilder.DropForeignKey(
                name: "FK_location_sample_device_device_id",
                schema: "safety",
                table: "location_sample");

            migrationBuilder.DropForeignKey(
                name: "FK_sos_trigger_receipt_device_device_id",
                schema: "safety",
                table: "sos_trigger_receipt");

            migrationBuilder.DropForeignKey(
                name: "FK_sos_trigger_receipt_sos_incident_incident_id",
                schema: "safety",
                table: "sos_trigger_receipt");

            migrationBuilder.AddForeignKey(
                name: "FK_automatic_alert_state_device_device_id",
                schema: "safety",
                table: "automatic_alert_state",
                column: "device_id",
                principalSchema: "safety",
                principalTable: "device",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_battery_sample_device_device_id",
                schema: "safety",
                table: "battery_sample",
                column: "device_id",
                principalSchema: "safety",
                principalTable: "device",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_location_sample_device_device_id",
                schema: "safety",
                table: "location_sample",
                column: "device_id",
                principalSchema: "safety",
                principalTable: "device",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_sos_trigger_receipt_device_device_id",
                schema: "safety",
                table: "sos_trigger_receipt",
                column: "device_id",
                principalSchema: "safety",
                principalTable: "device",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_sos_trigger_receipt_sos_incident_incident_id",
                schema: "safety",
                table: "sos_trigger_receipt",
                column: "incident_id",
                principalSchema: "safety",
                principalTable: "sos_incident",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_automatic_alert_state_device_device_id",
                schema: "safety",
                table: "automatic_alert_state");

            migrationBuilder.DropForeignKey(
                name: "FK_battery_sample_device_device_id",
                schema: "safety",
                table: "battery_sample");

            migrationBuilder.DropForeignKey(
                name: "FK_location_sample_device_device_id",
                schema: "safety",
                table: "location_sample");

            migrationBuilder.DropForeignKey(
                name: "FK_sos_trigger_receipt_device_device_id",
                schema: "safety",
                table: "sos_trigger_receipt");

            migrationBuilder.DropForeignKey(
                name: "FK_sos_trigger_receipt_sos_incident_incident_id",
                schema: "safety",
                table: "sos_trigger_receipt");

            migrationBuilder.AddForeignKey(
                name: "FK_automatic_alert_state_device_device_id",
                schema: "safety",
                table: "automatic_alert_state",
                column: "device_id",
                principalSchema: "safety",
                principalTable: "device",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_battery_sample_device_device_id",
                schema: "safety",
                table: "battery_sample",
                column: "device_id",
                principalSchema: "safety",
                principalTable: "device",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_location_sample_device_device_id",
                schema: "safety",
                table: "location_sample",
                column: "device_id",
                principalSchema: "safety",
                principalTable: "device",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sos_trigger_receipt_device_device_id",
                schema: "safety",
                table: "sos_trigger_receipt",
                column: "device_id",
                principalSchema: "safety",
                principalTable: "device",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sos_trigger_receipt_sos_incident_incident_id",
                schema: "safety",
                table: "sos_trigger_receipt",
                column: "incident_id",
                principalSchema: "safety",
                principalTable: "sos_incident",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
