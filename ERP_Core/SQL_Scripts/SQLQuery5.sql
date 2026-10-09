
CREATE PROCEDURE [dbo].[sp_RoutineAnalysisInsert]
    @Id INT OUTPUT,
    @RoutineTaskId INT,
    @EmpId INT,
    @Code NVARCHAR(50) = NULL,
    @TaskSubject NVARCHAR(500),
    @TaskDesctiption NVARCHAR(MAX) = NULL,
    @StartedWorkingOn DATETIME2 = NULL,
    @ClosedOn DATETIME2 = NULL,
    @TimeTaken DECIMAL(18,2) = NULL,
    @Type NVARCHAR(50) = 'Routine',
    @Status NVARCHAR(50) = 'To Do',
    @CompanyId INT = NULL,
    @Active BIT = 1,
    @CreatedOn DATETIME2 = NULL,
    @CreatedBy NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Code IS NULL OR LTRIM(RTRIM(@Code)) = ''
    BEGIN
        DECLARE @NextNum INT;
        SELECT @NextNum = ISNULL(MAX(Id), 0) + 1 FROM [dbo].[CRoutineAnalysis];
        SET @Code = 'RAN-' + RIGHT('0000' + CAST(@NextNum AS VARCHAR(10)), 4);
    END

    INSERT INTO [dbo].[CRoutineAnalysis] (
        [RoutineTaskId],
        [EmpId],
        [Code],
        [TaskSubject],
        [TaskDesctiption],
        [StartedWorkingOn],
        [ClosedOn],
        [TimeTaken],
        [Type],
        [Status],
        [CompanyId],
        [Active],
        [CreatedOn],
        [CreatedBy]
    ) VALUES (
        @RoutineTaskId,
        @EmpId,
        @Code,
        @TaskSubject,
        @TaskDesctiption,
        @StartedWorkingOn,
        @ClosedOn,
        @TimeTaken,
        COALESCE(@Type, 'Routine'),
        COALESCE(@Status, 'To Do'),
        @CompanyId,
        COALESCE(@Active, 1),
        COALESCE(@CreatedOn, GETDATE()),
        @CreatedBy
    );

    SET @Id = CAST(SCOPE_IDENTITY() AS INT);
END
GO


CREATE PROCEDURE [dbo].[sp_RoutineAnalysisUpdate]
    @Id INT,
    @EmpId INT = NULL,
    @TaskSubject NVARCHAR(500),
    @TaskDesctiption NVARCHAR(MAX) = NULL,
    @StartedWorkingOn DATETIME2 = NULL,
    @ClosedOn DATETIME2 = NULL,
    @TimeTaken DECIMAL(18,2) = NULL,
    @Type NVARCHAR(50) = 'Routine',
    @Status NVARCHAR(50) = 'To Do',
    @Active BIT = 1,
    @ModifiedOn DATETIME2 = NULL,
    @ModifiedBy NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Status IN ('Done', 'Completed', 'Resolved', 'Closed') AND @ClosedOn IS NULL
    BEGIN
        SET @ClosedOn = GETDATE();
    END

    UPDATE [dbo].[CRoutineAnalysis]
    SET
        [EmpId] = COALESCE(@EmpId, [EmpId]),
        [TaskSubject] = @TaskSubject,
        [TaskDesctiption] = @TaskDesctiption,
        [StartedWorkingOn] = COALESCE(@StartedWorkingOn, [StartedWorkingOn]),
        [ClosedOn] = @ClosedOn,
        [TimeTaken] = COALESCE(@TimeTaken, [TimeTaken]),
        [Type] = COALESCE(@Type, [Type]),
        [Status] = COALESCE(@Status, [Status]),
        [Active] = COALESCE(@Active, [Active]),
        [ModifiedOn] = COALESCE(@ModifiedOn, GETDATE()),
        [ModifiedBy] = @ModifiedBy
    WHERE [Id] = @Id;
END
GO


CREATE PROCEDURE [dbo].[sp_RoutineAnalysisDelete]
    @Id INT,
    @ModifiedBy NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[CRoutineAnalysis]
    SET [Active] = 0,
        [ModifiedOn] = GETDATE(),
        [ModifiedBy] = @ModifiedBy
    WHERE [Id] = @Id;
END
GO


