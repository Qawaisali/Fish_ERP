-- ==========================================================================================
-- SCRIPT: SP_FixTicketAnalysisStatusRollup.sql
-- PURPOSE: Fixes Ticket Analysis status recalculation so that creating or updating
--          a sub-task/analysis on a Completed/Resolved/Closed Ticket dynamically moves
--          the Ticket status back to 'To Do' or 'In Progress' and clears [ClosedOn].
-- ==========================================================================================

-- ------------------------------------------------------------------------------------------
-- 1. STORED PROCEDURE: sp_RecalculateTicketProgressAndStatus
-- ------------------------------------------------------------------------------------------
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
        @ToDoSubTasks = SUM(CASE WHEN [Status] IN ('To Do', 'ToDo', 'Open', 'Not Started') THEN 1 ELSE 0 END)
    FROM [dbo].[CTicketAnalysis]
    WHERE [TicketId] = @TicketId AND ([Active] = 1 OR [Active] IS NULL);

    IF @TotalSubTasks > 0
    BEGIN
        -- 1. All sub-tasks are Done/Resolved -> Mark Ticket as Resolved
        IF @DoneSubTasks = @TotalSubTasks
        BEGIN
            UPDATE [dbo].[CTickets]
            SET [Status] = 'Resolved',
                [ClosedOn] = COALESCE([ClosedOn], GETDATE()),
                [ModifiedOn] = GETDATE()
            WHERE [Id] = @TicketId AND [Status] <> 'Resolved';
        END
        -- 2. Some sub-tasks are In Progress OR some are Done (while other subtasks remain open) -> Reopen to In Progress
        ELSE IF @InProgressSubTasks > 0 OR @DoneSubTasks > 0
        BEGIN
            UPDATE [dbo].[CTickets]
            SET [Status] = 'In Progress',
                [ClosedOn] = NULL,
                [ModifiedOn] = GETDATE()
            WHERE [Id] = @TicketId AND [Status] NOT IN ('In Progress', 'Cancelled');
        END
        -- 3. All sub-tasks are To Do / Open -> Reopen to To Do
        ELSE IF @ToDoSubTasks = @TotalSubTasks
        BEGIN
            UPDATE [dbo].[CTickets]
            SET [Status] = 'To Do',
                [ClosedOn] = NULL,
                [ModifiedOn] = GETDATE()
            WHERE [Id] = @TicketId AND [Status] NOT IN ('To Do', 'Cancelled');
        END
    END
END
GO


-- ------------------------------------------------------------------------------------------
-- 2. STORED PROCEDURE: sp_TicketAnalysisInsert
-- ------------------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_TicketAnalysisInsert')
    DROP PROCEDURE [dbo].[sp_TicketAnalysisInsert]
GO

CREATE PROCEDURE [dbo].[sp_TicketAnalysisInsert]
    @Id INT OUTPUT,
    @TicketId INT,
    @EmpId INT,
    @Code NVARCHAR(50) = NULL,
    @TaskSubject NVARCHAR(500),
    @TaskDesctiption NVARCHAR(MAX) = NULL,
    @StartedWorkingOn DATETIME2 = NULL,
    @ClosedOn DATETIME2 = NULL,
    @TimeTaken DECIMAL(18,2) = NULL,
    @Type NVARCHAR(50) = 'Analysis',
    @Status NVARCHAR(50) = 'To Do',
    @CompanyId INT = NULL,
    @FinancialYearId INT = NULL,
    @Active BIT = 1,
    @CreatedOn DATETIME2 = NULL,
    @CreatedBy NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[CTicketAnalysis] (
        [TicketId],
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
        @TicketId,
        @EmpId,
        @Code,
        @TaskSubject,
        @TaskDesctiption,
        @StartedWorkingOn,
        @ClosedOn,
        @TimeTaken,
        COALESCE(@Type, 'Analysis'),
        COALESCE(@Status, 'To Do'),
        @CompanyId,
        COALESCE(@Active, 1),
        COALESCE(@CreatedOn, GETDATE()),
        @CreatedBy
    );

    SET @Id = CAST(SCOPE_IDENTITY() AS INT);

    -- Recalculate Ticket status
    EXEC [dbo].[sp_RecalculateTicketProgressAndStatus] @TicketId = @TicketId;
END
GO


-- ------------------------------------------------------------------------------------------
-- 3. STORED PROCEDURE: sp_TicketAnalysisUpdate
-- ------------------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_TicketAnalysisUpdate')
    DROP PROCEDURE [dbo].[sp_TicketAnalysisUpdate]
GO

CREATE PROCEDURE [dbo].[sp_TicketAnalysisUpdate]
    @Id INT,
    @EmpId INT = NULL,
    @TaskSubject NVARCHAR(500),
    @TaskDesctiption NVARCHAR(MAX) = NULL,
    @StartedWorkingOn DATETIME2 = NULL,
    @ClosedOn DATETIME2 = NULL,
    @TimeTaken DECIMAL(18,2) = NULL,
    @Type NVARCHAR(50) = 'Analysis',
    @Status NVARCHAR(50) = 'To Do',
    @Active BIT = 1,
    @ModifiedOn DATETIME2 = NULL,
    @ModifiedBy NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TicketId INT;
    SELECT @TicketId = [TicketId] FROM [dbo].[CTicketAnalysis] WHERE [Id] = @Id;

    IF @Status IN ('Done', 'Completed', 'Resolved', 'Closed') AND @ClosedOn IS NULL
    BEGIN
        SET @ClosedOn = GETDATE();
    END
    ELSE IF @Status IN ('To Do', 'ToDo', 'In Progress', 'InProgress', 'Open')
    BEGIN
        SET @ClosedOn = NULL;
    END

    UPDATE [dbo].[CTicketAnalysis]
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

    IF @TicketId IS NOT NULL
    BEGIN
        EXEC [dbo].[sp_RecalculateTicketProgressAndStatus] @TicketId = @TicketId;
    END
END
GO


-- ------------------------------------------------------------------------------------------
-- 4. STORED PROCEDURE: sp_TicketAnalysisUpdateStatus
-- ------------------------------------------------------------------------------------------
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

    IF @Status IN ('In Progress', 'InProgress') AND @StartedWorkingOn IS NULL
    BEGIN
        SET @StartedWorkingOn = GETDATE();
    END

    IF @Status IN ('Done', 'Completed', 'Resolved', 'Closed')
    BEGIN
        SET @ClosedOn = GETDATE();
    END
    ELSE IF @Status IN ('To Do', 'ToDo', 'In Progress', 'InProgress', 'Open')
    BEGIN
        SET @ClosedOn = NULL;
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


-- ------------------------------------------------------------------------------------------
-- 5. STORED PROCEDURE: sp_TicketAnalysisDelete
-- ------------------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_TicketAnalysisDelete')
    DROP PROCEDURE [dbo].[sp_TicketAnalysisDelete]
GO

CREATE PROCEDURE [dbo].[sp_TicketAnalysisDelete]
    @Id INT,
    @ModifiedBy NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TicketId INT;
    SELECT @TicketId = [TicketId] FROM [dbo].[CTicketAnalysis] WHERE [Id] = @Id;

    UPDATE [dbo].[CTicketAnalysis]
    SET [Active] = 0,
        [ModifiedOn] = GETDATE(),
        [ModifiedBy] = @ModifiedBy
    WHERE [Id] = @Id;

    IF @TicketId IS NOT NULL
    BEGIN
        EXEC [dbo].[sp_RecalculateTicketProgressAndStatus] @TicketId = @TicketId;
    END
END
GO
