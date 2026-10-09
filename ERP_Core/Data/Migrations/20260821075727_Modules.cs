using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Modules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CModules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CModules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CUserRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    UserId1 = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CUserRoles_AspNetUsers_UserId1",
                        column: x => x.UserId1,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CUserRoles_CRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "CRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CRoleModulesPermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    ModuleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRoleModulesPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CRoleModulesPermissions_CModules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "CModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CRoleModulesPermissions_CRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "CRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CRoleModulesPermissions_ModuleId",
                table: "CRoleModulesPermissions",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_CRoleModulesPermissions_RoleId",
                table: "CRoleModulesPermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_CUserRoles_RoleId",
                table: "CUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_CUserRoles_UserId1",
                table: "CUserRoles",
                column: "UserId1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CRoleModulesPermissions");

            migrationBuilder.DropTable(
                name: "CUserRoles");

            migrationBuilder.DropTable(
                name: "CModules");
        }
    }
}
