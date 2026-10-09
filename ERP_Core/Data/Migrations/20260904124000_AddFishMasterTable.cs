using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Core.Data.Migrations
{
    public partial class AddFishMasterTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Fishes')
                BEGIN
                    CREATE TABLE [dbo].[Fishes] (
                        [Id] int NOT NULL IDENTITY,
                        [Code] varchar(50) NULL,
                        [Name] nvarchar(100) NOT NULL,
                        [Type] nvarchar(100) NOT NULL,
                        [Unit] varchar(20) NULL,
                        [CompanyId] int NULL,
                        [CreatedOn] datetime2 NULL,
                        [CreatedBy] nvarchar(max) NULL,
                        [ModifiedOn] datetime2 NULL,
                        [ModifiedBy] nvarchar(max) NULL,
                        [Active] bit NULL CONSTRAINT [DF_Fishes_Active] DEFAULT (1),
                        CONSTRAINT [PK_Fishes] PRIMARY KEY ([Id])
                    );

                    CREATE INDEX [IX_Fishes_Code] ON [dbo].[Fishes] ([Code]);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Fishes");
        }
    }
}
