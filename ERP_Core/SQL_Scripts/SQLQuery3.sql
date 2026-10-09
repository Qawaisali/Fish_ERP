
-- ==========================================================================================
-- SQL SCRIPT: Updated Stored Procedures for CTaskTimes Polymorphic Entity Tracking
-- Supports both 'Task' (CTaskAnalysis) and 'Ticket' (CTicketAnalysis)
-- ==========================================================================================

select * from CTaskTimes

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_TaskTimeStartSession')
    DROP PROCEDURE [dbo].[sp_TaskTimeStartSession]
GO
CREATE PROCEDURE [dbo].[sp_TaskTimeStartSession]
    @EntityType NVARCHAR(50) = 'Task',   -- 'Task' or 'Ticket'
    @EntityId INT,                       -- Id of CTaskAnalysis or CTicketAnalysis
    @UserIdentifier NVARCHAR(MAX) = NULL,
    @Status NVARCHAR(50) = 'In Progress',
    @CurrentTime DATETIME2 = NULL,
    @MinSessionSeconds INT = 20          -- Discard accidental micro-clicks under 20 seconds
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Now DATETIME2 = COALESCE(@CurrentTime, GETDATE());
    DECLARE @EmpId INT = TRY_CAST(@UserIdentifier AS INT);

    -- Standardize Status string ('In Progress' or 'Under Review')
    DECLARE @SessionStatus NVARCHAR(50) = CASE 
        WHEN @Status IN ('Review', 'Under Review', 'UnderReview') THEN 'Under Review'
        ELSE 'In Progress'
    END;

    -- 1. Close ANY currently active open sessions for this user in CTaskTimes
    DECLARE @ActiveSessionCursor CURSOR;
    DECLARE @OpenTimeId INT, @OpenEntityType NVARCHAR(50), @OpenEntityId INT, @OpenStartTime DATETIME2;

    SET @ActiveSessionCursor = CURSOR FOR 
        SELECT tt.[Id], COALESCE(tt.[EntityType], 'Task'), tt.[EntityId], tt.[StartTime]
        FROM [dbo].[CTaskTimes] tt
        WHERE tt.[EndTime] IS NULL 
          AND (tt.[CreatedBy] = @UserIdentifier OR @UserIdentifier IS NULL);

    OPEN @ActiveSessionCursor;
    FETCH NEXT FROM @ActiveSessionCursor INTO @OpenTimeId, @OpenEntityType, @OpenEntityId, @OpenStartTime;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @ElapsedSeconds INT = DATEDIFF(SECOND, @OpenStartTime, @Now);

        IF @ElapsedSeconds < @MinSessionSeconds
        BEGIN
            DELETE FROM [dbo].[CTaskTimes] WHERE [Id] = @OpenTimeId;
        END
        ELSE
        BEGIN
            DECLARE @ElapsedHours DECIMAL(18,2) = ROUND(@ElapsedSeconds / 3600.0, 2);

            -- PRESERVE original working status ('In Progress' or 'Under Review') in CTaskTimes. Do NOT overwrite to 'On Hold'!
            UPDATE [dbo].[CTaskTimes]
            SET 
                [EndTime] = @Now,
                [Status] = COALESCE([Status], 'In Progress'),
                [WorkingHours] = @ElapsedHours
            WHERE [Id] = @OpenTimeId;
        END

        -- Recalculate TimeTaken and set status to On Hold on previous item (if not the target)
        IF @OpenEntityType = 'Ticket'
        BEGIN
            UPDATE [dbo].[CTicketAnalysis]
            SET 
                [Status] = CASE WHEN NOT (@EntityType = 'Ticket' AND [Id] = @OpenEntityId) THEN 'On Hold' ELSE [Status] END,
                [TimeTaken] = (SELECT ISNULL(SUM([WorkingHours]), 0) FROM [dbo].[CTaskTimes] WHERE [EntityType] = 'Ticket' AND [EntityId] = @OpenEntityId),
                [ModifiedOn] = @Now,
                [ModifiedBy] = @UserIdentifier
            WHERE [Id] = @OpenEntityId;
        END
        ELSE
        BEGIN
            UPDATE [dbo].[CTaskAnalysis]
            SET 
                [Status] = CASE WHEN NOT (@EntityType = 'Task' AND [Id] = @OpenEntityId) THEN 'On Hold' ELSE [Status] END,
                [TimeTaken] = (SELECT ISNULL(SUM([WorkingHours]), 0) FROM [dbo].[CTaskTimes] WHERE ([EntityType] = 'Task' OR [EntityType] IS NULL) AND [EntityId] = @OpenEntityId),
                [ModifiedOn] = @Now,
                [ModifiedBy] = @UserIdentifier
            WHERE [Id] = @OpenEntityId;
        END

        FETCH NEXT FROM @ActiveSessionCursor INTO @OpenTimeId, @OpenEntityType, @OpenEntityId, @OpenStartTime;
    END

    CLOSE @ActiveSessionCursor;
    DEALLOCATE @ActiveSessionCursor;

    -- 2. Put all other active CTaskAnalysis for this employee On Hold
    UPDATE [dbo].[CTaskAnalysis]
    SET 
        [Status] = 'On Hold',
        [TimeTaken] = (SELECT ISNULL(SUM([WorkingHours]), 0) FROM [dbo].[CTaskTimes] WHERE ([EntityType] = 'Task' OR [EntityType] IS NULL) AND [EntityId] = [CTaskAnalysis].[Id]),
        [ModifiedOn] = @Now,
        [ModifiedBy] = @UserIdentifier
    WHERE NOT (@EntityType = 'Task' AND [Id] = @EntityId)
      AND [Status] IN ('In Progress', 'InProgress', 'Review', 'Under Review')
      AND (
          [CreatedBy] = @UserIdentifier 
          OR [EmpId] = @EmpId
          OR EXISTS (
              SELECT 1 FROM [dbo].[CTaskAssignees] ta 
              WHERE ta.[TaskId] = [CTaskAnalysis].[TaskId] AND ta.[EmpId] = @EmpId
          )
      );

    -- 3. Put all other active CTicketAnalysis for this employee On Hold
    UPDATE [dbo].[CTicketAnalysis]
    SET 
        [Status] = 'On Hold',
        [TimeTaken] = (SELECT ISNULL(SUM([WorkingHours]), 0) FROM [dbo].[CTaskTimes] WHERE [EntityType] = 'Ticket' AND [EntityId] = [CTicketAnalysis].[Id]),
        [ModifiedOn] = @Now,
        [ModifiedBy] = @UserIdentifier
    WHERE NOT (@EntityType = 'Ticket' AND [Id] = @EntityId)
      AND [Status] IN ('In Progress', 'InProgress', 'Review', 'Under Review')
      AND (
          [CreatedBy] = @UserIdentifier 
          OR [EmpId] = @EmpId
          OR EXISTS (
              SELECT 1 FROM [dbo].[CTickets] tk 
              WHERE tk.[Id] = [CTicketAnalysis].[TicketId] AND tk.[AssignToId] = @EmpId
          )
      );

    -- 4. Open a new active running session in CTaskTimes for the targeted EntityType and EntityId
    -- Only stores 'In Progress' or 'Under Review'
    INSERT INTO [dbo].[CTaskTimes] (
        [EntityType],
        [EntityId],
        [Status],
        [StartTime],
        [EndTime],
        [WorkingHours],
        [CreatedBy],
        [CreatedOn]
    ) VALUES (
        @EntityType,
        @EntityId,
        @SessionStatus,
        @Now,
        NULL,
        0,
        @UserIdentifier,
        @Now
    );

    -- 5. Update the target item status in CTaskAnalysis / CTicketAnalysis
    IF @EntityType = 'Ticket'
    BEGIN
        UPDATE [dbo].[CTicketAnalysis]
        SET 
            [Status] = @SessionStatus,
            [StartedWorkingOn] = COALESCE([StartedWorkingOn], @Now),
            [ModifiedOn] = @Now,
            [ModifiedBy] = @UserIdentifier
        WHERE [Id] = @EntityId;

        -- Rollup Ticket status
        DECLARE @TicketId INT = (SELECT [TicketId] FROM [dbo].[CTicketAnalysis] WHERE [Id] = @EntityId);
        IF @TicketId IS NOT NULL
        BEGIN
            EXEC [dbo].[sp_RecalculateTicketProgressAndStatus] @TicketId = @TicketId;
        END
    END
    ELSE
    BEGIN
        UPDATE [dbo].[CTaskAnalysis]
        SET 
            [Status] = @SessionStatus,
            [StartedWorkingOn] = COALESCE([StartedWorkingOn], @Now),
            [ModifiedOn] = @Now,
            [ModifiedBy] = @UserIdentifier
        WHERE [Id] = @EntityId;

        -- Rollup Task status
        DECLARE @ParentTaskId INT = (SELECT [TaskId] FROM [dbo].[CTaskAnalysis] WHERE [Id] = @EntityId);
        IF @ParentTaskId IS NOT NULL
        BEGIN
            UPDATE [dbo].[CTasks]
            SET 
                [Status] = 'In Progress',
                [ModifiedOn] = @Now,
                [ModifiedBy] = @UserIdentifier
            WHERE [Id] = @ParentTaskId AND [Status] NOT IN ('In Progress', 'Completed', 'Closed', 'Cancelled');
        END
    END
