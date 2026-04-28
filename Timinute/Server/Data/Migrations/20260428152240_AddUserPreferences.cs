using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Timinute.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Preferences_Theme",
                table: "AspNetUsers",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<decimal>(
                name: "Preferences_WeeklyGoalHours",
                table: "AspNetUsers",
                type: "decimal(4,1)",
                nullable: false,
                defaultValue: 32.0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Preferences_WorkdayHoursPerDay",
                table: "AspNetUsers",
                type: "decimal(4,1)",
                nullable: false,
                defaultValue: 8.0m);

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d",
                columns: new[] { "Preferences_WeeklyGoalHours", "Preferences_WorkdayHoursPerDay" },
                values: new object[] { 32.0m, 8.0m });

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "b2c3d4e5-f6a7-4b5c-8d7e-0f1a2b3c4d5e",
                columns: new[] { "Preferences_WeeklyGoalHours", "Preferences_WorkdayHoursPerDay" },
                values: new object[] { 32.0m, 8.0m });

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "c3d4e5f6-a7b8-4c5d-8e7f-1a2b3c4d5e6f",
                columns: new[] { "Preferences_WeeklyGoalHours", "Preferences_WorkdayHoursPerDay" },
                values: new object[] { 32.0m, 8.0m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Preferences_Theme",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Preferences_WeeklyGoalHours",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Preferences_WorkdayHoursPerDay",
                table: "AspNetUsers");
        }
    }
}
