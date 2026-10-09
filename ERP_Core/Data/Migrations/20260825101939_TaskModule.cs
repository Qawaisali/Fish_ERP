using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class TaskModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- CTickets Column Alterations & Additions (Idempotent)
                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CTickets]') AND name = 'Type')
                    ALTER TABLE [dbo].[CTickets] ALTER COLUMN [Type] NVARCHAR(50) NULL;

                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CTickets]') AND name = 'Subject')
                    ALTER TABLE [dbo].[CTickets] ALTER COLUMN [Subject] NVARCHAR(500) NOT NULL;

                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CTickets]') AND name = 'Status')
                    ALTER TABLE [dbo].[CTickets] ALTER COLUMN [Status] NVARCHAR(50) NULL;

                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CTickets]') AND name = 'Priority')
                    ALTER TABLE [dbo].[CTickets] ALTER COLUMN [Priority] NVARCHAR(50) NULL;

                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CTickets]') AND name = 'ContactPerson')
                    ALTER TABLE [dbo].[CTickets] ALTER COLUMN [ContactPerson] NVARCHAR(250) NULL;

                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CTickets]') AND name = 'ContactNo')
                    ALTER TABLE [dbo].[CTickets] ALTER COLUMN [ContactNo] NVARCHAR(50) NULL;

                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CTickets]') AND name = 'Code')
                    ALTER TABLE [dbo].[CTickets] ALTER COLUMN [Code] NVARCHAR(100) NOT NULL;

                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CTickets]') AND name = 'CCTo')
                    ALTER TABLE [dbo].[CTickets] ALTER COLUMN [CCTo] NVARCHAR(150) NULL;

                -- Safely handle ReceivedOn
                IF EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID(N'[dbo].[CTickets]') 
                      AND name = N'ReceivedOn' 
                      AND system_type_id = 56
                )
                BEGIN
                    DECLARE @defConstraint sysname;
                    SELECT @defConstraint = d.name
                    FROM sys.default_constraints d
                    INNER JOIN sys.columns c ON d.parent_column_id = c.column_id AND d.parent_object_id = c.object_id
                    WHERE d.parent_object_id = OBJECT_ID(N'[dbo].[CTickets]') AND c.name = N'ReceivedOn';

                    IF @defConstraint IS NOT NULL
                        EXEC(N'ALTER TABLE [dbo].[CTickets] DROP CONSTRAINT [' + @defConstraint + '];');

                    ALTER TABLE [dbo].[CTickets] DROP COLUMN [ReceivedOn];
                    ALTER TABLE [dbo].[CTickets] ADD [ReceivedOn] DATETIME2 NULL;
                END
                ELSE IF NOT EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID(N'[dbo].[CTickets]') AND name = N'ReceivedOn'
                )
                BEGIN
                    ALTER TABLE [dbo].[CTickets] ADD [ReceivedOn] DATETIME2 NULL;
                END;

                -- Safely add ConvertedOn if not exists
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CTickets]') AND name = 'ConvertedOn')
                    ALTER TABLE [dbo].[CTickets] ADD [ConvertedOn] DATETIME2 NULL;

                -- Safely add DueOn if not exists
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CTickets]') AND name = 'DueOn')
                    ALTER TABLE [dbo].[CTickets] ADD [DueOn] DATETIME2 NULL;

                -- Safely add TimeTake if not exists
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CTickets]') AND name = 'TimeTake')
                    ALTER TABLE [dbo].[CTickets] ADD [TimeTake] FLOAT NULL;

                -- Safely add isConverted if not exists
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CTickets]') AND name = 'isConverted')
                    ALTER TABLE [dbo].[CTickets] ADD [isConverted] BIT NOT NULL DEFAULT 0;
            ");

            migrationBuilder.CreateTable(
                name: "CTaskAnalysis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    TaskSubject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TaskDesctiption = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedWorkingOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TimeTaken = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CTaskAnalysis", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CTaskAssignees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    EmpId = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CTaskAssignees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    ContactPerson = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ContactNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReceivedOn = table.Column<int>(type: "int", nullable: true),
                    AssignToId = table.Column<int>(type: "int", nullable: true),
                    AssignedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CCTo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartedWorkingOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TimeTaken = table.Column<double>(type: "float", nullable: true),
                    isTicket = table.Column<bool>(type: "bit", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: true),
                    FinancialYearId = table.Column<int>(type: "int", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CTasks", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CTaskAnalysis");

            migrationBuilder.DropTable(
                name: "CTaskAssignees");

            migrationBuilder.DropTable(
                name: "CTasks");

            migrationBuilder.DropColumn(
                name: "ConvertedOn",
                table: "CTickets");

            migrationBuilder.DropColumn(
                name: "DueOn",
                table: "CTickets");

            migrationBuilder.DropColumn(
                name: "TimeTake",
                table: "CTickets");

            migrationBuilder.DropColumn(
                name: "isConverted",
                table: "CTickets");

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "CTickets",
                type: "int",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Subject",
                table: "CTickets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "CTickets",
                type: "int",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ReceivedOn",
                table: "CTickets",
                type: "int",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Priority",
                table: "CTickets",
                type: "int",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "CTickets",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContactPerson",
                table: "CTickets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContactNo",
                table: "CTickets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "CTickets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "CCTo",
                table: "CTickets",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldNullable: true);
        }
    }
}
