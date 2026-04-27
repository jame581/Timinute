using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Timinute.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "TrackedTasks",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Projects",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "a7b8c9d0-e1f2-4a5b-8c7d-5e6f7a8b9c0d",
                column: "DeletedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "b8c9d0e1-f2a3-4b5c-8d7e-6f7a8b9c0d1e",
                column: "DeletedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "c9d0e1f2-a3b4-4c5d-8e7f-7a8b9c0d1e2f",
                column: "DeletedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "d0e1f2a3-b4c5-4d5e-8f7a-8b9c0d1e2f3a",
                column: "DeletedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "d4e5f6a7-b8c9-4d5e-8f7a-2b3c4d5e6f7a",
                column: "DeletedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "e5f6a7b8-c9d0-4e5f-8a7b-3c4d5e6f7a8b",
                column: "DeletedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "f6a7b8c9-d0e1-4f5a-8b7c-4d5e6f7a8b9c",
                column: "DeletedAt",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_TrackedTasks_DeletedAt",
                table: "TrackedTasks",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_DeletedAt",
                table: "Projects",
                column: "DeletedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackedTasks_DeletedAt",
                table: "TrackedTasks");

            migrationBuilder.DropIndex(
                name: "IX_Projects_DeletedAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "TrackedTasks");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Projects");
        }
    }
}
