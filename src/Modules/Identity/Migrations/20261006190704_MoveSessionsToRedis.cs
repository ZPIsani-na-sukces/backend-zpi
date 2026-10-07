using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Identity.Migrations
{
    public partial class MoveSessionsToRedis : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_token",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "account_session",
                schema: "identity");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_session",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_session", x => x.id);
                    table.CheckConstraint("ck_account_session_client_type", "client_type IN ('web','mobile')");
                    table.CheckConstraint("ck_account_session_expiration", "expires_at > created_at");
                    table.CheckConstraint("ck_account_session_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_account_session_account_account_id",
                        column: x => x.account_id,
                        principalSchema: "identity",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_token",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_token", x => x.id);
                    table.CheckConstraint("ck_refresh_token_expiration", "expires_at > created_at");
                    table.ForeignKey(
                        name: "FK_refresh_token_account_session_session_id",
                        column: x => x.session_id,
                        principalSchema: "identity",
                        principalTable: "account_session",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_session_account_id",
                schema: "identity",
                table: "account_session",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_token_session_id",
                schema: "identity",
                table: "refresh_token",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_token_token_hash",
                schema: "identity",
                table: "refresh_token",
                column: "token_hash",
                unique: true);
        }
    }
}