CREATE PROCEDURE [dbo].[sp_RoutineAnalysisGetByRoutineTaskId]
    @RoutineTaskId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        a.[Id],
        a.[RoutineTaskId],
        a.[EmpId],
        a.[Code],
        a.[TaskSubject],
        a.[TaskDesctiption],
        a.[StartedWorkingOn],
        a.[ClosedOn],
        a.[TimeTaken],
        a.[Type],
        a.[Status],
        a.[CompanyId],
        a.[Active],
        a.[CreatedOn],
        a.[CreatedBy],
        a.[ModifiedOn],
        a.[ModifiedBy],
        rt.[TaskDetails] AS [ParentRoutineTaskDetails],
        rd.[RoutineType] AS [ParentRoutineType],
        u.[Name] AS [AssignedEmployeeName]
    FROM [dbo].[CRoutineAnalysis] a
    INNER JOIN [dbo].[CRoutineTasks] rt ON a.[RoutineTaskId] = rt.[Id]
    LEFT JOIN [dbo].[CRoutineDetails] rd ON rt.[RoutineDetailId] = rd.[Id]
    LEFT JOIN [dbo].[AspNetUsers] u ON a.[EmpId] = u.[EmpId]
    WHERE a.[RoutineTaskId] = @RoutineTaskId
      AND (a.[Active] IS NULL OR a.[Active] = 1)
    ORDER BY a.[Id] ASC;
END
GO


CREATE PROCEDURE [dbo].[sp_RoutineAnalysisGetByEmployeeId]
    @EmpId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        a.[Id],
        a.[RoutineTaskId],
        a.[EmpId],
        a.[Code],
        a.[TaskSubject],
        a.[TaskDesctiption],
        a.[StartedWorkingOn],
        a.[ClosedOn],
        a.[TimeTaken],
        a.[Type],
        a.[Status],
        a.[CompanyId],
        a.[Active],
        a.[CreatedOn],
        a.[CreatedBy],
        a.[ModifiedOn],
        a.[ModifiedBy],
        rt.[TaskDetails] AS [ParentRoutineTaskDetails],
        rd.[RoutineType] AS [ParentRoutineType],
        u.[Name] AS [AssignedEmployeeName]
    FROM [dbo].[CRoutineAnalysis] a
    INNER JOIN [dbo].[CRoutineTasks] rt ON a.[RoutineTaskId] = rt.[Id]
    LEFT JOIN [dbo].[CRoutineDetails] rd ON rt.[RoutineDetailId] = rd.[Id]
    LEFT JOIN [dbo].[AspNetUsers] u ON a.[EmpId] = u.[EmpId]
    WHERE a.[EmpId] = @EmpId
      AND (a.[Active] IS NULL OR a.[Active] = 1)
      AND (rt.[Active] IS NULL OR rt.[Active] = 1)
    ORDER BY a.[Id] DESC;
END
GO


CREATE PROCEDURE [dbo].[sp_RoutineAnalysisGetById]
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        a.[Id],
        a.[RoutineTaskId],
        a.[EmpId],
        a.[Code],
        a.[TaskSubject],
        a.[TaskDesctiption],
        a.[StartedWorkingOn],
        a.[ClosedOn],
        a.[TimeTaken],
        a.[Type],
        a.[Status],
        a.[CompanyId],
        a.[Active],
        a.[CreatedOn],
        a.[CreatedBy],
        a.[ModifiedOn],
        a.[ModifiedBy],
        rt.[TaskDetails] AS [ParentRoutineTaskDetails],
        rd.[RoutineType] AS [ParentRoutineType],
        u.[Name] AS [AssignedEmployeeName]
    FROM [dbo].[CRoutineAnalysis] a
    INNER JOIN [dbo].[CRoutineTasks] rt ON a.[RoutineTaskId] = rt.[Id]
    LEFT JOIN [dbo].[CRoutineDetails] rd ON rt.[RoutineDetailId] = rd.[Id]
    LEFT JOIN [dbo].[AspNetUsers] u ON a.[EmpId] = u.[EmpId]
    WHERE a.[Id] = @Id;
END
GO

