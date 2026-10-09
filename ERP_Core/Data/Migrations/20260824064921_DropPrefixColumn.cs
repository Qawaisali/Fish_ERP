using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropPrefixColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CUserRoles");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AspNetUsers_EmpId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ProjectPrefix",
                table: "CProjectTypes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProjectPrefix",
                table: "CProjectTypes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AspNetUsers_EmpId",
                table: "AspNetUsers",
                column: "EmpId");

            migrationBuilder.CreateTable(
                name: "CUserRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "EmpId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CUserRoles_CRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "CRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CUserRoles_RoleId",
                table: "CUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_CUserRoles_UserId",
                table: "CUserRoles",
                column: "UserId");
        }
    }
}
