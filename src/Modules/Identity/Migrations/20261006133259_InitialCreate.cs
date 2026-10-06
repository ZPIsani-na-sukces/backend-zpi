using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814

namespace Zpi.Modules.Identity.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "outbox_message",
                schema: "identity",
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
                name: "role",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role", x => x.id);
                    table.CheckConstraint("ck_role_code", "code IN ('senior','caretaker')");
                });

            migrationBuilder.CreateTable(
                name: "account",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    normalized_phone_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    phone_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    email_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account", x => x.id);
                    table.CheckConstraint("ck_account_email_verification", "email_verified_at IS NULL OR email IS NOT NULL");
                    table.CheckConstraint("ck_account_first_name_nonempty", "length(first_name) >= 1");
                    table.CheckConstraint("ck_account_last_name_nonempty", "length(last_name) >= 1");
                    table.CheckConstraint("ck_account_password_hash_nonempty", "length(password_hash) >= 1");
                    table.CheckConstraint("ck_account_phone", "phone_number ~ '^\\+[1-9][0-9]{1,14}$'");
                    table.CheckConstraint("ck_account_status", "status IN ('pendingPhoneVerification','active','blocked')");
                    table.CheckConstraint("ck_account_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_account_role_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "verification_ticket",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verification_ticket", x => x.id);
                    table.CheckConstraint("ck_verification_ticket_expiration", "expires_at > created_at");
                    table.ForeignKey(
                        name: "FK_verification_ticket_account_account_id",
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
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "verification_challenge",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purpose = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    destination = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    secret_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resend_after = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    invalidated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verification_challenge", x => x.id);
                    table.CheckConstraint("ck_verification_challenge_attempts", "attempts BETWEEN 0 AND 5");
                    table.CheckConstraint("ck_verification_challenge_channel", "channel IN ('sms','email')");
                    table.CheckConstraint("ck_verification_challenge_expiration", "expires_at > created_at");
                    table.CheckConstraint("ck_verification_challenge_purpose", "purpose IN ('phoneVerification','emailVerification','passwordReset')");
                    table.CheckConstraint("ck_verification_challenge_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_verification_challenge_account_account_id",
                        column: x => x.account_id,
                        principalSchema: "identity",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_verification_challenge_verification_ticket_ticket_id",
                        column: x => x.ticket_id,
                        principalSchema: "identity",
                        principalTable: "verification_ticket",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "role",
                columns: new[] { "id", "code", "name" },
                values: new object[,]
                {
                    { new Guid("019a0000-0000-7000-8000-000000000001"), "senior", "Senior" },
                    { new Guid("019a0000-0000-7000-8000-000000000002"), "caretaker", "Caretaker" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_normalized_email",
                schema: "identity",
                table: "account",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_normalized_phone_number",
                schema: "identity",
                table: "account",
                column: "normalized_phone_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_role_id",
                schema: "identity",
                table: "account",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_account_session_account_id",
                schema: "identity",
                table: "account_session",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_message_next_attempt_at_occurred_at",
                schema: "identity",
                table: "outbox_message",
                columns: new[] { "next_attempt_at", "occurred_at" },
                filter: "processed_at IS NULL");

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

            migrationBuilder.CreateIndex(
                name: "IX_role_code",
                schema: "identity",
                table: "role",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_verification_challenge_account_id",
                schema: "identity",
                table: "verification_challenge",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_verification_challenge_ticket_id",
                schema: "identity",
                table: "verification_challenge",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "IX_verification_ticket_account_id",
                schema: "identity",
                table: "verification_ticket",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_verification_ticket_token_hash",
                schema: "identity",
                table: "verification_ticket",
                column: "token_hash",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_message",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "refresh_token",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "verification_challenge",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "account_session",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "verification_ticket",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "account",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "role",
                schema: "identity");
        }
    }
}
