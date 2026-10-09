


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
