using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNatureofAccountMenuAndRights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @ModuleId INT;

IF NOT EXISTS (SELECT 1 FROM [dbo].[CModules] WHERE [Name] = 'Nature of Account' OR [Url] = 'NatureofAccount')
BEGIN
    SELECT @ModuleId = ISNULL(MAX(Id), 0) + 1 FROM [dbo].[CModules];
    
    SET IDENTITY_INSERT [dbo].[CModules] ON;
    INSERT INTO [dbo].[CModules] ([Id], [Name], [Url], [Order], [isParentModule], [isSubModule], [ParentModuleId], [Active], [CreatedOn], [CreatedBy])
    VALUES (@ModuleId, 'Nature of Account', 'NatureofAccount', 15, 1, 0, 0, 1, GETDATE(), 'System');
    SET IDENTITY_INSERT [dbo].[CModules] OFF;
END
ELSE
BEGIN
    SELECT TOP 1 @ModuleId = [Id] FROM [dbo].[CModules] WHERE [Name] = 'Nature of Account' OR [Url] = 'NatureofAccount';
    
    UPDATE [dbo].[CModules]
    SET [Active] = 1, [Url] = 'NatureofAccount', [isParentModule] = 1, [isSubModule] = 0, [ParentModuleId] = 0
    WHERE [Id] = @ModuleId;
END

-- Grant permissions to all active roles in CRoleModulesPermissions if not already present
IF @ModuleId IS NOT NULL AND @ModuleId > 0
BEGIN
    INSERT INTO [dbo].[CRoleModulesPermissions] ([RoleId], [ModuleId])
    SELECT r.[Id], @ModuleId
    FROM [dbo].[CRoles] r
    WHERE NOT EXISTS (
        SELECT 1 FROM [dbo].[CRoleModulesPermissions] p 
        WHERE p.[RoleId] = r.[Id] AND p.[ModuleId] = @ModuleId
    );
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @ModuleId INT;
SELECT TOP 1 @ModuleId = [Id] FROM [dbo].[CModules] WHERE [Name] = 'Nature of Account' OR [Url] = 'NatureofAccount';

IF @ModuleId IS NOT NULL
BEGIN
    DELETE FROM [dbo].[CRoleModulesPermissions] WHERE [ModuleId] = @ModuleId;
    DELETE FROM [dbo].[CModules] WHERE [Id] = @ModuleId;
END
");
        }
    }
}
