using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNatureofAccountTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'NatureofAccount')
BEGIN
    CREATE TABLE [dbo].[NatureofAccount](
        [Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [AccountName] [varchar](100) NULL,
        [DebtorOrCreditor] [nchar](1) NULL
    );
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'NatureofAccount')
BEGIN
    DROP TABLE [dbo].[NatureofAccount];
END
");
        }
    }
}
