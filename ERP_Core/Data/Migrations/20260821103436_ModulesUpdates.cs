using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ModulesUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Active",
                table: "CModules",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "CModules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOn",
                table: "CModules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "CModules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedOn",
                table: "CModules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentModuleId",
                table: "CModules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "isParentModule",
                table: "CModules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "isSubMoudle",
                table: "CModules",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Active",
                table: "CModules");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CModules");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                table: "CModules");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "CModules");

            migrationBuilder.DropColumn(
                name: "ModifiedOn",
                table: "CModules");

            migrationBuilder.DropColumn(
                name: "ParentModuleId",
                table: "CModules");

            migrationBuilder.DropColumn(
                name: "isParentModule",
                table: "CModules");

            migrationBuilder.DropColumn(
                name: "isSubMoudle",
                table: "CModules");
        }
    }
}
