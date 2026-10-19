using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Identity.Migrations
{
    public partial class CascadeOwnedData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_verification_challenge_account_account_id",
                schema: "identity",
                table: "verification_challenge");

            migrationBuilder.DropForeignKey(
                name: "FK_verification_challenge_verification_ticket_ticket_id",
                schema: "identity",
                table: "verification_challenge");

            migrationBuilder.DropForeignKey(
                name: "FK_verification_ticket_account_account_id",
                schema: "identity",
                table: "verification_ticket");

            migrationBuilder.AddForeignKey(
                name: "FK_verification_challenge_account_account_id",
                schema: "identity",
                table: "verification_challenge",
                column: "account_id",
                principalSchema: "identity",
                principalTable: "account",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_verification_challenge_verification_ticket_ticket_id",
                schema: "identity",
                table: "verification_challenge",
                column: "ticket_id",
                principalSchema: "identity",
                principalTable: "verification_ticket",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_verification_ticket_account_account_id",
                schema: "identity",
                table: "verification_ticket",
                column: "account_id",
                principalSchema: "identity",
                principalTable: "account",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_verification_challenge_account_account_id",
                schema: "identity",
                table: "verification_challenge");

            migrationBuilder.DropForeignKey(
                name: "FK_verification_challenge_verification_ticket_ticket_id",
                schema: "identity",
                table: "verification_challenge");

            migrationBuilder.DropForeignKey(
                name: "FK_verification_ticket_account_account_id",
                schema: "identity",
                table: "verification_ticket");

            migrationBuilder.AddForeignKey(
                name: "FK_verification_challenge_account_account_id",
                schema: "identity",
                table: "verification_challenge",
                column: "account_id",
                principalSchema: "identity",
                principalTable: "account",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_verification_challenge_verification_ticket_ticket_id",
                schema: "identity",
                table: "verification_challenge",
                column: "ticket_id",
                principalSchema: "identity",
                principalTable: "verification_ticket",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_verification_ticket_account_account_id",
                schema: "identity",
                table: "verification_ticket",
                column: "account_id",
                principalSchema: "identity",
                principalTable: "account",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
