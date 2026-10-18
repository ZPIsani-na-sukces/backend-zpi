using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.MedicalRecords.Migrations
{
    public partial class MedicalFileStorageAndAudit : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "purged_at",
                schema: "medical",
                table: "medical_file",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "medical_audit_entry",
                schema: "medical",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    senior_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medical_audit_entry", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_medical_audit_entry_senior_id_occurred_at",
                schema: "medical",
                table: "medical_audit_entry",
                columns: new[] { "senior_id", "occurred_at" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "medical_audit_entry",
                schema: "medical");

            migrationBuilder.DropColumn(
                name: "purged_at",
                schema: "medical",
                table: "medical_file");
        }
    }
}
