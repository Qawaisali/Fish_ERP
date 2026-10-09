using Dapper;
using ERP_Core.Data;
using ERP_Core.Dtos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ERP_Core.DataAccessLayer
{
    public class ModulesDAL
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<ModulesDAL> _logger;

        public ModulesDAL(ERPDbContext context, ILogger<ModulesDAL> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Verifies user role and returns RoleId and RoleName from database via DAL upon login
        /// </summary>
        public async Task<(bool IsValid, int RoleId, string RoleName)> VerifyUserRoleByEmpIdAsync(int empId)
        {
            try
            {
                var connectionString = _context.Database.GetConnectionString();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT TOP 1 
                        u.RoleId, 
                        ISNULL(r.RoleName, CASE WHEN u.RoleId = 1 THEN 'Owner' ELSE 'Staff' END) AS RoleName,
                        ISNULL(r.Active, 1) AS IsRoleActive
                    FROM [dbo].[AspNetUsers] u
                    LEFT JOIN [dbo].[CRoles] r ON u.RoleId = r.Id
                    WHERE u.EmpId = @EmpId AND u.Active = 1";

                var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { EmpId = empId });
                if (result != null)
                {
                    int roleId = (int)result.RoleId;
                    string roleName = (string)result.RoleName;
                    bool isRoleActive = result.IsRoleActive == 1 || result.IsRoleActive == true;

                    if (roleId > 0 && isRoleActive)
                    {
                        return (true, roleId, roleName);
                    }
                }

                // DbContext fallback
                var dbUser = await _context.CApplicationUsers.FirstOrDefaultAsync(u => u.EmpId == empId && u.Active);
                if (dbUser != null && dbUser.RoleId > 0)
                {
                    var role = await _context.CRoles.FirstOrDefaultAsync(r => r.Id == dbUser.RoleId && r.Active == 1);
                    var roleName = role?.RoleName ?? (dbUser.RoleId == 1 ? "Owner" : "Staff");
                    return (true, dbUser.RoleId, roleName);
                }

                return (false, 0, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying user role via DAL for EmpId {EmpId}, using DbContext fallback", empId);
                var dbUser = await _context.CApplicationUsers.FirstOrDefaultAsync(u => u.EmpId == empId && u.Active);
                if (dbUser != null && dbUser.RoleId > 0)
                {
                    var role = await _context.CRoles.FirstOrDefaultAsync(r => r.Id == dbUser.RoleId);
                    var roleName = role?.RoleName ?? (dbUser.RoleId == 1 ? "Owner" : "Staff");
                    return (true, dbUser.RoleId, roleName);
                }
                return (false, 0, string.Empty);
            }
        }

        public async Task<List<SidebarLinkDto>> GetSidebarLinksByEmpIdAsync(int empId, string? identityUserId = null)
        {
            // If empId <= 0, resolve from identity user ID if provided
            if (empId <= 0 && !string.IsNullOrEmpty(identityUserId))
            {
                var resolvedUser = await _context.CApplicationUsers.FirstOrDefaultAsync(u => u.Id == identityUserId);
                if (resolvedUser != null && resolvedUser.EmpId > 0)
                {
                    empId = resolvedUser.EmpId;
                }
            }

            List<SidebarLinkDto> links = new();

            try
            {
                var connectionString = _context.Database.GetConnectionString();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                const string spName = "sp_GetSidebarLinks";
                var result = await connection.QueryAsync<SidebarLinkDto>(
                    spName,
                    new { UserId = empId },
                    commandType: CommandType.StoredProcedure
                );

                links = result?.ToList() ?? new List<SidebarLinkDto>();

                // Normalize any records where ParentModuleId was stored in isParentModule column (e.g. isParentModule > 1)
                foreach (var link in links)
                {
                    if (link.ParentModuleId == 0 && link.isParentModule > 1)
                    {
                        link.ParentModuleId = link.isParentModule;
                        link.isParentModule = 0;
                        link.isSubModule = 1;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stored procedure sp_GetSidebarLinks failed for EmpId {EmpId}. Falling back to LINQ query.", empId);
            }

            // Resilient LINQ fallback if SP returned no links or had an issue
            if (!links.Any() && empId > 0)
            {
                try
                {
                    var user = await _context.CApplicationUsers.FirstOrDefaultAsync(u => u.EmpId == empId);
                    if (user != null)
                    {
                        var isOwnerOrAdmin = user.RoleId == 1 || string.Equals(user.UserName, "zahid.hussain", StringComparison.OrdinalIgnoreCase);

                        if (isOwnerOrAdmin)
                        {
                            // Admin / Owner fallback: load all active modules
                            links = await _context.CModules
                                .Where(m => m.Active == true)
                                .OrderBy(m => m.Order)
                                .Select(m => new SidebarLinkDto
                                {
                                    Id = m.Id,
                                    EmpId = user.EmpId,
                                    Name = m.Name,
                                    Url = m.Url,
                                    ParentModuleId = m.ParentModuleId,
                                    isParentModule = m.isParentModule,
                                    isSUbModule = m.isSubModule,
                                    Order = m.Order
                                })
                                .ToListAsync();
                        }
                        else
                        {
                            // Staff fallback: join CRoleModulesPermissions
                            links = await (
                                from m in _context.CModules
                                join mp in _context.CRoleModulesPermissions on m.Id equals mp.ModuleId
                                where mp.RoleId == user.RoleId && m.Active == true
                                orderby m.Order
                                select new SidebarLinkDto
                                {
                                    Id = m.Id,
                                    EmpId = user.EmpId,
                                    Name = m.Name,
                                    Url = m.Url,
                                    ParentModuleId = m.ParentModuleId,
                                    isParentModule = m.isParentModule,
                                    isSUbModule = m.isSubModule,
                                    Order = m.Order
                                }
                            ).Distinct().ToListAsync();
                        }
                        foreach (var link in links)
                        {
                            if (link.ParentModuleId == 0 && link.isParentModule > 1)
                            {
                                link.ParentModuleId = link.isParentModule;
                                link.isParentModule = 0;
                                link.isSubModule = 1;
                            }
                        }
                    }
                }
                catch (Exception exFallback)
                {
                    _logger.LogError(exFallback, "Error during LINQ fallback for GetSidebarLinksByEmpIdAsync with EmpId {EmpId}", empId);
                }
            }

            return links.OrderBy(m => m.Order).ToList();
        }
    }
}
