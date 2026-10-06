using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Care.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "care");

            migrationBuilder.CreateTable(
                name: "outbox_message",
                schema: "care",
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
                name: "senior_care_invitation",
                schema: "care",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    senior_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caretaker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_senior_care_invitation", x => x.id);
                    table.CheckConstraint("ck_senior_care_invitation_expiration", "expires_at > created_at");
                    table.CheckConstraint("ck_senior_care_invitation_parties", "senior_id <> caretaker_id AND sender_account_id = caretaker_id AND recipient_account_id = senior_id");
                    table.CheckConstraint("ck_senior_care_invitation_status", "status IN ('pending','accepted','rejected','cancelled','expired')");
                    table.CheckConstraint("ck_senior_care_invitation_version", "version >= 1");
                });

            migrationBuilder.CreateTable(
                name: "senior_care_relationship",
                schema: "care",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    senior_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caretaker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_senior_care_relationship", x => x.id);
                    table.CheckConstraint("ck_senior_care_relationship_parties", "senior_id <> caretaker_id");
                    table.CheckConstraint("ck_senior_care_relationship_removed", "(status = 'active' AND removed_at IS NULL) OR (status = 'removed' AND removed_at IS NOT NULL)");
                    table.CheckConstraint("ck_senior_care_relationship_status", "status IN ('active','removed')");
                    table.CheckConstraint("ck_senior_care_relationship_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_senior_care_relationship_senior_care_invitation_invitation_~",
                        column: x => x.invitation_id,
                        principalSchema: "care",
                        principalTable: "senior_care_invitation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_message_next_attempt_at_occurred_at",
                schema: "care",
                table: "outbox_message",
                columns: new[] { "next_attempt_at", "occurred_at" },
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_senior_care_invitation_expires_at",
                schema: "care",
                table: "senior_care_invitation",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_senior_care_invitation_recipient_account_id",
                schema: "care",
                table: "senior_care_invitation",
                column: "recipient_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_senior_care_invitation_senior_id_caretaker_id",
                schema: "care",
                table: "senior_care_invitation",
                columns: new[] { "senior_id", "caretaker_id" },
                unique: true,
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "IX_senior_care_relationship_caretaker_id",
                schema: "care",
                table: "senior_care_relationship",
                column: "caretaker_id");

            migrationBuilder.CreateIndex(
                name: "IX_senior_care_relationship_invitation_id",
                schema: "care",
                table: "senior_care_relationship",
                column: "invitation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_senior_care_relationship_senior_id_caretaker_id",
                schema: "care",
                table: "senior_care_relationship",
                columns: new[] { "senior_id", "caretaker_id" },
                unique: true,
                filter: "status = 'active'");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_message",
                schema: "care");

            migrationBuilder.DropTable(
                name: "senior_care_relationship",
                schema: "care");

            migrationBuilder.DropTable(
                name: "senior_care_invitation",
                schema: "care");
        }
    }
}
