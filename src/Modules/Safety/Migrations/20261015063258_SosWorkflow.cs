using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Safety.Migrations
{
    public partial class SosWorkflow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sos_trigger_receipt",
                schema: "safety",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    triggered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sos_trigger_receipt", x => new { x.device_id, x.triggered_at });
                    table.ForeignKey(
                        name: "FK_sos_trigger_receipt_device_device_id",
                        column: x => x.device_id,
                        principalSchema: "safety",
                        principalTable: "device",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sos_trigger_receipt_sos_incident_incident_id",
                        column: x => x.incident_id,
                        principalSchema: "safety",
                        principalTable: "sos_incident",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sos_incident_senior_id",
                schema: "safety",
                table: "sos_incident",
                column: "senior_id",
                unique: true,
                filter: "status <> 'resolved'");

            migrationBuilder.CreateIndex(
                name: "IX_sos_trigger_receipt_incident_id",
                schema: "safety",
                table: "sos_trigger_receipt",
                column: "incident_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sos_trigger_receipt",
                schema: "safety");

            migrationBuilder.DropIndex(
                name: "IX_sos_incident_senior_id",
                schema: "safety",
                table: "sos_incident");
        }
    }
}
