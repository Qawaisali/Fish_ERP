using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Core.Data.Migrations
{
    public partial class AddCategoryToFishMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT * FROM sys.columns 
                    WHERE object_id = OBJECT_ID(N'[dbo].[Fishes]') AND name = 'CategoryId'
                )
                BEGIN
                    ALTER TABLE [dbo].[Fishes] ADD [CategoryId] int NULL;

                    ALTER TABLE [dbo].[Fishes] ADD CONSTRAINT [FK_Fishes_ProductCategories_CategoryId]
                    FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[ProductCategories] ([Id]) ON DELETE NO ACTION;

                    CREATE INDEX [IX_Fishes_CategoryId] ON [dbo].[Fishes] ([CategoryId]);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Fishes_ProductCategories_CategoryId",
                table: "Fishes");

            migrationBuilder.DropIndex(
                name: "IX_Fishes_CategoryId",
                table: "Fishes");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Fishes");
        }
    }
}