END
GO


-- ==========================================================================================
-- 2. STORED PROCEDURE: sp_TaskTimeStopSession
-- ==========================================================================================
IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_TaskTimeStopSession')
    DROP PROCEDURE [dbo].[sp_TaskTimeStopSession]
GO
CREATE PROCEDURE [dbo].[sp_TaskTimeStopSession]
    @EntityType NVARCHAR(50) = 'Task',   -- 'Task' or 'Ticket'
    @EntityId INT,                       -- Id of CTaskAnalysis or CTicketAnalysis
    @NewStatus NVARCHAR(50) = 'Done',
    @UserIdentifier NVARCHAR(MAX) = NULL,
    @CurrentTime DATETIME2 = NULL,
    @MinSessionSeconds INT = 20          -- Discard accidental micro-clicks under 20 seconds
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Now DATETIME2 = COALESCE(@CurrentTime, GETDATE());

    -- 1. Close open session for this Entity in CTaskTimes
    DECLARE @OpenTimeId INT, @OpenStartTime DATETIME2;
    SELECT TOP 1 @OpenTimeId = [Id], @OpenStartTime = [StartTime]
    FROM [dbo].[CTaskTimes]
    WHERE ([EntityType] = @EntityType OR (@EntityType = 'Task' AND [EntityType] IS NULL))
      AND [EntityId] = @EntityId 
      AND [EndTime] IS NULL
    ORDER BY [Id] DESC;

    IF @OpenTimeId IS NOT NULL
    BEGIN
        DECLARE @ElapsedSeconds INT = DATEDIFF(SECOND, @OpenStartTime, @Now);

        -- ACCIDENTAL CLICK FILTER: If session lasted less than @MinSessionSeconds, delete/discard it!
        IF @ElapsedSeconds < @MinSessionSeconds
        BEGIN
            DELETE FROM [dbo].[CTaskTimes] WHERE [Id] = @OpenTimeId;
        END
        ELSE
        BEGIN
            DECLARE @ElapsedHours DECIMAL(18,2) = ROUND(@ElapsedSeconds / 3600.0, 2);

            -- PRESERVE original working status ('In Progress' or 'Under Review') in CTaskTimes. Do NOT overwrite to 'Done' or 'On Hold'!
            UPDATE [dbo].[CTaskTimes]
            SET
                [EndTime] = @Now,
                [Status] = COALESCE([Status], 'In Progress'),
                [WorkingHours] = @ElapsedHours
            WHERE [Id] = @OpenTimeId;
        END
    END

    -- 2. Update Total TimeTaken and Status on Target Entity (CTaskAnalysis or CTicketAnalysis)
    IF @EntityType = 'Ticket'
    BEGIN
        UPDATE [dbo].[CTicketAnalysis]
        SET
            [Status] = @NewStatus,
            [ClosedOn] = CASE WHEN @NewStatus IN ('Done', 'Completed', 'Resolved', 'Closed') THEN @Now ELSE [ClosedOn] END,
            [TimeTaken] = (SELECT ISNULL(SUM([WorkingHours]), 0) FROM [dbo].[CTaskTimes] WHERE [EntityType] = 'Ticket' AND [EntityId] = @EntityId),
            [ModifiedOn] = @Now,
            [ModifiedBy] = @UserIdentifier
        WHERE [Id] = @EntityId;

        -- Rollup Ticket status
        DECLARE @TicketId INT = (SELECT [TicketId] FROM [dbo].[CTicketAnalysis] WHERE [Id] = @EntityId);
        IF @TicketId IS NOT NULL
        BEGIN
            EXEC [dbo].[sp_RecalculateTicketProgressAndStatus] @TicketId = @TicketId;
        END
    END
    ELSE
    BEGIN
        UPDATE [dbo].[CTaskAnalysis]
        SET
            [Status] = @NewStatus,
            [ClosedOn] = CASE WHEN @NewStatus IN ('Done', 'Completed', 'Resolved') THEN @Now ELSE [ClosedOn] END,
            [TimeTaken] = (SELECT ISNULL(SUM([WorkingHours]), 0) FROM [dbo].[CTaskTimes] WHERE ([EntityType] = 'Task' OR [EntityType] IS NULL) AND [EntityId] = @EntityId),
            [ModifiedOn] = @Now,
            [ModifiedBy] = @UserIdentifier
        WHERE [Id] = @EntityId;

        -- Rollup Task status
        DECLARE @TaskId INT = (SELECT [TaskId] FROM [dbo].[CTaskAnalysis] WHERE [Id] = @EntityId);
        IF @TaskId IS NOT NULL
        BEGIN
            DECLARE @TotalCount INT = 0;
            DECLARE @DoneCount INT = 0;
            DECLARE @InProgressCount INT = 0;

            SELECT
                @TotalCount = COUNT(*),
                @DoneCount = SUM(CASE WHEN [Status] IN ('Done', 'Completed', 'Resolved') THEN 1 ELSE 0 END),
                @InProgressCount = SUM(CASE WHEN [Status] IN ('In Progress', 'InProgress', 'Review', 'Under Review') THEN 1 ELSE 0 END)
            FROM [dbo].[CTaskAnalysis]
            WHERE [TaskId] = @TaskId
              AND ([Active] IS NULL OR [Active] = 1);

            IF @TotalCount > 0 AND @DoneCount = @TotalCount
            BEGIN
                UPDATE [dbo].[CTasks]
                SET
                    [Status] = 'Done',
                    [ClosedOn] = @Now,
                    [ModifiedOn] = @Now,
                    [ModifiedBy] = @UserIdentifier
                WHERE [Id] = @TaskId AND [Status] NOT IN ('Done', 'Completed', 'Closed');
            END
            ELSE IF @InProgressCount > 0 OR (@DoneCount > 0 AND @DoneCount < @TotalCount)
            BEGIN
                UPDATE [dbo].[CTasks]
                SET
                    [Status] = 'In Progress',
                    [ClosedOn] = NULL,
                    [ModifiedOn] = @Now,
                    [ModifiedBy] = @UserIdentifier
                WHERE [Id] = @TaskId AND [Status] <> 'In Progress';
            END
            ELSE IF @TotalCount > 0 AND @DoneCount = 0 AND @InProgressCount = 0
            BEGIN
                UPDATE [dbo].[CTasks]
                SET
                    [Status] = 'To Do',
                    [ClosedOn] = NULL,
                    [ModifiedOn] = @Now,
                    [ModifiedBy] = @UserIdentifier
                WHERE [Id] = @TaskId AND [Status] <> 'To Do';
            END
        END
    END
