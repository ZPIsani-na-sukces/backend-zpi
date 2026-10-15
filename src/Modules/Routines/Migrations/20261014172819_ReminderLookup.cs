using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Routines.Migrations
{
    public partial class ReminderLookup : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_task_occurrence_scheduled_at",
                schema: "routines",
                table: "task_occurrence",
                column: "scheduled_at",
                filter: "status = 'pending'");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_task_occurrence_scheduled_at",
                schema: "routines",
                table: "task_occurrence");
        }
    }
}
