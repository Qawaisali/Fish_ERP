using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class RoutineTaskTable1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TaskDate",
                table: "CRoutineTasks",
                type: "datetime2",
                nullable: false,
                computedColumnSql: "CAST([Time] AS DATE)",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_CRoutineTasks_RoutineDetailId_TaskDate",
                table: "CRoutineTasks",
                columns: new[] { "RoutineDetailId", "TaskDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CRoutineTasks_RoutineDetailId_TaskDate",
                table: "CRoutineTasks");

            migrationBuilder.DropColumn(
                name: "TaskDate",
                table: "CRoutineTasks");
        }
    }
}
