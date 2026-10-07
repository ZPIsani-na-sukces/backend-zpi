using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Identity.Migrations
{
    public partial class AccountLifecycle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_account_status",
                schema: "identity",
                table: "account");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deletion_requested_at",
                schema: "identity",
                table: "account",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "security_stamp",
                schema: "identity",
                table: "account",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_account_deletion_requested_at",
                schema: "identity",
                table: "account",
                column: "deletion_requested_at");

            migrationBuilder.AddCheckConstraint(
                name: "ck_account_status",
                schema: "identity",
                table: "account",
                sql: "status IN ('pendingPhoneVerification','active','blocked','pendingDeletion')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_account_deletion_requested_at",
                schema: "identity",
                table: "account");

            migrationBuilder.DropCheckConstraint(
                name: "ck_account_status",
                schema: "identity",
                table: "account");

            migrationBuilder.DropColumn(
                name: "deletion_requested_at",
                schema: "identity",
                table: "account");

            migrationBuilder.DropColumn(
                name: "security_stamp",
                schema: "identity",
                table: "account");

            migrationBuilder.AddCheckConstraint(
                name: "ck_account_status",
                schema: "identity",
                table: "account",
                sql: "status IN ('pendingPhoneVerification','active','blocked')");
        }
    }
}
