using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Routines.Migrations
{
    public partial class RemoveTaskOccurrenceUniqueIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_task_occurrence_routine_task_id_schedule_revision_local_dat~",
                schema: "routines",
                table: "task_occurrence");

            migrationBuilder.DropIndex(
                name: "IX_task_occurrence_routine_task_id_schedule_revision_scheduled~",
                schema: "routines",
                table: "task_occurrence");

            migrationBuilder.CreateIndex(
                name: "IX_task_occurrence_routine_task_id",
                schema: "routines",
                table: "task_occurrence",
                column: "routine_task_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_task_occurrence_routine_task_id",
                schema: "routines",
                table: "task_occurrence");

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
        }
    }
}
