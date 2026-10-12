using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.SeniorProfile.Migrations
{
    public partial class ContactSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contact_settings",
                schema: "profile",
                columns: table => new
                {
                    senior_id = table.Column<Guid>(type: "uuid", nullable: false),
                    senior_editing_locked = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_settings", x => x.senior_id);
                    table.CheckConstraint("ck_contact_settings_version", "version >= 1");
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contact_settings",
                schema: "profile");
        }
    }
}
