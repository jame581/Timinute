using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Timinute.Server.Data.Migrations
{
    public partial class ApplicationRoleValues : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Project_Companies_CompanyId",
                table: "Project");

            migrationBuilder.DropForeignKey(
                name: "FK_TrackedTask_AspNetUsers_UserId",
                table: "TrackedTask");

            migrationBuilder.DropForeignKey(
                name: "FK_TrackedTask_Project_ProjectId",
                table: "TrackedTask");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TrackedTask",
                table: "TrackedTask");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Project",
                table: "Project");

            migrationBuilder.RenameTable(
                name: "TrackedTask",
                newName: "TrackedTasks");

            migrationBuilder.RenameTable(
                name: "Project",
                newName: "Projects");

            migrationBuilder.RenameIndex(
                name: "IX_TrackedTask_UserId",
                table: "TrackedTasks",
                newName: "IX_TrackedTasks_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_TrackedTask_ProjectId",
                table: "TrackedTasks",
                newName: "IX_TrackedTasks_ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_Project_CompanyId",
                table: "Projects",
                newName: "IX_Projects_CompanyId");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "AspNetRoles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "AspNetRoles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TrackedTasks",
                table: "TrackedTasks",
                column: "TaskId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Projects",
                table: "Projects",
                column: "ProjectId");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Description", "Discriminator", "Name", "NormalizedName" },
                values: new object[] { "113a9386-9a69-414b-9a8a-77446eaadb6e", "e2ba2eef-f33c-4371-9ea0-cb211b09138a", "Basic role with lowest rights.", "ApplicationRole", "Basic", "BASIC" });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Description", "Discriminator", "Name", "NormalizedName" },
                values: new object[] { "61a0ebb3-22c2-409c-85bb-dd944fc4dcc8", "33d6b824-08a7-40a0-bee9-83b658b6e395", "Admin role with highest rights.", "ApplicationRole", "Admin", "ADMIN" });

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Companies_CompanyId",
                table: "Projects",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_TrackedTasks_AspNetUsers_UserId",
                table: "TrackedTasks",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TrackedTasks_Projects_ProjectId",
                table: "TrackedTasks",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "ProjectId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Companies_CompanyId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_TrackedTasks_AspNetUsers_UserId",
                table: "TrackedTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_TrackedTasks_Projects_ProjectId",
                table: "TrackedTasks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TrackedTasks",
                table: "TrackedTasks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Projects",
                table: "Projects");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "113a9386-9a69-414b-9a8a-77446eaadb6e");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "61a0ebb3-22c2-409c-85bb-dd944fc4dcc8");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "AspNetRoles");

            migrationBuilder.RenameTable(
                name: "TrackedTasks",
                newName: "TrackedTask");

            migrationBuilder.RenameTable(
                name: "Projects",
                newName: "Project");

            migrationBuilder.RenameIndex(
                name: "IX_TrackedTasks_UserId",
                table: "TrackedTask",
                newName: "IX_TrackedTask_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_TrackedTasks_ProjectId",
                table: "TrackedTask",
                newName: "IX_TrackedTask_ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_Projects_CompanyId",
                table: "Project",
                newName: "IX_Project_CompanyId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TrackedTask",
                table: "TrackedTask",
                column: "TaskId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Project",
                table: "Project",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Project_Companies_CompanyId",
                table: "Project",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_TrackedTask_AspNetUsers_UserId",
                table: "TrackedTask",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TrackedTask_Project_ProjectId",
                table: "TrackedTask",
                column: "ProjectId",
                principalTable: "Project",
                principalColumn: "ProjectId");
        }
    }
}
