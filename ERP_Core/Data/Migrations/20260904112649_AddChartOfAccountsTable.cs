using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChartOfAccountsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure Primary Key on NatureofAccount if not already present
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID('NatureofAccount') AND type = 'PK')
                BEGIN
                    ALTER TABLE [dbo].[NatureofAccount] ADD CONSTRAINT [PK_NatureofAccount] PRIMARY KEY ([Id]);
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChartOfAccounts')
                BEGIN
                    CREATE TABLE [dbo].[ChartOfAccounts] (
                        [Id] int NOT NULL IDENTITY,
                        [AccountCode] varchar(50) NULL,
                        [AccountName] nvarchar(200) NOT NULL,
                        [ParentAccountId] int NULL,
                        [NatureOfAccountId] int NULL,
                        [Currency] varchar(10) NULL,
                        [Transable] bit NOT NULL CONSTRAINT [DF_ChartOfAccounts_Transable] DEFAULT (1),
                        [CompanyId] int NULL,
                        [CreatedOn] datetime2 NULL,
                        [CreatedBy] nvarchar(max) NULL,
                        [ModifiedOn] datetime2 NULL,
                        [ModifiedBy] nvarchar(max) NULL,
                        [Active] bit NULL CONSTRAINT [DF_ChartOfAccounts_Active] DEFAULT (1),
                        CONSTRAINT [PK_ChartOfAccounts] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_ChartOfAccounts_ChartOfAccounts_ParentAccountId] FOREIGN KEY ([ParentAccountId]) REFERENCES [dbo].[ChartOfAccounts] ([Id]),
                        CONSTRAINT [FK_ChartOfAccounts_NatureofAccount_NatureOfAccountId] FOREIGN KEY ([NatureOfAccountId]) REFERENCES [dbo].[NatureofAccount] ([Id])
                    );

                    CREATE INDEX [IX_ChartOfAccounts_NatureOfAccountId] ON [dbo].[ChartOfAccounts] ([NatureOfAccountId]);
                    CREATE INDEX [IX_ChartOfAccounts_ParentAccountId] ON [dbo].[ChartOfAccounts] ([ParentAccountId]);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartOfAccounts");
        }
    }
}
