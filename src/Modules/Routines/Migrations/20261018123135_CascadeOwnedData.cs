using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Routines.Migrations
{
    public partial class CascadeOwnedData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_routine_task_schedule_routine_task_routine_task_id",
                schema: "routines",
                table: "routine_task_schedule");

            migrationBuilder.DropForeignKey(
                name: "FK_task_confirmation_task_occurrence_task_occurrence_id",
                schema: "routines",
                table: "task_confirmation");

            migrationBuilder.DropForeignKey(
                name: "FK_task_occurrence_routine_task_routine_task_id",
                schema: "routines",
                table: "task_occurrence");

            migrationBuilder.AddForeignKey(
                name: "FK_routine_task_schedule_routine_task_routine_task_id",
                schema: "routines",
                table: "routine_task_schedule",
                column: "routine_task_id",
                principalSchema: "routines",
                principalTable: "routine_task",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_task_confirmation_task_occurrence_task_occurrence_id",
                schema: "routines",
                table: "task_confirmation",
                column: "task_occurrence_id",
                principalSchema: "routines",
                principalTable: "task_occurrence",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_task_occurrence_routine_task_routine_task_id",
                schema: "routines",
                table: "task_occurrence",
                column: "routine_task_id",
                principalSchema: "routines",
                principalTable: "routine_task",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_routine_task_schedule_routine_task_routine_task_id",
                schema: "routines",
                table: "routine_task_schedule");

            migrationBuilder.DropForeignKey(
                name: "FK_task_confirmation_task_occurrence_task_occurrence_id",
                schema: "routines",
                table: "task_confirmation");

            migrationBuilder.DropForeignKey(
                name: "FK_task_occurrence_routine_task_routine_task_id",
                schema: "routines",
                table: "task_occurrence");

            migrationBuilder.AddForeignKey(
                name: "FK_routine_task_schedule_routine_task_routine_task_id",
                schema: "routines",
                table: "routine_task_schedule",
                column: "routine_task_id",
                principalSchema: "routines",
                principalTable: "routine_task",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_task_confirmation_task_occurrence_task_occurrence_id",
                schema: "routines",
                table: "task_confirmation",
                column: "task_occurrence_id",
                principalSchema: "routines",
                principalTable: "task_occurrence",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_task_occurrence_routine_task_routine_task_id",
                schema: "routines",
                table: "task_occurrence",
                column: "routine_task_id",
                principalSchema: "routines",
                principalTable: "routine_task",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
