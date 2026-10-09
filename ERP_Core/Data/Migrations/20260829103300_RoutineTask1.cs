using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class RoutineTask1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "CRoutines");

            migrationBuilder.DropColumn(
                name: "FixedTaskDetails",
                table: "CRoutines");

            migrationBuilder.DropColumn(
                name: "RoutineType",
                table: "CRoutines");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "CRoutines");

            migrationBuilder.DropColumn(
                name: "Time",
                table: "CRoutines");

            migrationBuilder.CreateTable(
                name: "CRoutineDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoutineId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RoutineType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Time = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FixedTaskDetails = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRoutineDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CRoutineDetails_CRoutines_RoutineId",
                        column: x => x.RoutineId,
                        principalTable: "CRoutines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CRoutineDetails_RoutineId",
                table: "CRoutineDetails",
                column: "RoutineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CRoutineDetails");

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

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "CRoutines",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FixedTaskDetails",
                table: "CRoutines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoutineType",
                table: "CRoutines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "CRoutines",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Time",
                table: "CRoutines",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
