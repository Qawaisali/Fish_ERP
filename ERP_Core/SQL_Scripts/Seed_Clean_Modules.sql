-- =========================================================================
-- Seed Clean Modules & Update sp_GetSidebarLinks
-- Description: Cleans legacy ERP forms (Tasks, Tickets, Milestones, Projects, Routines, Salary, etc.)
--              and configures clean Core Architecture modules.
-- =========================================================================

-- 1. Deactivate all legacy/obsolete modules in CModules
UPDATE [dbo].[CModules]
SET [Active] = 0
WHERE [Name] LIKE '%Task%'
   OR [Name] LIKE '%Ticket%'
   OR [Name] LIKE '%Milestone%'
   OR [Name] LIKE '%Project%'
   OR [Name] LIKE '%Routine%'
   OR [Name] LIKE '%Lead%'
   OR [Name] LIKE '%Salary%'
   OR [Name] LIKE '%Evaluation%'
   OR [Name] LIKE '%Priority%'
   OR [Name] LIKE '%Department%'
   OR [Name] LIKE '%Designation%'
   OR [Name] LIKE '%Document%'
   OR [Name] LIKE '%Organization%'
   OR [Name] LIKE '%Notification%'
   OR [Url] LIKE '%task%'
   OR [Url] LIKE '%ticket%'
   OR [Url] LIKE '%milestone%'
   OR [Url] LIKE '%project%'
   OR [Url] LIKE '%routine%'
   OR [Url] LIKE '%lead%'
   OR [Url] LIKE '%salary%'
   OR [Url] LIKE '%evaluat%'
   OR [Url] LIKE '%priority%'
   OR [Url] LIKE '%department%'
   OR [Url] LIKE '%designation%'
   OR [Url] LIKE '%document%'
   OR [Url] LIKE '%organization%'
   OR [Url] LIKE '%notification%';
GO

-- 2. Ensure Core Parent Module: Administration
IF NOT EXISTS (SELECT 1 FROM [dbo].[CModules] WHERE [Name] = 'Administration' AND [isParentModule] = 1)
BEGIN
    INSERT INTO [dbo].[CModules] ([Name], [Url], [ParentModuleId], [isParentModule], [isSubModule], [Order], [Active])
    VALUES ('Administration', '#', 0, 1, 0, 10, 1);
END
ELSE
BEGIN
    UPDATE [dbo].[CModules] 
    SET [Active] = 1, [Order] = 10, [isParentModule] = 1, [isSubModule] = 0
    WHERE [Name] = 'Administration' AND [isParentModule] = 1;
END
GO

DECLARE @AdminParentId INT = (SELECT TOP 1 [Id] FROM [dbo].[CModules] WHERE [Name] = 'Administration' AND [isParentModule] = 1);

-- 3. Ensure Core Sub-Modules
-- 3.1 Register User
IF NOT EXISTS (SELECT 1 FROM [dbo].[CModules] WHERE [Url] = 'user/register' OR [Name] = 'Register User')
BEGIN
    INSERT INTO [dbo].[CModules] ([Name], [Url], [ParentModuleId], [isParentModule], [isSubModule], [Order], [Active])
    VALUES ('Register User', 'user/register', @AdminParentId, 0, 1, 11, 1);
END
ELSE
BEGIN
    UPDATE [dbo].[CModules]
    SET [Name] = 'Register User', [Url] = 'user/register', [ParentModuleId] = @AdminParentId, [isParentModule] = 0, [isSubModule] = 1, [Order] = 11, [Active] = 1
    WHERE [Url] = 'user/register' OR [Name] = 'Register User';
END

-- 3.2 Roles & Permissions
IF NOT EXISTS (SELECT 1 FROM [dbo].[CModules] WHERE [Url] = 'teamaccess' OR [Name] = 'Roles & Permissions')
BEGIN
    INSERT INTO [dbo].[CModules] ([Name], [Url], [ParentModuleId], [isParentModule], [isSubModule], [Order], [Active])
    VALUES ('Roles & Permissions', 'teamaccess', @AdminParentId, 0, 1, 12, 1);
END
ELSE
BEGIN
    UPDATE [dbo].[CModules]
    SET [Name] = 'Roles & Permissions', [Url] = 'teamaccess', [ParentModuleId] = @AdminParentId, [isParentModule] = 0, [isSubModule] = 1, [Order] = 12, [Active] = 1
    WHERE [Url] = 'teamaccess' OR [Name] = 'Roles & Permissions';
END

-- 3.3 Company Setup
IF NOT EXISTS (SELECT 1 FROM [dbo].[CModules] WHERE [Url] = 'companysetup' OR [Name] = 'Company Setup')
BEGIN
    INSERT INTO [dbo].[CModules] ([Name], [Url], [ParentModuleId], [isParentModule], [isSubModule], [Order], [Active])
    VALUES ('Company Setup', 'companysetup', @AdminParentId, 0, 1, 13, 1);
END
ELSE
BEGIN
    UPDATE [dbo].[CModules]
    SET [Name] = 'Company Setup', [Url] = 'companysetup', [ParentModuleId] = @AdminParentId, [isParentModule] = 0, [isSubModule] = 1, [Order] = 13, [Active] = 1
    WHERE [Url] = 'companysetup' OR [Name] = 'Company Setup';
END

-- 3.4 User Profile
IF NOT EXISTS (SELECT 1 FROM [dbo].[CModules] WHERE [Url] = 'profile' OR [Name] = 'My Profile')
BEGIN
    INSERT INTO [dbo].[CModules] ([Name], [Url], [ParentModuleId], [isParentModule], [isSubModule], [Order], [Active])
    VALUES ('My Profile', 'profile', @AdminParentId, 0, 1, 14, 1);
END
ELSE
BEGIN
    UPDATE [dbo].[CModules]
    SET [Name] = 'My Profile', [Url] = 'profile', [ParentModuleId] = @AdminParentId, [isParentModule] = 0, [isSubModule] = 1, [Order] = 14, [Active] = 1
    WHERE [Url] = 'profile' OR [Name] = 'My Profile';
END
GO

-- 4. Update Stored Procedure sp_GetSidebarLinks
IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_GetSidebarLinks')
    DROP PROCEDURE [dbo].[sp_GetSidebarLinks]
GO

CREATE PROCEDURE [dbo].[sp_GetSidebarLinks]
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        u.EmpId,
        m.Id,
        m.Name,
        m.Url,
        m.ParentModuleId,
        m.isParentModule,
        m.isSubModule,
        m.[Order]
    FROM [dbo].[CModules] m
    INNER JOIN [dbo].[CRoleModulesPermissions] mp ON m.Id = mp.ModuleId
    INNER JOIN [dbo].[AspNetUsers] u ON u.RoleId = mp.RoleId
    WHERE u.EmpId = @UserId
      AND m.[Active] = 1
    ORDER BY m.[Order] ASC;
END
GO