END
GO


-- ==========================================================================================
-- 3. STORED PROCEDURE: sp_TaskTimesGetByAnalysisId (or sp_TaskTimesGetByEntity)
-- ==========================================================================================
IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_TaskTimesGetByAnalysisId')
    DROP PROCEDURE [dbo].[sp_TaskTimesGetByAnalysisId]
GO
CREATE PROCEDURE [dbo].[sp_TaskTimesGetByAnalysisId]
    @EntityType NVARCHAR(50) = 'Task',
    @EntityId INT = NULL,
    @TaskAnalysisId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Backward compatibility for callers passing only @TaskAnalysisId
    DECLARE @TargetId INT = COALESCE(@EntityId, @TaskAnalysisId);

    SELECT 
        [Id],
        COALESCE([EntityType], 'Task') AS [EntityType],
        [EntityId],
        [Status],
        [StartTime],
        [EndTime],
        [WorkingHours],
        [CreatedBy],
        [CreatedOn]
    FROM [dbo].[CTaskTimes]
    WHERE ([EntityType] = @EntityType OR (@EntityType = 'Task' AND [EntityType] IS NULL))
      AND [EntityId] = @TargetId
    ORDER BY [StartTime] DESC;
END
GO

select * from CTaskTimes

-- 4. STORED PROCEDURE: sp_GetCalendarWorkLogsByMonth -- OLD
IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_GetCalendarWorkLogsByMonth')
    DROP PROCEDURE [dbo].[sp_GetCalendarWorkLogsByMonth]
GO
CREATE PROCEDURE [dbo].[sp_GetCalendarWorkLogsByMonth]
    @Year INT,
    @Month INT,
    @EmpId INT = NULL,
    @UserIdentifier NVARCHAR(MAX) = NULL,
    @CompanyId INT = 0,
    @MinMinutes INT = 1
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        MIN(tt.[Id]) AS [Id],
        COALESCE(tt.[EntityType], 'Task') AS [EntityType],
        tt.[EntityId],
        COALESCE(a.[TaskSubject], ta.[TaskSubject], 'Work Session') AS [Title],
        COALESCE(a.[Code], ta.[Code], CASE WHEN tt.[EntityType] = 'Ticket' THEN 'TAN-' + CAST(tt.[EntityId] AS NVARCHAR(20)) ELSE 'ANL-' + CAST(tt.[EntityId] AS NVARCHAR(20)) END) AS [TaskCode],
        COALESCE(p1.[ProjectName], p2.[ProjectName], 'General') AS [ProjectName],
        CAST(tt.[StartTime] AS DATE) AS [DueDate],
        MIN(tt.[StartTime]) AS [StartTime],
        MAX(ISNULL(tt.[EndTime], GETDATE())) AS [EndTime],
        SUM(ISNULL(tt.[WorkingHours], 0)) AS [WorkingHours],
        SUM(DATEDIFF(MINUTE, tt.[StartTime], ISNULL(tt.[EndTime], GETDATE()))) AS [TotalMinutes],
        COALESCE(a.[Type], ta.[Type], 'Analysis') AS [Category],
        COALESCE(a.[Status], ta.[Status], 'In Progress') AS [Status],
        COALESCE(t.[Priority], tk.[Priority], 'Normal') AS [Priority],
        COALESCE(u.[Name], u.[UserName], tt.[CreatedBy], 'Employee') AS [AssignedTo],
        COALESCE(a.[TaskDesctiption], ta.[TaskDesctiption], '') AS [Description]
    FROM [dbo].[CTaskTimes] tt
    LEFT JOIN [dbo].[CTaskAnalysis] a ON (tt.[EntityType] = 'Task' OR tt.[EntityType] IS NULL) AND tt.[EntityId] = a.[Id]
    LEFT JOIN [dbo].[CTasks] t ON a.[TaskId] = t.[Id]
    LEFT JOIN [dbo].[CProjects] p1 ON t.[ProjectId] = p1.[Id]
    LEFT JOIN [dbo].[CTicketAnalysis] ta ON tt.[EntityType] = 'Ticket' AND tt.[EntityId] = ta.[Id]
    LEFT JOIN [dbo].[CTickets] tk ON ta.[TicketId] = tk.[Id]
    LEFT JOIN [dbo].[CProjects] p2 ON tk.[ProjectId] = p2.[Id]
    LEFT JOIN [dbo].[AspNetUsers] u ON (a.[EmpId] = u.[EmpId] OR ta.[EmpId] = u.[EmpId] OR tt.[CreatedBy] = u.[Id] OR tt.[CreatedBy] = CAST(u.[EmpId] AS NVARCHAR(50)))
    WHERE YEAR(tt.[StartTime]) = @Year
      AND MONTH(tt.[StartTime]) = @Month
      AND (@CompanyId = 0 OR a.[CompanyId] = @CompanyId OR ta.[CompanyId] = @CompanyId OR a.[CompanyId] IS NULL)
      AND (
          @EmpId IS NULL OR @EmpId = 0 
          OR a.[EmpId] = @EmpId
          OR ta.[EmpId] = @EmpId
          OR tt.[CreatedBy] = CAST(@EmpId AS NVARCHAR(50)) 
          OR tt.[CreatedBy] = @UserIdentifier
      )
    GROUP BY 
        COALESCE(tt.[EntityType], 'Task'),
        tt.[EntityId],
        a.[Id],
        ta.[Id],
        a.[TaskSubject],
        ta.[TaskSubject],
        a.[Code],
        ta.[Code],
        p1.[ProjectName],
        p2.[ProjectName],
        CAST(tt.[StartTime] AS DATE),
        a.[Type],
        ta.[Type],
        a.[Status],
        ta.[Status],
        t.[Priority],
        tk.[Priority],
        u.[Name],
        u.[UserName],
        tt.[CreatedBy],
        a.[TaskDesctiption],
        ta.[TaskDesctiption]
    HAVING SUM(DATEDIFF(MINUTE, tt.[StartTime], ISNULL(tt.[EndTime], GETDATE()))) >= @MinMinutes
        OR SUM(ISNULL(tt.[WorkingHours], 0)) >= (@MinMinutes / 60.0)
    ORDER BY [DueDate] ASC, [TotalMinutes] DESC;
