using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DECLARE @df sysname;

                SET @df = NULL;
                SELECT @df = [d].[name] FROM [sys].[default_constraints] [d]
                INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
                WHERE [d].[parent_object_id] = OBJECT_ID(N'dbo.CTickets') AND [c].[name] = N'ConvertedOn';
                IF @df IS NOT NULL EXEC(N'ALTER TABLE dbo.CTickets DROP CONSTRAINT [' + @df + '];');
                IF COL_LENGTH('dbo.CTickets', 'ConvertedOn') IS NOT NULL ALTER TABLE dbo.CTickets DROP COLUMN ConvertedOn;

                SET @df = NULL;
                SELECT @df = [d].[name] FROM [sys].[default_constraints] [d]
                INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
                WHERE [d].[parent_object_id] = OBJECT_ID(N'dbo.CTickets') AND [c].[name] = N'isConverted';
                IF @df IS NOT NULL EXEC(N'ALTER TABLE dbo.CTickets DROP CONSTRAINT [' + @df + '];');
                IF COL_LENGTH('dbo.CTickets', 'isConverted') IS NOT NULL ALTER TABLE dbo.CTickets DROP COLUMN isConverted;

                SET @df = NULL;
                SELECT @df = [d].[name] FROM [sys].[default_constraints] [d]
                INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
                WHERE [d].[parent_object_id] = OBJECT_ID(N'dbo.CTasks') AND [c].[name] = N'TicketId';
                IF @df IS NOT NULL EXEC(N'ALTER TABLE dbo.CTasks DROP CONSTRAINT [' + @df + '];');
                IF COL_LENGTH('dbo.CTasks', 'TicketId') IS NOT NULL ALTER TABLE dbo.CTasks DROP COLUMN TicketId;

                SET @df = NULL;
                SELECT @df = [d].[name] FROM [sys].[default_constraints] [d]
                INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
                WHERE [d].[parent_object_id] = OBJECT_ID(N'dbo.CTasks') AND [c].[name] = N'isTicket';
                IF @df IS NOT NULL EXEC(N'ALTER TABLE dbo.CTasks DROP CONSTRAINT [' + @df + '];');
                IF COL_LENGTH('dbo.CTasks', 'isTicket') IS NOT NULL ALTER TABLE dbo.CTasks DROP COLUMN isTicket;
            ");

            migrationBuilder.RenameColumn(
                name: "TaskAnalysisId",
                table: "CTaskTimes",
                newName: "EntityId");

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                table: "CTaskTimes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DueOn",
                table: "CTasks",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "CDepartments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CDepartments", x => x.Id);
                });


            migrationBuilder.CreateTable(
                name: "CRoutineAnalysis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RoutineTaskId = table.Column<int>(type: "int", nullable: false),
                    EmpId = table.Column<int>(type: "int", nullable: false),
                    TaskSubject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TaskDesctiption = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartedWorkingOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TimeTaken = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyId = table.Column<int>(type: "int", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRoutineAnalysis", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CTaskPointTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PriorityIdUsed = table.Column<int>(type: "int", nullable: false),
                    BasePointsUsed = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PerHourPlusRateUsed = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PerHourMinusRateUsed = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DueDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EventDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BusinessHoursDiff = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PointsAwarded = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionType = table.Column<int>(type: "int", nullable: false),
                    ParentTransactionId = table.Column<int>(type: "int", nullable: true),
                    GraceDeadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CalculatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CTaskPointTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CTaskPointTransactions_CPriorityPoints_PriorityIdUsed",
                        column: x => x.PriorityIdUsed,
                        principalTable: "CPriorityPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CTaskPointTransactions_CTaskPointTransactions_ParentTransactionId",
                        column: x => x.ParentTransactionId,
                        principalTable: "CTaskPointTransactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CTicketAnalysis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    EmpId = table.Column<int>(type: "int", nullable: false),
                    TaskSubject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TaskDesctiption = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartedWorkingOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TimeTaken = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyId = table.Column<int>(type: "int", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CTicketAnalysis", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CTaskPointTransactions_ParentTransactionId",
                table: "CTaskPointTransactions",
                column: "ParentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_CTaskPointTransactions_PriorityIdUsed",
                table: "CTaskPointTransactions",
                column: "PriorityIdUsed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CDepartments");

            migrationBuilder.DropTable(
                name: "CEvaluationMetricDetails");

            migrationBuilder.DropTable(
                name: "CRoutineAnalysis");

            migrationBuilder.DropTable(
                name: "CTaskPointTransactions");

            migrationBuilder.DropTable(
                name: "CTicketAnalysis");

            migrationBuilder.DropTable(
                name: "CEvaluationMetrics");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "CTaskTimes");

            migrationBuilder.DropColumn(
                name: "DueOn",
                table: "CTasks");

            migrationBuilder.RenameColumn(
                name: "EntityId",
                table: "CTaskTimes",
                newName: "TaskAnalysisId");

            migrationBuilder.AddColumn<DateTime>(
                name: "ConvertedOn",
                table: "CTickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "isConverted",
                table: "CTickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TicketId",
                table: "CTasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "isTicket",
                table: "CTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
