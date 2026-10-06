using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Routines.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "routines");

            migrationBuilder.CreateTable(
                name: "outbox_message",
                schema: "routines",
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
                name: "routine_task",
                schema: "routines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    senior_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    instructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    schedule_revision = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_routine_task", x => x.id);
                    table.CheckConstraint("ck_routine_task_dates", "ends_on IS NULL OR ends_on >= starts_on");
                    table.CheckConstraint("ck_routine_task_revision", "schedule_revision >= 1");
                    table.CheckConstraint("ck_routine_task_type", "type IN ('medication','meal','hydration')");
                    table.CheckConstraint("ck_routine_task_version", "version >= 1");
                    table.CheckConstraint("ck_routinetask_instructions_nonempty", "length(instructions) >= 1");
                    table.CheckConstraint("ck_routinetask_title_nonempty", "length(title) >= 1");
                });

            migrationBuilder.CreateTable(
                name: "routine_task_schedule",
                schema: "routines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    routine_task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    time_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    days_of_week = table.Column<int[]>(type: "integer[]", nullable: false),
                    time_of_day = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routine_task_schedule", x => x.id);
                    table.CheckConstraint("ck_routine_task_schedule_days", "cardinality(days_of_week) BETWEEN 1 AND 7 AND days_of_week <@ ARRAY[1,2,3,4,5,6,7]");
                    table.CheckConstraint("ck_routine_task_schedule_revision", "revision >= 1");
                    table.CheckConstraint("ck_routinetaskschedule_time_zone_nonempty", "length(time_zone) >= 1");
                    table.ForeignKey(
                        name: "FK_routine_task_schedule_routine_task_routine_task_id",
                        column: x => x.routine_task_id,
                        principalSchema: "routines",
                        principalTable: "routine_task",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_occurrence",
                schema: "routines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    senior_id = table.Column<Guid>(type: "uuid", nullable: false),
                    routine_task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_revision = table.Column<int>(type: "integer", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    local_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    time_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    instructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_occurrence", x => x.id);
                    table.CheckConstraint("ck_task_occurrence_revision", "schedule_revision >= 1");
                    table.CheckConstraint("ck_task_occurrence_status", "status IN ('pending','confirmed','skipped','missed','cancelled')");
                    table.CheckConstraint("ck_task_occurrence_type", "type IN ('medication','meal','hydration')");
                    table.CheckConstraint("ck_task_occurrence_version", "version >= 1");
                    table.CheckConstraint("ck_taskoccurrence_instructions_nonempty", "length(instructions) >= 1");
                    table.CheckConstraint("ck_taskoccurrence_title_nonempty", "length(title) >= 1");
                    table.ForeignKey(
                        name: "FK_task_occurrence_routine_task_routine_task_id",
                        column: x => x.routine_task_id,
                        principalSchema: "routines",
                        principalTable: "routine_task",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_confirmation",
                schema: "routines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_occurrence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    performed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_by_account_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_confirmation", x => x.id);
                    table.CheckConstraint("ck_task_confirmation_decision", "decision IN ('confirmed','skipped')");
                    table.ForeignKey(
                        name: "FK_task_confirmation_task_occurrence_task_occurrence_id",
                        column: x => x.task_occurrence_id,
                        principalSchema: "routines",
                        principalTable: "task_occurrence",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_message_next_attempt_at_occurred_at",
                schema: "routines",
                table: "outbox_message",
                columns: new[] { "next_attempt_at", "occurred_at" },
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_routine_task_senior_id",
                schema: "routines",
                table: "routine_task",
                column: "senior_id");

            migrationBuilder.CreateIndex(
                name: "IX_routine_task_schedule_routine_task_id_revision_time_of_day",
                schema: "routines",
                table: "routine_task_schedule",
                columns: new[] { "routine_task_id", "revision", "time_of_day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_confirmation_task_occurrence_id",
                schema: "routines",
                table: "task_confirmation",
                column: "task_occurrence_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_occurrence_routine_task_id_schedule_revision_local_dat~",
                schema: "routines",
                table: "task_occurrence",
                columns: new[] { "routine_task_id", "schedule_revision", "local_date", "local_time" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_occurrence_routine_task_id_schedule_revision_scheduled~",
                schema: "routines",
                table: "task_occurrence",
                columns: new[] { "routine_task_id", "schedule_revision", "scheduled_at" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_occurrence_senior_id_scheduled_at",
                schema: "routines",
                table: "task_occurrence",
                columns: new[] { "senior_id", "scheduled_at" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_message",
                schema: "routines");

            migrationBuilder.DropTable(
                name: "routine_task_schedule",
                schema: "routines");

            migrationBuilder.DropTable(
                name: "task_confirmation",
                schema: "routines");

            migrationBuilder.DropTable(
                name: "task_occurrence",
                schema: "routines");

            migrationBuilder.DropTable(
                name: "routine_task",
                schema: "routines");
        }
    }
}