END
GO

select * from CTaskTimes

select * from CTickets

select * from CMileStoneApprovals
select * from CMileStoneApprovalDetails

select * from CMilestones
select * from CMilestoneDetails

exec sp_GetCalendarWorkLogsByMonth 2026, 8


select * from CTasks
select * from CTaskAnalysis
select * from CTaskAssignees

select * from CTaskTimes

select * from CTicketAnalysis

select * from CTickets

select * from CTasks
select * from CTaskAssignees



exec sp_GetCalendarWorkLogsByMonth

exec sp_GetCalendarWorkLogsByMonth 2026, 8
-- New
IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_GetCalendarWorkLogsByMonth')
    DROP PROCEDURE [dbo].[sp_GetCalendarWorkLogsByMonth]
GO
CREATE PROCEDURE [dbo].[sp_GetCalendarWorkLogsByMonth]
    @Year INT,
    @Month INT,
    @EmpId INT = NULL,
    @UserIdentifier NVARCHAR(MAX) = NULL,
    @CompanyId INT = 0,
    @MinMinutes INT = 1,
	@DepartmentId INT = NULL,
    @ProjectId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        MIN(tt.[Id]) AS [Id],
        tt.[EntityType],
        tt.[EntityId],
        COALESCE(a.[TaskSubject], ta.[TaskSubject], 'Work Session') AS [Title],
        COALESCE(a.[Code], ta.[Code], CASE WHEN tt.[EntityType] = 'Ticket' THEN 'TAN-' + CAST(tt.[EntityId] AS NVARCHAR(20)) ELSE 'ANL-' + CAST(tt.[EntityId] AS NVARCHAR(20)) END) AS [TaskCode],
        COALESCE(p1.[ProjectName], p2.[ProjectName], 'General') AS [ProjectName],
        CAST(tt.[StartTime] AS DATE) AS [DueDate],
        MIN(tt.[StartTime]) AS [StartTime],
        MAX(ISNULL(tt.[EndTime], GETDATE())) AS [EndTime],
        SUM(ISNULL(tt.[WorkingHours], 0)) AS [WorkingHours],
        SUM(DATEDIFF(MINUTE, tt.[StartTime], ISNULL(tt.[EndTime], GETDATE()))) AS [TotalMinutes],
        COALESCE(a.[Type], ta.[Type], 'Analysis') AS [Category],
        COALESCE(a.[Status], ta.[Status], 'In Progress') AS [Status],
        COALESCE(t.[Priority], tk.[Priority], 'Normal') AS [Priority],
        COALESCE(u.[Name], u.[UserName], tt.[CreatedBy], 'Employee') AS [AssignedTo],
        COALESCE(a.[TaskDesctiption], ta.[TaskDesctiption], '') AS [Description]
    FROM [dbo].[CTaskTimes] tt
    LEFT JOIN [dbo].[CTaskAnalysis] a ON (tt.[EntityType] = 'Task' OR tt.[EntityType] IS NULL) AND tt.[EntityId] = a.[Id]
    LEFT JOIN [dbo].[CTasks] t ON a.[TaskId] = t.[Id]
    LEFT JOIN [dbo].[CProjects] p1 ON t.[ProjectId] = p1.[Id]
    LEFT JOIN [dbo].[CTicketAnalysis] ta ON tt.[EntityType] = 'Ticket' AND tt.[EntityId] = ta.[Id]
    LEFT JOIN [dbo].[CTickets] tk ON ta.[TicketId] = tk.[Id]
    LEFT JOIN [dbo].[CProjects] p2 ON tk.[ProjectId] = p2.[Id]
    LEFT JOIN [dbo].[AspNetUsers] u ON (a.[EmpId] = u.[EmpId] OR ta.[EmpId] = u.[EmpId] OR tt.[CreatedBy] = u.[Id] OR tt.[CreatedBy] = CAST(u.[EmpId] AS NVARCHAR(50)))
    WHERE YEAR(tt.[StartTime]) = @Year
      AND MONTH(tt.[StartTime]) = @Month
      AND (@CompanyId = 0 OR a.[CompanyId] = @CompanyId OR ta.[CompanyId] = @CompanyId OR a.[CompanyId] IS NULL)
      AND (
          @EmpId IS NULL
          OR a.[EmpId] = @EmpId
          OR ta.[EmpId] = @EmpId
      )
	  AND (
          @ProjectId IS NULL
          OR t.[ProjectId] = @ProjectId 
          OR tk.[ProjectId] = @ProjectId
      )
      AND (
          @DepartmentId IS NULL 
          OR u.[DepartmenId] = @DepartmentId
      )
    GROUP BY 
        tt.[EntityType],
        tt.[EntityId],
        COALESCE(a.[TaskSubject], ta.[TaskSubject], 'Work Session'),
        COALESCE(a.[Code], ta.[Code], CASE WHEN tt.[EntityType] = 'Ticket' THEN 'TAN-' + CAST(tt.[EntityId] AS NVARCHAR(20)) ELSE 'ANL-' + CAST(tt.[EntityId] AS NVARCHAR(20)) END),
        COALESCE(p1.[ProjectName], p2.[ProjectName], 'General'),
        CAST(tt.[StartTime] AS DATE),
        COALESCE(a.[Type], ta.[Type], 'Analysis'),
        COALESCE(a.[Status], ta.[Status], 'In Progress'),
        COALESCE(t.[Priority], tk.[Priority], 'Normal'),
        COALESCE(u.[Name], u.[UserName], tt.[CreatedBy], 'Employee'),
        COALESCE(a.[TaskDesctiption], ta.[TaskDesctiption], '')
    HAVING SUM(DATEDIFF(MINUTE, tt.[StartTime], ISNULL(tt.[EndTime], GETDATE()))) >= @MinMinutes
        OR SUM(ISNULL(tt.[WorkingHours], 0)) >= (@MinMinutes / 60.0)
    ORDER BY [DueDate] ASC, [TotalMinutes] DESC;
