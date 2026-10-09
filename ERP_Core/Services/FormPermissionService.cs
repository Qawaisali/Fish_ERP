using ERP_Core.Data;
using ERP_Core.Models;
using Microsoft.EntityFrameworkCore;

namespace ERP_Core.Services
{
    public class FormPermissionService
    {
        private readonly ERPDbContext _dbContext;

        public FormPermissionService(ERPDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<Modules>> GetAllFormsAsync()
        {
            return await _dbContext.CModules.ToListAsync();
        }

        public async Task AssignFormsToRoleAsync(int roleId, List<int> formIds)
        {
            var existing = _dbContext.CRoleModulesPermissions.Where(rf => rf.RoleId == roleId);
            _dbContext.CRoleModulesPermissions.RemoveRange(existing);

            var newAssignments = formIds.Select(formId => new RoleModulesPermission
            {
                RoleId = roleId,
                ModuleId = formId
            });

            await _dbContext.CRoleModulesPermissions.AddRangeAsync(newAssignments);
            await _dbContext.SaveChangesAsync();
        }
    }
}
