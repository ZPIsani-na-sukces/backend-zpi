using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.MedicalRecords.Migrations
{
    public partial class CascadeOwnedData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_medical_file_medical_record_medical_record_id",
                schema: "medical",
                table: "medical_file");

            migrationBuilder.AddForeignKey(
                name: "FK_medical_file_medical_record_medical_record_id",
                schema: "medical",
                table: "medical_file",
                column: "medical_record_id",
                principalSchema: "medical",
                principalTable: "medical_record",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_medical_file_medical_record_medical_record_id",
                schema: "medical",
                table: "medical_file");

            migrationBuilder.AddForeignKey(
                name: "FK_medical_file_medical_record_medical_record_id",
                schema: "medical",
                table: "medical_file",
                column: "medical_record_id",
                principalSchema: "medical",
                principalTable: "medical_record",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