END
GO

select * from CTaskAnalysis

select * from CTicketAnalysis

select * from CProjects


IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_GetAllProjects')
    DROP PROCEDURE [dbo].[sp_GetAllProjects]
GO
CREATE PROCEDURE [dbo].[sp_GetAllProjects]  
    @CompanyId INT,  
    @FinancialYearId INT  
AS  
BEGIN  
    SET NOCOUNT ON;  
  
    SELECT   
        [Id], [ServiceType], [Code], [ProjectName], [OrganizationId],   
        [StartDate], [TargetEndDate], [ActualandDate],   
        [Comment], [Status], [ProjectTypeId], [Deliverable],   
        [Priority], [Progress], [Description], [CompanyId],   
        [FinancialYearID], [Active], [CreatedOn], [CreatedBy],   
        [ModifiedOn], [ModifiedBy]  
    FROM [dbo].[CProjects]  
    WHERE [CompanyId] = @CompanyId  
      AND [Active] = 1 AND @FinancialYearId = [FinancialYearID]  
    ORDER BY [Id] DESC;  
END  


select * from CRoutineTasks
select * from CRoutines
select * from CRoutineDetails

select * from CRoutineTasks
select * from CTaskAnalysis
select * from CTasks

exec [sp_GetCalendarDayWorkLogs] 2026

exec sp_GetCalendarDayWorkLogs '2026-08-31'

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_GetCalendarDayWorkLogs')
    DROP PROCEDURE [dbo].[sp_GetCalendarDayWorkLogs]
GO
CREATE PROCEDURE [dbo].[sp_GetCalendarDayWorkLogs]
    @Date DATE,
    @EmpId INT = NULL,
    @UserIdentifier NVARCHAR(MAX) = NULL,
    @CompanyId INT = 0,
    @MinMinutes INT = 1,
	@DepartmentId INT = NULL,
    @ProjectId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        tt.[Id] AS [TimeLogId],
        COALESCE(tt.[EntityType], 'Task') AS [EntityType],
        tt.[EntityId],
        COALESCE(a.[Code], ta.[Code], CASE WHEN tt.[EntityType] = 'Ticket' THEN 'TAN-' + CAST(tt.[EntityId] AS NVARCHAR(20)) ELSE 'ANL-' + CAST(tt.[EntityId] AS NVARCHAR(20)) END) AS [SubTaskCode],
        COALESCE(a.[TaskSubject], ta.[TaskSubject], 'Work Session') AS [SubTaskTitle],
        COALESCE(t.[Code], tk.[Code], '—') AS [ParentTaskCode],
        COALESCE(t.[Subject], tk.[Subject], '—') AS [ParentTaskTitle],
        COALESCE(p1.[ProjectName], p2.[ProjectName], 'General') AS [ProjectName],
        COALESCE(a.[Type], ta.[Type], 'Analysis') AS [Category],
        COALESCE(tt.[Status], a.[Status], ta.[Status], 'In Progress') AS [Status],
        tt.[StartTime],
        tt.[EndTime],
        ISNULL(tt.[WorkingHours], 0) AS [WorkingHours],
        DATEDIFF(MINUTE, tt.[StartTime], ISNULL(tt.[EndTime], GETDATE())) AS [TotalMinutes],
        COALESCE(a.[TaskDesctiption], ta.[TaskDesctiption], '') AS [Description],
        COALESCE(u.[Name], u.[UserName], tt.[CreatedBy], 'Employee') AS [CreatedBy]
    FROM [dbo].[CTaskTimes] tt
    LEFT JOIN [dbo].[CTaskAnalysis] a ON (tt.[EntityType] = 'Task' OR tt.[EntityType] IS NULL) AND tt.[EntityId] = a.[Id]
    LEFT JOIN [dbo].[CTasks] t ON a.[TaskId] = t.[Id]
    LEFT JOIN [dbo].[CProjects] p1 ON t.[ProjectId] = p1.[Id]
    LEFT JOIN [dbo].[CTicketAnalysis] ta ON tt.[EntityType] = 'Ticket' AND tt.[EntityId] = ta.[Id]
    LEFT JOIN [dbo].[CTickets] tk ON ta.[TicketId] = tk.[Id]
    LEFT JOIN [dbo].[CProjects] p2 ON tk.[ProjectId] = p2.[Id]
    LEFT JOIN [dbo].[AspNetUsers] u ON (a.[EmpId] = u.[EmpId] OR ta.[EmpId] = u.[EmpId] OR tt.[CreatedBy] = u.[Id] OR tt.[CreatedBy] = CAST(u.[EmpId] AS NVARCHAR(50)))
    WHERE CAST(tt.[StartTime] AS DATE) = @Date
      AND (@CompanyId = 0 OR a.[CompanyId] = @CompanyId OR ta.[CompanyId] = @CompanyId OR a.[CompanyId] IS NULL)
      AND (
          @EmpId IS NULL OR @EmpId = 0 
          OR a.[EmpId] = @EmpId
          OR ta.[EmpId] = @EmpId
          OR tt.[CreatedBy] = CAST(@EmpId AS NVARCHAR(50)) 
          OR tt.[CreatedBy] = @UserIdentifier
      )
      AND (
          DATEDIFF(MINUTE, tt.[StartTime], ISNULL(tt.[EndTime], GETDATE())) >= @MinMinutes
          OR ISNULL(tt.[WorkingHours], 0) >= (@MinMinutes / 60.0)
      )AND (
          @ProjectId IS NULL
          OR t.[ProjectId] = @ProjectId 
          OR tk.[ProjectId] = @ProjectId
      )
      AND (
          @DepartmentId IS NULL
          OR u.[DepartmenId] = @DepartmentId
      )
    ORDER BY tt.[StartTime] ASC;
