using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class OpeningBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OpeningBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OBCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    OBDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    FinancialYearId = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpeningBalances_CCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "CCompanies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBalances_CFinancialYears_FinancialYearId",
                        column: x => x.FinancialYearId,
                        principalTable: "CFinancialYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpeningBalanceDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OBId = table.Column<int>(type: "int", nullable: false),
                    COAId = table.Column<int>(type: "int", nullable: false),
                    OBDtlTranType = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    OBDtlOpenBlnc = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningBalanceDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpeningBalanceDetails_ChartOfAccounts_COAId",
                        column: x => x.COAId,
                        principalTable: "ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBalanceDetails_OpeningBalances_OBId",
                        column: x => x.OBId,
                        principalTable: "OpeningBalances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceDetails_COAId",
                table: "OpeningBalanceDetails",
                column: "COAId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceDetails_OBId",
                table: "OpeningBalanceDetails",
                column: "OBId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_CompanyId",
                table: "OpeningBalances",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_FinancialYearId",
                table: "OpeningBalances",
                column: "FinancialYearId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpeningBalanceDetails");

            migrationBuilder.DropTable(
                name: "OpeningBalances");
        }
    }
}
