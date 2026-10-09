using ERP_Core.Attributes;
using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using ERP_Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP_Core.Controllers.TeamAccess
{
    [Authorize]
    [FormAccessAuthorize("Roles & Permissions")]
    [Route("TeamAccess")]
    public class TeamAccessController : Controller
    {
        private readonly ERPDbContext _dbContext;
        private readonly FormPermissionService _formPermissionService;

        private static readonly string[] ObsoleteKeywords = new[]
        {
            "task", "ticket", "milestone", "project", "routine", "lead", "salary",
            "evaluat", "priority", "calendar", "document", "organization", "notification"
        };

        public TeamAccessController(ERPDbContext dbContext, FormPermissionService formPermissionService)
        {
            _dbContext = dbContext;
            _formPermissionService = formPermissionService;
        }

        private static bool IsValidModule(string? name, string? url)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var cleanName = name.Trim().ToLowerInvariant();
            var cleanUrl = (url ?? "").Trim().ToLowerInvariant();
            foreach (var keyword in ObsoleteKeywords)
            {
                if (cleanName.Contains(keyword) || cleanUrl.Contains(keyword)) return false;
            }
            return true;
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index()
        {
            // 1. Fetch all roles
            var allRoles = await _dbContext.CRoles.OrderBy(r => r.Id).ToListAsync();

            // 2. Fetch users for calculating counts and initials
            var allUsers = await _dbContext.CApplicationUsers.ToListAsync();
            var usersByRole = allUsers
                .Where(u => u.RoleId > 0)
                .GroupBy(u => u.RoleId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 3. Fetch active valid modules
            var validModules = (await _dbContext.CModules.Where(m => m.Active != false).ToListAsync())
                .Where(m => IsValidModule(m.Name, m.Url))
                .ToList();
            var validModuleIds = validModules.Select(m => m.Id).ToHashSet();

            // 4. Fetch role-module permissions count
            var allPermissions = await _dbContext.CRoleModulesPermissions.ToListAsync();
            var permissionsByRole = allPermissions
                .Where(p => validModuleIds.Contains(p.ModuleId))
                .GroupBy(p => p.RoleId)
                .ToDictionary(g => g.Key, g => g.Select(p => p.ModuleId).Distinct().Count());

            var roleDtos = allRoles.Select(r =>
            {
                var assignedUsers = usersByRole.TryGetValue(r.Id, out var uList) ? uList : new List<ApplicationUser>();
                var names = assignedUsers.Select(u => !string.IsNullOrWhiteSpace(u.Name) ? u.Name : (u.UserName ?? $"User #{u.EmpId}")).ToList();
                var initials = names.Select(n => (n.Length > 0 ? n.Substring(0, 1) : "U").ToUpper()).ToList();

                return new TeamRoleItemDto
                {
                    Id = r.Id,
                    RoleName = r.RoleName,
                    Active = r.Active == 1,
                    CreatedOn = r.CreatedOn,
                    CreatedBy = r.CreatedBy,
                    EmployeeCount = assignedUsers.Count,
                    ModuleCount = permissionsByRole.TryGetValue(r.Id, out var count) ? count : 0,
                    EmployeeNames = names,
                    EmployeeInitials = initials
                };
            }).ToList();

            var parentModules = validModules.Where(m => m.isParentModule == 1 || m.ParentModuleId == 0).OrderBy(m => m.Order).ToList();

            var viewModel = new TeamAccessIndexViewModel
            {
                Roles = roleDtos,
                AvailableParentModules = parentModules,
                TotalRoles = allRoles.Count,
                TotalEmployees = allUsers.Count(u => u.RoleId > 0),
                TotalModules = validModules.Count,
                ActiveRolesCount = allRoles.Count(r => r.Active == 1)
            };

            return View("~/Views/TeamAccess/Index.cshtml", viewModel);
        }

        [HttpGet]
        [Route("Details/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var role = await _dbContext.CRoles.FirstOrDefaultAsync(r => r.Id == id);
            if (role == null)
            {
                TempData["ErrorMessage"] = $"Role with ID #{id} was not found.";
                return RedirectToAction(nameof(Index));
            }

            // 1. Fetch designations for display
            var designations = await _dbContext.CDesignations.ToListAsync();
            var designationMap = designations.ToDictionary(d => d.Id, d => d.Name);

            // 2. Fetch assigned employees
            var allUsers = await _dbContext.CApplicationUsers.ToListAsync();
            var assignedUsers = allUsers.Where(u => u.RoleId == id).ToList();

            var employeeDtos = assignedUsers.Select(u => new EmployeeRoleDto
            {
                Id = u.Id,
                EmpId = u.EmpId,
                Name = !string.IsNullOrWhiteSpace(u.Name) ? u.Name : (u.UserName ?? $"Emp #{u.EmpId}"),
                UserName = u.UserName ?? string.Empty,
                Email = u.Email ?? "—",
                PhoneNumber = u.PhoneNumber ?? "—",
                DesignationName = u.DesignationId > 0 && designationMap.TryGetValue(u.DesignationId, out var desig) ? desig : "—",
                RoleId = u.RoleId,
                RoleName = role.RoleName,
                Active = u.Active,
                CreatedOn = u.CreatedOn
            }).OrderBy(e => e.EmpId).ToList();

            // 3. Available employees for assignment (users not currently in this role)
            var availableEmployees = allUsers.Where(u => u.RoleId != id).Select(u => new EmployeeRoleDto
            {
                Id = u.Id,
                EmpId = u.EmpId,
                Name = !string.IsNullOrWhiteSpace(u.Name) ? u.Name : (u.UserName ?? $"Emp #{u.EmpId}"),
                UserName = u.UserName ?? string.Empty,
                Email = u.Email ?? "—",
                PhoneNumber = u.PhoneNumber ?? "—",
                DesignationName = u.DesignationId > 0 && designationMap.TryGetValue(u.DesignationId, out var desig) ? desig : "—",
                RoleId = u.RoleId,
                RoleName = "Other Role",
                Active = u.Active
            }).OrderBy(e => e.Name).ToList();

            // 4. Fetch valid active modules and permissions
            var allDbModules = await _dbContext.CModules.Where(m => m.Active != false).OrderBy(m => m.Order).ToListAsync();
            var allModules = allDbModules.Where(m => IsValidModule(m.Name, m.Url)).ToList();

            var grantedModuleIds = (await _dbContext.CRoleModulesPermissions
                .Where(p => p.RoleId == id)
                .Select(p => p.ModuleId)
                .ToListAsync())
                .ToHashSet();

            // Organize into Parent Module Groups
            var parentModules = allModules.Where(m => m.isParentModule == 1 || m.ParentModuleId == 0).OrderBy(m => m.Order).ToList();
            var childModules = allModules.Where(m => m.isSubModule == 1 || m.ParentModuleId > 0).OrderBy(m => m.Order).ToList();

            var moduleGroups = new List<ParentModuleGroupDto>();

            foreach (var parent in parentModules)
            {
                var subList = childModules.Where(c => c.ParentModuleId == parent.Id).Select(c => new ModulePermissionDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Url = c.Url ?? "#",
                    Order = c.Order,
                    isParentModule = c.isParentModule,
                    isSubModule = c.isSubModule,
                    ParentModuleId = c.ParentModuleId,
                    ParentModuleName = parent.Name,
                    IsGranted = grantedModuleIds.Contains(c.Id)
                }).ToList();

                moduleGroups.Add(new ParentModuleGroupDto
                {
                    Id = parent.Id,
                    Name = parent.Name,
                    Order = parent.Order,
                    IsGranted = grantedModuleIds.Contains(parent.Id),
                    SubModules = subList
                });
            }

            // Standalone / unparented child modules
            var mappedSubIds = moduleGroups.SelectMany(g => g.SubModules.Select(s => s.Id)).ToHashSet();
            var unparented = childModules.Where(c => !mappedSubIds.Contains(c.Id)).ToList();
            if (unparented.Any())
            {
                moduleGroups.Add(new ParentModuleGroupDto
                {
                    Id = 0,
                    Name = "General & System Modules",
                    Order = 999,
                    IsGranted = false,
                    SubModules = unparented.Select(c => new ModulePermissionDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Url = c.Url ?? "#",
                        Order = c.Order,
                        isParentModule = c.isParentModule,
                        isSubModule = c.isSubModule,
                        ParentModuleId = c.ParentModuleId,
                        ParentModuleName = "General",
                        IsGranted = grantedModuleIds.Contains(c.Id)
                    }).ToList()
                });
            }

            var roleItem = new TeamRoleItemDto
            {
                Id = role.Id,
                RoleName = role.RoleName,
                Active = role.Active == 1,
                CreatedOn = role.CreatedOn,
                CreatedBy = role.CreatedBy,
                EmployeeCount = employeeDtos.Count,
                ModuleCount = grantedModuleIds.Count,
                EmployeeNames = employeeDtos.Select(e => e.Name).ToList(),
                EmployeeInitials = employeeDtos.Select(e => (e.Name.Length > 0 ? e.Name.Substring(0, 1) : "U").ToUpper()).ToList()
            };

            var viewModel = new RoleDetailsViewModel
            {
                Role = roleItem,
                Employees = employeeDtos,
                ModuleGroups = moduleGroups,
                AvailableEmployees = availableEmployees,
                AvailableParentModules = parentModules,
                TotalSystemModules = allModules.Count,
                GrantedModulesCount = grantedModuleIds.Count
            };

            return View("~/Views/TeamAccess/Details.cshtml", viewModel);
        }

        [HttpPost]
        [Route("CreateRole")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(string roleName, bool active = true)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                TempData["ErrorMessage"] = "Role Name is required.";
                return RedirectToAction(nameof(Index));
            }

            var existing = await _dbContext.CRoles.AnyAsync(r => r.RoleName.ToLower() == roleName.Trim().ToLower());
            if (existing)
            {
                TempData["ErrorMessage"] = $"A role with the name '{roleName.Trim()}' already exists.";
                return RedirectToAction(nameof(Index));
            }

            var newRole = new Role
            {
                RoleName = roleName.Trim(),
                Active = active ? 1 : 0,
                CreatedOn = DateTime.Now,
                CreatedBy = User.Identity?.Name ?? "System"
            };

            _dbContext.CRoles.Add(newRole);
            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Role '{newRole.RoleName}' created successfully.";
            return RedirectToAction(nameof(Details), new { id = newRole.Id });
        }

        [HttpPost]
        [Route("EditRole")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(int id, string roleName, bool active)
        {
            var role = await _dbContext.CRoles.FirstOrDefaultAsync(r => r.Id == id);
            if (role == null)
            {
                TempData["ErrorMessage"] = "Role not found.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(roleName))
            {
                TempData["ErrorMessage"] = "Role Name cannot be empty.";
                return RedirectToAction(nameof(Index));
            }

            role.RoleName = roleName.Trim();
            role.Active = active ? 1 : 0;
            role.ModifiedOn = DateTime.Now;
            role.ModifiedBy = User.Identity?.Name ?? "System";

            await _dbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Role '{role.RoleName}' updated successfully.";

            return RedirectToAction(nameof(Details), new { id = role.Id });
        }

        [HttpPost]
        [Route("SavePermissions")]
        public async Task<IActionResult> SavePermissions([FromBody] SavePermissionsRequest request)
        {
            if (request == null || request.RoleId <= 0)
            {
                return Json(new { success = false, message = "Invalid Role ID or request." });
            }

            try
            {
                var role = await _dbContext.CRoles.FirstOrDefaultAsync(r => r.Id == request.RoleId);
                if (role == null)
                {
                    return Json(new { success = false, message = "Role not found." });
                }

                var moduleIds = request.ModuleIds ?? new List<int>();
                await _formPermissionService.AssignFormsToRoleAsync(request.RoleId, moduleIds);

                return Json(new
                {
                    success = true,
                    message = $"Permissions for '{role.RoleName}' saved successfully ({moduleIds.Count} modules enabled).",
                    grantedCount = moduleIds.Count
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving permissions: " + ex.Message });
            }
        }

        [HttpPost]
        [Route("AssignEmployee")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignEmployee(int roleId, string userId)
        {
            if (roleId <= 0 || string.IsNullOrWhiteSpace(userId))
            {
                TempData["ErrorMessage"] = "Invalid employee or role selection.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _dbContext.CApplicationUsers.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Employee user account not found.";
                return RedirectToAction(nameof(Details), new { id = roleId });
            }

            user.RoleId = roleId;
            user.ModifiedOn = DateTime.Now;
            user.ModifiedBy = User.Identity?.Name ?? "System";

            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Employee '{user.Name ?? user.UserName}' successfully assigned to this role.";
            return RedirectToAction(nameof(Details), new { id = roleId });
        }

        [HttpPost]
        [Route("RemoveEmployee")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveEmployee(int roleId, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["ErrorMessage"] = "Invalid employee ID.";
                return RedirectToAction(nameof(Details), new { id = roleId });
            }

            var user = await _dbContext.CApplicationUsers.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null && user.RoleId == roleId)
            {
                user.RoleId = 0;
                user.ModifiedOn = DateTime.Now;
                user.ModifiedBy = User.Identity?.Name ?? "System";
                await _dbContext.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Employee '{user.Name ?? user.UserName}' removed from this role.";
            }

            return RedirectToAction(nameof(Details), new { id = roleId });
        }

        // =========================================================================
        // DELETE ROLE
        // =========================================================================
        [HttpPost]
        [Route("DeleteRole")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRole(int id)
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid Role ID.";
                return RedirectToAction(nameof(Index));
            }

            var role = await _dbContext.CRoles.FirstOrDefaultAsync(r => r.Id == id);
            if (role == null)
            {
                TempData["ErrorMessage"] = "Role not found.";
                return RedirectToAction(nameof(Index));
            }

            // Protect essential system roles
            var protectedNames = new[] { "owner", "admin", "super admin", "superadmin" };
            if (id == 1 || protectedNames.Contains(role.RoleName.Trim().ToLower()))
            {
                TempData["ErrorMessage"] = $"The '{role.RoleName}' role is a core system role and cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // 1. Unassign all employees from this role
                var assignedUsers = await _dbContext.CApplicationUsers.Where(u => u.RoleId == id).ToListAsync();
                foreach (var user in assignedUsers)
                {
                    user.RoleId = 0;
                    user.ModifiedOn = DateTime.Now;
                    user.ModifiedBy = User.Identity?.Name ?? "System";
                }

                // 2. Remove role module permissions
                var perms = await _dbContext.CRoleModulesPermissions.Where(p => p.RoleId == id).ToListAsync();
                _dbContext.CRoleModulesPermissions.RemoveRange(perms);

                // 3. Remove role
                _dbContext.CRoles.Remove(role);
                await _dbContext.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Role '{role.RoleName}' has been permanently deleted.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to delete role: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // CREATE / ADD NEW MODULE
        // =========================================================================
        [HttpPost]
        [Route("CreateModule")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateModule(string name, string? url, int order = 0, int parentModuleId = 0, bool assignToAllRoles = false, bool active = true, int? returnRoleId = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "Module Name is required.";
                if (returnRoleId.HasValue && returnRoleId.Value > 0)
                    return RedirectToAction(nameof(Details), new { id = returnRoleId.Value });
                return RedirectToAction(nameof(Index));
            }

            name = name.Trim();
            url = string.IsNullOrWhiteSpace(url) ? "#" : url.Trim().TrimStart('/');

            var duplicate = await _dbContext.CModules.AnyAsync(m => m.Name.ToLower() == name.ToLower() || (url != "#" && m.Url != null && m.Url.ToLower() == url.ToLower()));
            if (duplicate)
            {
                TempData["ErrorMessage"] = $"A module with name '{name}' or URL '{url}' already exists.";
                if (returnRoleId.HasValue && returnRoleId.Value > 0)
                    return RedirectToAction(nameof(Details), new { id = returnRoleId.Value });
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var maxId = await _dbContext.CModules.MaxAsync(m => (int?)m.Id) ?? 0;
                int newId = maxId + 1;

                if (order <= 0)
                {
                    order = (await _dbContext.CModules.MaxAsync(m => (int?)m.Order) ?? 0) + 1;
                }

                int isParent = parentModuleId > 0 ? 0 : 1;
                int isSub = parentModuleId > 0 ? 1 : 0;

                await _dbContext.Database.OpenConnectionAsync();
                try
                {
                    await _dbContext.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[CModules] ON");
                    await _dbContext.Database.ExecuteSqlRawAsync(
                        @"INSERT INTO [dbo].[CModules] ([Id], [Name], [Url], [Order], [isParentModule], [isSubModule], [ParentModuleId], [Active], [CreatedOn], [CreatedBy])
                          VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9})",
                        newId, name, url, order, isParent, isSub, parentModuleId, active ? 1 : 0, DateTime.Now, User.Identity?.Name ?? "System");
                    await _dbContext.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[CModules] OFF");
                }
                finally
                {
                    await _dbContext.Database.CloseConnectionAsync();
                }

                if (assignToAllRoles)
                {
                    var roleIds = await _dbContext.CRoles.Select(r => r.Id).ToListAsync();
                    foreach (var rId in roleIds)
                    {
                        await _dbContext.CRoleModulesPermissions.AddAsync(new RoleModulesPermission
                        {
                            RoleId = rId,
                            ModuleId = newId
                        });
                    }
                    await _dbContext.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = $"Module '{name}' created successfully{(assignToAllRoles ? " and access granted to all roles" : "")}.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to create module: " + ex.Message;
            }

            if (returnRoleId.HasValue && returnRoleId.Value > 0)
                return RedirectToAction(nameof(Details), new { id = returnRoleId.Value });

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // DELETE MODULE
        // =========================================================================
        [HttpPost]
        [Route("DeleteModule")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteModule(int id, int? returnRoleId = null)
        {
            var module = await _dbContext.CModules.FirstOrDefaultAsync(m => m.Id == id);
            if (module == null)
            {
                TempData["ErrorMessage"] = "Module not found.";
                if (returnRoleId.HasValue && returnRoleId.Value > 0)
                    return RedirectToAction(nameof(Details), new { id = returnRoleId.Value });
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Remove permissions
                var perms = await _dbContext.CRoleModulesPermissions.Where(p => p.ModuleId == id).ToListAsync();
                _dbContext.CRoleModulesPermissions.RemoveRange(perms);

                // Remove module
                _dbContext.CModules.Remove(module);
                await _dbContext.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Module '{module.Name}' deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to delete module: " + ex.Message;
            }

            if (returnRoleId.HasValue && returnRoleId.Value > 0)
                return RedirectToAction(nameof(Details), new { id = returnRoleId.Value });

            return RedirectToAction(nameof(Index));
        }
    }

    public class SavePermissionsRequest
    {
        public int RoleId { get; set; }
        public List<int> ModuleIds { get; set; } = new();
    }
}