END
GO


-- ==========================================================================================
-- 6. STORED PROCEDURE: sp_TicketAnalysisUpdateStatus
-- ==========================================================================================
IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_TicketAnalysisUpdateStatus')
    DROP PROCEDURE [dbo].[sp_TicketAnalysisUpdateStatus]
GO
CREATE PROCEDURE [dbo].[sp_TicketAnalysisUpdateStatus]
    @Id INT,
    @Status NVARCHAR(50),
    @EmpId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TicketId INT;
    DECLARE @ClosedOn DATETIME2 = NULL;
    DECLARE @StartedWorkingOn DATETIME2 = NULL;

    SELECT 
        @TicketId = [TicketId],
        @StartedWorkingOn = [StartedWorkingOn]
    FROM [dbo].[CTicketAnalysis] 
    WHERE [Id] = @Id;

    IF @Status IN ('In Progress', 'InProgress', 'Review', 'Under Review') AND @StartedWorkingOn IS NULL
    BEGIN
        SET @StartedWorkingOn = GETDATE();
    END

    IF @Status IN ('Done', 'Completed', 'Resolved', 'Closed')
    BEGIN
        SET @ClosedOn = GETDATE();
    END

    UPDATE [dbo].[CTicketAnalysis]
    SET 
        [Status] = @Status,
        [StartedWorkingOn] = COALESCE(@StartedWorkingOn, [StartedWorkingOn]),
        [ClosedOn] = @ClosedOn,
        [ModifiedOn] = GETDATE(),
        [ModifiedBy] = CAST(@EmpId AS NVARCHAR(50))
    WHERE [Id] = @Id;

    IF @TicketId IS NOT NULL
    BEGIN
        EXEC [dbo].[sp_RecalculateTicketProgressAndStatus] @TicketId = @TicketId;
    END
END
GO


IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_RecalculateTicketProgressAndStatus')
    DROP PROCEDURE [dbo].[sp_RecalculateTicketProgressAndStatus]
GO
CREATE PROCEDURE [dbo].[sp_RecalculateTicketProgressAndStatus]
    @TicketId INT
AS
BEGIN
    SET NOCOUNT ON;

    IF @TicketId IS NULL OR @TicketId <= 0 RETURN;

    DECLARE @TotalSubTasks INT = 0;
    DECLARE @DoneSubTasks INT = 0;
    DECLARE @InProgressSubTasks INT = 0;
    DECLARE @ToDoSubTasks INT = 0;

    SELECT 
        @TotalSubTasks = COUNT(*),
        @DoneSubTasks = SUM(CASE WHEN [Status] IN ('Done', 'Completed', 'Resolved', 'Closed') THEN 1 ELSE 0 END),
        @InProgressSubTasks = SUM(CASE WHEN [Status] IN ('In Progress', 'InProgress', 'Review', 'Under Review') THEN 1 ELSE 0 END),
        @ToDoSubTasks = SUM(CASE WHEN [Status] IN ('To Do', 'ToDo', 'Open') THEN 1 ELSE 0 END)
    FROM [dbo].[CTicketAnalysis]
    WHERE [TicketId] = @TicketId AND ([Active] = 1 OR [Active] IS NULL);

    IF @TotalSubTasks > 0
    BEGIN
        -- All sub-tasks are Done -> Mark Ticket as Resolved
        IF @DoneSubTasks = @TotalSubTasks
        BEGIN
            UPDATE [dbo].[CTickets]
            SET [Status] = 'Resolved',
                [ClosedOn] = COALESCE([ClosedOn], GETDATE()),
                [ModifiedOn] = GETDATE()
            WHERE [Id] = @TicketId AND [Status] NOT IN ('Resolved', 'Closed');
        END
        -- Some sub-tasks are In Progress or Done -> Mark Ticket as In Progress
        ELSE IF @InProgressSubTasks > 0 OR @DoneSubTasks > 0
        BEGIN
            UPDATE [dbo].[CTickets]
            SET [Status] = 'In Progress',
                [ClosedOn] = NULL,
                [ModifiedOn] = GETDATE()
            WHERE [Id] = @TicketId AND [Status] NOT IN ('In Progress', 'Resolved', 'Closed');
        END
    END
END
GO


IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_TaskAnalysisDelete')
    DROP PROCEDURE [dbo].[sp_TaskAnalysisDelete]
GO
CREATE PROCEDURE [dbo].[sp_TaskAnalysisDelete]
    @Id INT,
    @ModifiedBy NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Id IS NULL OR @Id <= 0 RETURN;

    DECLARE @TaskId INT = (SELECT [TaskId] FROM [dbo].[CTaskAnalysis] WHERE [Id] = @Id);

    -- 1. Soft Delete / Deactivate SubTask
    UPDATE [dbo].[CTaskAnalysis]
    SET 
        [Active] = 0,
        [ModifiedOn] = GETDATE(),
        [ModifiedBy] = @ModifiedBy
    WHERE [Id] = @Id;

    -- 2. Clean up any lingering open live timer sessions for this sub-task
    DELETE FROM [dbo].[CTaskTimes]
    WHERE ([EntityType] = 'Task' OR [EntityType] IS NULL)
      AND [EntityId] = @Id
      AND [EndTime] IS NULL;

    -- 3. Recalculate Parent Task Progress & Status
    IF @TaskId IS NOT NULL
    BEGIN
        DECLARE @TotalCount INT = 0;
        DECLARE @DoneCount INT = 0;
        DECLARE @InProgressCount INT = 0;

        SELECT 
            @TotalCount = COUNT(*),
            @DoneCount = SUM(CASE WHEN [Status] IN ('Done', 'Completed', 'Resolved', 'Closed') THEN 1 ELSE 0 END),
            @InProgressCount = SUM(CASE WHEN [Status] IN ('In Progress', 'InProgress', 'Review', 'Under Review') THEN 1 ELSE 0 END)
        FROM [dbo].[CTaskAnalysis]
        WHERE [TaskId] = @TaskId 
          AND ([Active] IS NULL OR [Active] = 1);
		  select * from CTasks
        IF @TotalCount > 0
        BEGIN
            -- All remaining sub-tasks are Done
            IF @DoneCount = @TotalCount
            BEGIN
                UPDATE [dbo].[CTasks]
                SET [Status] = 'Done',
                    [ClosedOn] = COALESCE([ClosedOn], GETDATE()),
                    [ModifiedOn] = GETDATE()
                WHERE [Id] = @TaskId;
            END
            -- Some sub-tasks are In Progress or Done
            ELSE IF @InProgressCount > 0 OR @DoneCount > 0
            BEGIN
                UPDATE [dbo].[CTasks]
                SET [Status] = 'In Progress',
                    [ClosedOn] = NULL,
                    [ModifiedOn] = GETDATE()
                WHERE [Id] = @TaskId;
            END
            -- All remaining sub-tasks are To Do
            ELSE
            BEGIN
                UPDATE [dbo].[CTasks]
                SET [Status] = 'To Do',
                    [ClosedOn] = NULL,
                    [ModifiedOn] = GETDATE()
                WHERE [Id] = @TaskId AND [Status] NOT IN ('To Do');
            END
        END
    END
