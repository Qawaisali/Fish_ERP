using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class RoutineTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Deadline",
                table: "CTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoutineTaskId",
                table: "CTasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "isRoutine",
                table: "CTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "Deadline",
                table: "CRoutines",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Deadline",
                table: "CTasks");

            migrationBuilder.DropColumn(
                name: "RoutineTaskId",
                table: "CTasks");

            migrationBuilder.DropColumn(
                name: "isRoutine",
                table: "CTasks");

            migrationBuilder.DropColumn(
                name: "Deadline",
                table: "CRoutines");
        }
    }
}
