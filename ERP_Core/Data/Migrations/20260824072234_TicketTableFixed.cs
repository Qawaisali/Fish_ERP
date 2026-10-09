using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class TicketTableFixed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CTickets",
                table: "CTickets");

            // FIX: Drop the old IDENTITY column instead of trying to rename/alter it
            migrationBuilder.DropColumn(
                name: "TicketID",
                table: "CTickets");

            migrationBuilder.RenameColumn(
                name: "TicketCode",
                table: "CTickets",
                newName: "Code");

            migrationBuilder.RenameColumn(
                name: "AssignDate",
                table: "CTickets",
                newName: "ModifiedOn");

            migrationBuilder.AlterColumn<int>(
                name: "CompanyId",
                table: "CTickets",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // FIX: Add the new non-identity FinancialYearId column
            migrationBuilder.AddColumn<int>(
                name: "FinancialYearId",
                table: "CTickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Add the new IDENTITY Primary Key (Id)
            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "CTickets",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<bool>(
                name: "Active",
                table: "CTickets",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedOn",
                table: "CTickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "CTickets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOn",
                table: "CTickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "CTickets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_CTickets",
                table: "CTickets",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CTickets",
                table: "CTickets");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "CTickets");

            // FIX DOWN: Drop the new columns
            migrationBuilder.DropColumn(
                name: "FinancialYearId",
                table: "CTickets");

            migrationBuilder.DropColumn(
                name: "Active",
                table: "CTickets");

            migrationBuilder.DropColumn(
                name: "AssignedOn",
                table: "CTickets");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CTickets");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                table: "CTickets");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "CTickets");

            migrationBuilder.RenameColumn(
                name: "ModifiedOn",
                table: "CTickets",
                newName: "AssignDate");

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "CTickets",
                newName: "TicketCode");

            migrationBuilder.AlterColumn<int>(
                name: "CompanyId",
                table: "CTickets",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // FIX DOWN: Re-add the old IDENTITY column
            migrationBuilder.AddColumn<int>(
                name: "TicketID",
                table: "CTickets",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CTickets",
                table: "CTickets",
                column: "TicketID");
        }
    }
}