using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Core.Data.Migrations
{
    public partial class AddBillMasterAndDetailTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BillMasters')
                BEGIN
                    CREATE TABLE [dbo].[BillMasters] (
                        [Id] int IDENTITY(1,1) NOT NULL,
                        [BillNo] varchar(50) NOT NULL,
                        [BillDate] datetime2 NOT NULL,
                        [SupplierId] int NULL,
                        [CustomerId] int NULL,
                        [CommissionPercent] decimal(18,2) NOT NULL DEFAULT 0,
                        [CommissionAmount] decimal(18,2) NOT NULL DEFAULT 0,
                        [LabourExpense] decimal(18,2) NOT NULL DEFAULT 0,
                        [LoadingExpense] decimal(18,2) NOT NULL DEFAULT 0,
                        [OtherExpense] decimal(18,2) NOT NULL DEFAULT 0,
                        [TotalExpense] decimal(18,2) NOT NULL DEFAULT 0,
                        [TotalGrossAmount] decimal(18,2) NOT NULL DEFAULT 0,
                        [NetPayable] decimal(18,2) NOT NULL DEFAULT 0,
                        [Status] varchar(20) NOT NULL DEFAULT 'Due',
                        [Remarks] nvarchar(500) NULL,
                        [CompanyId] int NULL,
                        [CreatedOn] datetime2 NULL,
                        [CreatedBy] nvarchar(max) NULL,
                        [ModifiedOn] datetime2 NULL,
                        [ModifiedBy] nvarchar(max) NULL,
                        [Active] bit NULL,
                        CONSTRAINT [PK_BillMasters] PRIMARY KEY CLUSTERED ([Id] ASC),
                        CONSTRAINT [FK_BillMasters_Vendors_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [dbo].[Vendors] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_BillMasters_Vendors_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Vendors] ([Id]) ON DELETE NO ACTION
                    );

                    CREATE INDEX [IX_BillMasters_SupplierId] ON [dbo].[BillMasters] ([SupplierId]);
                    CREATE INDEX [IX_BillMasters_CustomerId] ON [dbo].[BillMasters] ([CustomerId]);
                    CREATE UNIQUE INDEX [IX_BillMasters_BillNo] ON [dbo].[BillMasters] ([BillNo]);
                END

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BillDetails')
                BEGIN
                    CREATE TABLE [dbo].[BillDetails] (
                        [Id] int IDENTITY(1,1) NOT NULL,
                        [BillId] int NOT NULL,
                        [LotNo] varchar(50) NULL,
                        [FishId] int NULL,
                        [SupplierId] int NULL,
                        [CustomerId] int NULL,
                        [Crates] int NULL,
                        [WeightKg] decimal(18,2) NOT NULL DEFAULT 0,
                        [RatePerKg] decimal(18,2) NOT NULL DEFAULT 0,
                        [Amount] decimal(18,2) NOT NULL DEFAULT 0,
                        [Remarks] nvarchar(250) NULL,
                        [CreatedOn] datetime2 NULL,
                        [CreatedBy] nvarchar(max) NULL,
                        [ModifiedOn] datetime2 NULL,
                        [ModifiedBy] nvarchar(max) NULL,
                        [Active] bit NULL,
                        CONSTRAINT [PK_BillDetails] PRIMARY KEY CLUSTERED ([Id] ASC),
                        CONSTRAINT [FK_BillDetails_BillMasters_BillId] FOREIGN KEY ([BillId]) REFERENCES [dbo].[BillMasters] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_BillDetails_Fishes_FishId] FOREIGN KEY ([FishId]) REFERENCES [dbo].[Fishes] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_BillDetails_Vendors_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [dbo].[Vendors] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_BillDetails_Vendors_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Vendors] ([Id]) ON DELETE NO ACTION
                    );

                    CREATE INDEX [IX_BillDetails_BillId] ON [dbo].[BillDetails] ([BillId]);
                    CREATE INDEX [IX_BillDetails_FishId] ON [dbo].[BillDetails] ([FishId]);
                    CREATE INDEX [IX_BillDetails_SupplierId] ON [dbo].[BillDetails] ([SupplierId]);
                    CREATE INDEX [IX_BillDetails_CustomerId] ON [dbo].[BillDetails] ([CustomerId]);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BillDetails");
            migrationBuilder.DropTable(name: "BillMasters");
        }
    }
}