END
GO


IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_TaskAnalysisUpdate')
    DROP PROCEDURE [dbo].[sp_TaskAnalysisUpdate]
GO
CREATE PROCEDURE [dbo].[sp_TaskAnalysisUpdate]
    @Id INT,
    @EmpId INT = NULL,
    @TaskSubject NVARCHAR(500),
    @TaskDesctiption NVARCHAR(MAX) = NULL,
    @StartedWorkingOn DATETIME2 = NULL,
    @ClosedOn DATETIME2 = NULL,
    @TimeTaken DECIMAL(18,2) = 0,
    @Type NVARCHAR(50) = 'Analysis',
    @Status NVARCHAR(50) = 'To Do',
    @ModifiedOn DATETIME2 = NULL,
    @ModifiedBy NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[CTaskAnalysis]
    SET 
        [EmpId] = CASE WHEN @EmpId IS NOT NULL THEN @EmpId ELSE [EmpId] END,
        [TaskSubject] = @TaskSubject,
        [TaskDesctiption] = @TaskDesctiption,
        [StartedWorkingOn] = @StartedWorkingOn,
        [ClosedOn] = @ClosedOn,
        [TimeTaken] = @TimeTaken,
        [Type] = @Type,
        [Status] = @Status,
        [ModifiedOn] = COALESCE(@ModifiedOn, GETDATE()),
        [ModifiedBy] = @ModifiedBy
    WHERE [Id] = @Id;

    -- Recalculate Parent Task Progress and Status
    DECLARE @TaskId INT = (SELECT [TaskId] FROM [dbo].[CTaskAnalysis] WHERE [Id] = @Id);
    IF @TaskId IS NOT NULL
    BEGIN
        DECLARE @TotalCount INT = 0;
        DECLARE @DoneCount INT = 0;
        DECLARE @InProgressCount INT = 0;

        SELECT 
            @TotalCount = COUNT(*),
            @DoneCount = SUM(CASE WHEN [Status] IN ('Done', 'Completed', 'Resolved', 'Closed') THEN 1 ELSE 0 END),
            @InProgressCount = SUM(CASE WHEN [Status] IN ('In Progress', 'InProgress', 'Review', 'Under Review') THEN 1 ELSE 0 END)
        FROM [dbo].[CTaskAnalysis]
        WHERE [TaskId] = @TaskId AND ([Active] = 1 OR [Active] IS NULL);

        IF @TotalCount > 0
        BEGIN
            IF @DoneCount = @TotalCount
            BEGIN
                UPDATE [dbo].[CTasks]
                SET [Status] = 'Done',
                    [ClosedOn] = COALESCE([ClosedOn], GETDATE()),
                    [ModifiedOn] = GETDATE()
                WHERE [Id] = @TaskId AND [Status] <> 'Done';
            END
            ELSE IF @InProgressCount > 0 OR @DoneCount > 0
            BEGIN
                UPDATE [dbo].[CTasks]
                SET [Status] = 'In Progress',
                    [ClosedOn] = NULL,
                    [ModifiedOn] = GETDATE()
                WHERE [Id] = @TaskId AND [Status] NOT IN ('In Progress', 'Done');
            END
        END
    END
END
GO

select * from CTasks
select * from CTaskAnalysis

select * from CEmployeeSalaries

exec [sp_EmployeeSalaryGetActiveRates]

CREATE PROCEDURE [dbo].[sp_EmployeeSalaryGetActiveRates]
    @CompanyId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        [Id],
        [EmployeeId],
        [PreparedById],
        [PerHourSalary],
        [TaxPercentage],
        [TaxDeducted],
        [NetPerHour],
        [EffectiveDate],
        [Note],
        [RevisionNo],
        [CompanyId],
        [FinancialYearID],
        [CreatedOn],
        [CreatedBy],
        [ModifiedOn],
        [ModifiedBy],
        [Active]
    FROM [dbo].[CEmployeeSalaries]
    WHERE ([Active] = 1 OR [Active] IS NULL)
      AND (@CompanyId IS NULL OR @CompanyId = 0 OR [CompanyId] = @CompanyId OR [CompanyId] IS NULL)
    ORDER BY [EffectiveDate] DESC, [RevisionNo] DESC, [Id] DESC;
END
GO


CREATE PROCEDURE sp_GetMonthlyUserPoints
    @UserId   INT,
    @Month    INT,
    @Year     INT,
    @TenantId INT
AS
BEGIN
    SET NOCOUNT ON;
 
    SELECT SUM(PointsAwarded) AS TotalPoints
    FROM TaskPointTransaction
    WHERE UserId = @UserId
      AND TenantId = @TenantId
      AND MONTH(CalculatedOn) = @Month
      AND YEAR(CalculatedOn) = @Year;
END
GO
 

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_GetMonthlyUserPointsBreakdown')
    DROP PROCEDURE [dbo].[sp_GetMonthlyUserPointsBreakdown]
GO
CREATE PROCEDURE [dbo].[sp_GetMonthlyUserPointsBreakdown]
    @UserId    INT,
    @Month     INT,
    @Year      INT,
    @CompanyId INT = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        t.[Id],
        t.[EntityId],
        t.[EntityType],
        t.[EntityId] AS [TaskId],
        COALESCE(tsk.[Subject], tck.[Subject]) AS [TaskSubject],
        COALESCE(tsk.[Code], tck.[Code]) AS [TaskCode],
        t.[TransactionType],
        t.[DueDateTime],
        t.[EventDateTime],
        t.[BusinessHoursDiff],
        t.[PointsAwarded],
        t.[CalculatedOn],
        pm.[Name] AS [PriorityName]
    FROM [dbo].[CTaskPointTransactions] t
    LEFT JOIN [dbo].[CTasks] tsk ON tsk.[Id] = t.[EntityId] AND (t.[EntityType] = 'Task' OR t.[EntityType] IS NULL)
    LEFT JOIN [dbo].[CTickets] tck ON tck.[Id] = t.[EntityId] AND t.[EntityType] = 'Ticket'
    LEFT JOIN [dbo].[CPriorityPoints] pm ON pm.[Id] = t.[PriorityIdUsed]
    WHERE t.[UserId] = @UserId
      AND (t.[CompanyId] = @CompanyId)
      AND MONTH(t.[CalculatedOn]) = @Month
      AND YEAR(t.[CalculatedOn]) = @Year
      AND (t.[Active] = 1 OR t.[Active] IS NULL)
    ORDER BY t.[EntityType], t.[EntityId], t.[CalculatedOn];
