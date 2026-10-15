using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Routines.Migrations
{
    public partial class RoutineReminders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "before_reminder_queued_at",
                schema: "routines",
                table: "task_occurrence",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "due_reminder_queued_at",
                schema: "routines",
                table: "task_occurrence",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "before_reminder_queued_at",
                schema: "routines",
                table: "task_occurrence");

            migrationBuilder.DropColumn(
                name: "due_reminder_queued_at",
                schema: "routines",
                table: "task_occurrence");
        }
    }
}
