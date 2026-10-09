using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Core.Data.Migrations
{
    public partial class AddProductCategoryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProductCategories')
                BEGIN
                    CREATE TABLE [dbo].[ProductCategories] (
                        [Id] int NOT NULL IDENTITY,
                        [Code] varchar(50) NULL,
                        [Name] nvarchar(150) NOT NULL,
                        [PurchaseAccountId] int NULL,
                        [SaleAccountId] int NULL,
                        [PurchaseReturnAccountId] int NULL,
                        [SaleReturnAccountId] int NULL,
                        [CompanyId] int NULL,
                        [CreatedOn] datetime2 NULL,
                        [CreatedBy] nvarchar(max) NULL,
                        [ModifiedOn] datetime2 NULL,
                        [ModifiedBy] nvarchar(max) NULL,
                        [Active] bit NULL CONSTRAINT [DF_ProductCategories_Active] DEFAULT (1),
                        CONSTRAINT [PK_ProductCategories] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_ProductCategories_ChartOfAccounts_PurchaseAccountId] FOREIGN KEY ([PurchaseAccountId]) REFERENCES [dbo].[ChartOfAccounts] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_ProductCategories_ChartOfAccounts_SaleAccountId] FOREIGN KEY ([SaleAccountId]) REFERENCES [dbo].[ChartOfAccounts] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_ProductCategories_ChartOfAccounts_PurchaseReturnAccountId] FOREIGN KEY ([PurchaseReturnAccountId]) REFERENCES [dbo].[ChartOfAccounts] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_ProductCategories_ChartOfAccounts_SaleReturnAccountId] FOREIGN KEY ([SaleReturnAccountId]) REFERENCES [dbo].[ChartOfAccounts] ([Id]) ON DELETE NO ACTION
                    );

                    CREATE INDEX [IX_ProductCategories_Code] ON [dbo].[ProductCategories] ([Code]);
                    CREATE INDEX [IX_ProductCategories_PurchaseAccountId] ON [dbo].[ProductCategories] ([PurchaseAccountId]);
                    CREATE INDEX [IX_ProductCategories_SaleAccountId] ON [dbo].[ProductCategories] ([SaleAccountId]);
                    CREATE INDEX [IX_ProductCategories_PurchaseReturnAccountId] ON [dbo].[ProductCategories] ([PurchaseReturnAccountId]);
                    CREATE INDEX [IX_ProductCategories_SaleReturnAccountId] ON [dbo].[ProductCategories] ([SaleReturnAccountId]);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductCategories");
        }
    }
}