END
GO


select * from Logs 

select * from CTasks
select * from CTickets

select * from CTaskPointTransactions

update CTaskPointTransactions set EntityType = 'Task' where Id < 7

select * from CTickets

select * from CTaskTimes
select * from CTaskAnalysis

delete from 

delete from CTaskAnalysis where EmpId = 3
delete from CTaskTimes where CreatedBy = 3
delete from CTaskPointTransactions


select * from AspNetUsers

select * from CTickets
select * from CTaskTimes


IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'usp_EmployeeTaskReport')
    DROP PROCEDURE [dbo].usp_EmployeeTaskReport
GO
CREATE PROCEDURE dbo.usp_EmployeeTaskReport
    @EmpId           INT   = NULL,   -- NULL = all employees
    @Date            DATE  = NULL,   -- NULL = whole month (see @Month/@Year below)
    @Month           INT   = NULL,   -- only used when @Date IS NULL; defaults to current month
    @Year            INT   = NULL,   -- only used when @Date IS NULL; defaults to current year
    @CompanyId       INT   = NULL,
    @FinancialYearId INT   = NULL
AS
BEGIN
    SET NOCOUNT ON;

    ------------------------------------------------------------
    -- Resolve the date window
    ------------------------------------------------------------
    DECLARE @StartDate DATE;
    DECLARE @EndDateExclusive DATE;

    IF @Date IS NOT NULL
    BEGIN
        SET @StartDate = @Date;
        SET @EndDateExclusive = DATEADD(DAY, 1, @Date);
    END
    ELSE
    BEGIN
        DECLARE @EffYear  INT = ISNULL(@Year, YEAR(GETDATE()));
        DECLARE @EffMonth INT = ISNULL(@Month, MONTH(GETDATE()));

        SET @StartDate = DATEFROMPARTS(@EffYear, @EffMonth, 1);
        SET @EndDateExclusive = DATEADD(MONTH, 1, @StartDate);
    END

    ------------------------------------------------------------
    -- Main report
    -- One row per CTaskTimes entry (>30s duration), carrying
    -- employee / project / task / analysis / ticket details.
    -- Ordered by day, then by TaskTime (StartTime) within the day.
    ------------------------------------------------------------
    SELECT
        CAST(tt.StartTime AS DATE)                 AS TaskDate,

        -- Employee
        e.EmpId                                        AS EmployeeId,
        e.Name                                       AS EmployeeName,        -- ADJUST: replace e.Name with your actual employee name column/expression

        -- Project
        p.Id                                        AS ProjectId,
        p.ProjectName                                       AS ProjectName,         -- ADJUST: replace p.Name with your actual project name column
	
        -- Task (CTasks)
        t.Id                                        AS TaskId,
        t.Code                                       AS TaskCode,
        t.Subject                                    AS TaskSubject,
        t.Type                                       AS TaskType,
        t.Priority                                   AS TaskPriority,
        t.Status                                     AS TaskStatus,
        t.isTicket                                   AS IsTicketOrigin,

        -- Task Analysis (CTaskAnalysis)
        ta.Id                                        AS TaskAnalysisId,
        ta.Code                                       AS AnalysisCode,
        ta.TaskSubject                                AS AnalysisSubject,
        ta.TaskDesctiption                            AS AnalysisDescription,
        ta.Type                                       AS AnalysisType,
        ta.Status                                     AS AnalysisStatus,
        ta.StartedWorkingOn                           AS AnalysisStartedOn,
        ta.ClosedOn                                   AS AnalysisClosedOn,
        ta.TimeTaken                                  AS AnalysisTimeTaken,

        -- Ticket (CTickets) — only present if the task originated from / is linked to a ticket
        tk.Id                                        AS TicketId,
        tk.Code                                       AS TicketCode,
        tk.Subject                                    AS TicketSubject,
        tk.Type                                       AS TicketType,
        tk.Priority                                   AS TicketPriority,
        tk.Status                                     AS TicketStatus,
        tk.ContactPerson                              AS TicketContactPerson,
        tk.ContactNo                                  AS TicketContactNo,
        tk.ReceivedOn                                 AS TicketReceivedOn,
        tk.DueOn                                      AS TicketDueOn,

        -- Time entry (CTaskTimes) — this is the row-level "task time" being reported
        tt.Id                                         AS TaskTimeId,
        tt.Status                                      AS TimeEntryStatus,
        tt.StartTime                                   AS TaskTime,
        tt.EndTime,
        DATEDIFF(SECOND, tt.StartTime, tt.EndTime)     AS DurationSeconds,
        tt.WorkingHours

    FROM CTaskTimes tt
    INNER JOIN CTaskAnalysis ta
        ON ta.Id = tt.TaskAnalysisId
       AND ta.Active = 1
    INNER JOIN CTasks t
        ON t.Id = ta.TaskId
       AND t.Active = 1
    LEFT JOIN CTickets tk
        ON tk.Id = t.TicketId
    LEFT JOIN CProjects p                              -- ADJUST: replace with your actual Projects table name
        ON p.Id = t.ProjectId
    LEFT JOIN AspNetUsers e                              -- ADJUST: replace with your actual Employees table name
        ON e.EmpId = ta.EmpId

    WHERE
        tt.EndTime IS NOT NULL
        AND DATEDIFF(SECOND, tt.StartTime, tt.EndTime) > 30
        AND CAST(tt.StartTime AS DATE) >= @StartDate
        AND CAST(tt.StartTime AS DATE) <  @EndDateExclusive
        AND (@EmpId IS NULL OR ta.EmpId = @EmpId)
        AND (@CompanyId IS NULL OR t.CompanyId = @CompanyId)
        AND (@FinancialYearId IS NULL OR t.FinancialYearId = @FinancialYearId)

    ORDER BY
        TaskDate,
        EmployeeName,
        TaskTime;
END
GO



select * from CTasks
select * from CTaskAnalysis
select * from CTaskTimes
select * from CRoutines
select * from CRoutineDetails
select * from CRoutineTasks
select * from CRoutineAnalysis