CREATE PROCEDURE [dbo].[sp_RoutineAnalysisUpdateStatus]
    @Id INT,
    @Status NVARCHAR(50),
    @EmpId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @RoutineTaskId INT;
    DECLARE @ClosedOn DATETIME2 = NULL;
    DECLARE @StartedWorkingOn DATETIME2 = NULL;

    SELECT 
        @RoutineTaskId = [RoutineTaskId],
        @StartedWorkingOn = [StartedWorkingOn]
    FROM [dbo].[CRoutineAnalysis] 
    WHERE [Id] = @Id;

    IF @Status IN ('In Progress', 'InProgress', 'Review', 'Under Review')
    BEGIN
        IF @StartedWorkingOn IS NULL
        BEGIN
            SET @StartedWorkingOn = GETDATE();
        END

        UPDATE [dbo].[CRoutineAnalysis]
        SET 
            [Status] = 'On Hold',
            [ModifiedOn] = GETDATE(),
            [ModifiedBy] = CAST(@EmpId AS NVARCHAR(50))
        WHERE [Id] <> @Id
          AND [Status] IN ('In Progress', 'InProgress', 'Review', 'Under Review')
          AND ([EmpId] = @EmpId OR @EmpId IS NULL);

        IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CTaskAnalysis')
        BEGIN
            UPDATE [dbo].[CTaskAnalysis]
            SET 
                [Status] = 'On Hold',
                [ModifiedOn] = GETDATE(),
                [ModifiedBy] = CAST(@EmpId AS NVARCHAR(50))
            WHERE [Status] IN ('In Progress', 'InProgress', 'Review', 'Under Review')
              AND ([EmpId] = @EmpId OR @EmpId IS NULL);
        END

        IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CTicketAnalysis')
        BEGIN
            UPDATE [dbo].[CTicketAnalysis]
            SET 
                [Status] = 'On Hold',
                [ModifiedOn] = GETDATE(),
                [ModifiedBy] = CAST(@EmpId AS NVARCHAR(50))
            WHERE [Status] IN ('In Progress', 'InProgress', 'Review', 'Under Review')
              AND ([EmpId] = @EmpId OR @EmpId IS NULL);
        END
    END

    IF @Status IN ('Done', 'Completed', 'Resolved', 'Closed')
    BEGIN
        SET @ClosedOn = GETDATE();

        IF @RoutineTaskId IS NOT NULL
        BEGIN
            UPDATE [dbo].[CRoutineTasks]
            SET [Status] = 'Completed',
                [CompletedAt] = GETDATE(),
                [ModifiedOn] = GETDATE(),
                [ModifiedBy] = CAST(@EmpId AS NVARCHAR(50))
            WHERE [Id] = @RoutineTaskId;
        END
    END

    UPDATE [dbo].[CRoutineAnalysis]
    SET 
        [Status] = @Status,
        [StartedWorkingOn] = COALESCE(@StartedWorkingOn, [StartedWorkingOn]),
        [ClosedOn] = @ClosedOn,
        [ModifiedOn] = GETDATE(),
        [ModifiedBy] = CAST(@EmpId AS NVARCHAR(50))
    WHERE [Id] = @Id;
END
GO


/* =========================================================================
   usp_GetTaskTimeReport
   Serves BOTH the day report and the monthly report.

   Hierarchy:  Project -> Task/Ticket/Routine -> Analysis -> CTaskTimes
   Order:      chronological (morning -> evening) within each Analysis

   Usage:
     -- Day report
     EXEC dbo.usp_GetTaskTimeReport @StartDate = '2026-08-30';

     -- Month report (pass both dates; range is inclusive of both ends)
     EXEC dbo.usp_GetTaskTimeReport @StartDate = '2026-08-01', @EndDate = '2026-08-31';

   Returns TWO result sets:
     1) Detail rows, in hierarchical order (Project -> Task/Ticket/Routine
        -> Analysis -> chronological time entries). This is what the day
        report is built from.
     2) Subtotal rollup: total hours per Project, per Project+Task, and per
        Project+Task+Analysis. This is what the monthly report's summary
        lines come from — pair it with result set 1 if you also want full
        daily detail for the month.
   ========================================================================= */

CREATE PROCEDURE dbo.usp_GetTaskTimeReport
    @StartDate DATE,
    @EndDate   DATE = NULL      -- NULL => single-day report (defaults to @StartDate)
