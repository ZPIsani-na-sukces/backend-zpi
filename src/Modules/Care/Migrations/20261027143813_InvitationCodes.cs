using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Care.Migrations
{
    public partial class InvitationCodes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_senior_care_relationship_senior_care_invitation_invitation_~",
                schema: "care",
                table: "senior_care_relationship");

            migrationBuilder.Sql("DELETE FROM care.senior_care_invitation");

            migrationBuilder.DropIndex(
                name: "IX_senior_care_relationship_invitation_id",
                schema: "care",
                table: "senior_care_relationship");

            migrationBuilder.DropIndex(
                name: "IX_senior_care_invitation_senior_id_caretaker_id",
                schema: "care",
                table: "senior_care_invitation");

            migrationBuilder.DropCheckConstraint(
                name: "ck_senior_care_invitation_parties",
                schema: "care",
                table: "senior_care_invitation");

            migrationBuilder.DropCheckConstraint(
                name: "ck_senior_care_invitation_status",
                schema: "care",
                table: "senior_care_invitation");

            migrationBuilder.DropColumn(
                name: "invitation_id",
                schema: "care",
                table: "senior_care_relationship");

            migrationBuilder.DropColumn(
                name: "caretaker_id",
                schema: "care",
                table: "senior_care_invitation");

            migrationBuilder.DropColumn(
                name: "senior_id",
                schema: "care",
                table: "senior_care_invitation");

            migrationBuilder.AlterColumn<Guid>(
                name: "recipient_account_id",
                schema: "care",
                table: "senior_care_invitation",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "code",
                schema: "care",
                table: "senior_care_invitation",
                type: "character varying(6)",
                maxLength: 6,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "relationship_id",
                schema: "care",
                table: "senior_care_invitation",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_senior_care_invitation_code",
                schema: "care",
                table: "senior_care_invitation",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_senior_care_invitation_relationship_id",
                schema: "care",
                table: "senior_care_invitation",
                column: "relationship_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_senior_care_invitation_sender_account_id",
                schema: "care",
                table: "senior_care_invitation",
                column: "sender_account_id",
                unique: true,
                filter: "status = 'pending'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_senior_care_invitation_code",
                schema: "care",
                table: "senior_care_invitation",
                sql: "code ~ '^[0-9]{6}$' AND length(code) = 6");

            migrationBuilder.AddCheckConstraint(
                name: "ck_senior_care_invitation_parties",
                schema: "care",
                table: "senior_care_invitation",
                sql: "recipient_account_id IS NULL OR sender_account_id <> recipient_account_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_senior_care_invitation_status",
                schema: "care",
                table: "senior_care_invitation",
                sql: "(status = 'pending' AND recipient_account_id IS NULL AND responded_at IS NULL AND relationship_id IS NULL) OR (status = 'accepted' AND recipient_account_id IS NOT NULL AND responded_at IS NOT NULL AND relationship_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_senior_care_invitation_senior_care_relationship_relationshi~",
                schema: "care",
                table: "senior_care_invitation",
                column: "relationship_id",
                principalSchema: "care",
                principalTable: "senior_care_relationship",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Invitation history was removed. Restore a backup to roll back this migration.");
        }
    }
}
