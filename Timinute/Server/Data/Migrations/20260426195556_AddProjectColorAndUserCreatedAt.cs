using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Timinute.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectColorAndUserCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "Projects",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);

            // Backfill existing projects with palette colors, cycled by row number ordered by ProjectId.
            migrationBuilder.Sql(@"
                WITH Ordered AS (
                    SELECT ProjectId,
                           (ROW_NUMBER() OVER (ORDER BY ProjectId) - 1) % 5 AS Idx
                    FROM Projects
                    WHERE Color IS NULL
                )
                UPDATE p
                SET Color = CASE o.Idx
                                WHEN 0 THEN '#6366F1'
                                WHEN 1 THEN '#F59E0B'
                                WHEN 2 THEN '#10B981'
                                WHEN 3 THEN '#EC4899'
                                ELSE '#94A3B8'
                            END
                FROM Projects p
                INNER JOIN Ordered o ON p.ProjectId = o.ProjectId;
            ");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2022, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "b2c3d4e5-f6a7-4b5c-8d7e-0f1a2b3c4d5e",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2022, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "c3d4e5f6-a7b8-4c5d-8e7f-1a2b3c4d5e6f",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2022, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Color",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AspNetUsers");
        }
    }
}