AS
BEGIN
    SET NOCOUNT ON;

    IF @EndDate IS NULL
        SET @EndDate = @StartDate;

    DECLARE @RangeStart DATETIME2 = CAST(@StartDate AS DATETIME2);
    DECLARE @RangeEnd   DATETIME2 = DATEADD(DAY, 1, CAST(@EndDate AS DATETIME2)); -- exclusive upper bound

    IF OBJECT_ID('tempdb..#Report') IS NOT NULL
        DROP TABLE #Report;

    -- ---------- TASK branch ----------
    SELECT
        ISNULL(p.ProjectName, 'Routines')  AS ProjectName,
        CAST('Task' AS VARCHAR(10))         AS EntryType,
        t.Id                                AS TopId,
        t.Code                              AS TopCode,
        t.Subject                           AS TopSubject,
        a.Id                                AS AnalysisId,
        a.TaskSubject                       AS AnalysisSubject,
        a.Status                            AS AnalysisStatus,
        tt.Id                               AS TimeId,
        tt.StartTime,
        tt.EndTime,
        tt.WorkingHours,
        tt.CreatedBy                        AS EmpId
    INTO #Report
    FROM CTaskTimes tt
    JOIN CTaskAnalysis a  ON a.Id = tt.EntityId AND tt.EntityType = 'Task'
    JOIN CTasks t         ON t.Id = a.TaskId
    LEFT JOIN CProjects p ON p.Id = t.ProjectId
    WHERE tt.StartTime >= @RangeStart AND tt.StartTime < @RangeEnd;

    -- ---------- TICKET branch ----------
    INSERT INTO #Report
    SELECT
        ISNULL(p.ProjectName, 'Routines'),
        'Ticket',
        tk.Id,
        tk.Code,
        tk.Subject,
        a.Id,
        a.TaskSubject,
        a.Status,
        tt.Id,
        tt.StartTime,
        tt.EndTime,
        tt.WorkingHours,
        tt.CreatedBy
    FROM CTaskTimes tt
    JOIN CTicketAnalysis a ON a.Id = tt.EntityId AND tt.EntityType = 'Ticket'
    JOIN CTickets tk       ON tk.Id = a.TicketId
    LEFT JOIN CProjects p  ON p.Id = tk.ProjectId
    WHERE tt.StartTime >= @RangeStart AND tt.StartTime < @RangeEnd;

    -- ---------- ROUTINE branch (never has a project) ----------
    INSERT INTO #Report
    SELECT
        'Routines',
        'Routine',
        rt.Id,
        CAST(rt.Id AS NVARCHAR(50)),
        rt.TaskDetails,
        ra.Id,
        ra.TaskSubject,
        ra.Status,
        tt.Id,
        tt.StartTime,
        tt.EndTime,
        tt.WorkingHours,
        tt.CreatedBy
    FROM CTaskTimes tt
    JOIN CRoutineAnalysis ra ON ra.Id = tt.EntityId AND tt.EntityType = 'Routine'
    JOIN CRoutineTasks rt    ON rt.Id = ra.RoutineTaskId
    WHERE tt.StartTime >= @RangeStart AND tt.StartTime < @RangeEnd;

    -- ---------- Result set 1: detail rows, hierarchical order ----------
    SELECT *
    FROM #Report
    ORDER BY
        CASE WHEN ProjectName = 'Routines' THEN 1 ELSE 0 END,  -- Routines section last (optional)
        ProjectName,
        EntryType,
        TopId,
        AnalysisId,
        StartTime ASC;                                          -- morning -> evening

    -- ---------- Result set 2: subtotal rollup (Project / Task / Analysis) ----------
    SELECT
        ProjectName,
        TopSubject,
        AnalysisSubject,
        SUM(WorkingHours) AS TotalHours
    FROM #Report
    GROUP BY GROUPING SETS (
        (ProjectName),
        (ProjectName, TopSubject),
        (ProjectName, TopSubject, AnalysisSubject)
    )
    ORDER BY ProjectName, TopSubject, AnalysisSubject;

    DROP TABLE #Report;
END
GO

exec usp_GetTaskTimeReport '2026-08-28 00:00:00',  '2026-08-31 00:00:00'

select * from CRoles

update CRoles set RoleName = 'Admin' where Id = 3 


select * from CTaskTimes

select * from CTaskAnalysis


select * from CTasks

delete 


select * from CRoutineTasks
select * from CRoutineAnalysis


create procedure sp_GetTasks
