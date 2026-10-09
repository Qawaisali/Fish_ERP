using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountSetup1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsParentSupplierAcc",
                table: "AccountSetup");

            migrationBuilder.DropColumn(
                name: "VatInputIsParentAcc",
                table: "AccountSetup");

            migrationBuilder.DropColumn(
                name: "VatOutputisParentAcc",
                table: "AccountSetup");

            migrationBuilder.DropColumn(
                name: "isParentAcc",
                table: "AccountSetup");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsParentSupplierAcc",
                table: "AccountSetup",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VatInputIsParentAcc",
                table: "AccountSetup",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VatOutputisParentAcc",
                table: "AccountSetup",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "isParentAcc",
                table: "AccountSetup",
                type: "bit",
                nullable: true);
        }
    }
}
