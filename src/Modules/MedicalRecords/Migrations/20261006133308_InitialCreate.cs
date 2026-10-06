using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.MedicalRecords.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "medical");

            migrationBuilder.CreateTable(
                name: "medical_record",
                schema: "medical",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    senior_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    record_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    document_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medical_record", x => x.id);
                    table.CheckConstraint("ck_medical_record_record_type", "record_type IN ('testResult','prescription','dischargeSummary','consultation','other')");
                    table.CheckConstraint("ck_medical_record_version", "version >= 1");
                    table.CheckConstraint("ck_medicalrecord_title_nonempty", "length(title) >= 1");
                });

            migrationBuilder.CreateTable(
                name: "outbox_message",
                schema: "medical",
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
                name: "medical_file",
                schema: "medical",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    medical_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    original_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    checksum_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medical_file", x => x.id);
                    table.CheckConstraint("ck_medical_file_checksum", "checksum_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_medical_file_mime", "mime_type IN ('application/pdf','image/jpeg','image/png')");
                    table.CheckConstraint("ck_medical_file_size", "size_bytes BETWEEN 0 AND 20971520");
                    table.ForeignKey(
                        name: "FK_medical_file_medical_record_medical_record_id",
                        column: x => x.medical_record_id,
                        principalSchema: "medical",
                        principalTable: "medical_record",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_medical_file_medical_record_id",
                schema: "medical",
                table: "medical_file",
                column: "medical_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_medical_file_object_key",
                schema: "medical",
                table: "medical_file",
                column: "object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_medical_record_senior_id_document_date",
                schema: "medical",
                table: "medical_record",
                columns: new[] { "senior_id", "document_date" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_message_next_attempt_at_occurred_at",
                schema: "medical",
                table: "outbox_message",
                columns: new[] { "next_attempt_at", "occurred_at" },
                filter: "processed_at IS NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "medical_file",
                schema: "medical");

            migrationBuilder.DropTable(
                name: "outbox_message",
                schema: "medical");

            migrationBuilder.DropTable(
                name: "medical_record",
                schema: "medical");
        }
    }
}
