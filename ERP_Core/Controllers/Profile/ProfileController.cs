using System.Security.Claims;
using ERP_Core.Attributes;
using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP_Core.Controllers.Profile
{
    [Authorize]
    [FormAccessAuthorize("My Profile")]
    [Route("Profile")]
    public class ProfileController : Controller
    {
        private readonly ERPDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(
            ERPDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            ILogger<ProfileController> logger)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _logger = logger;
        }

        private async Task<ApplicationUser?> GetCurrentApplicationUserAsync()
        {
            var identityId = User.FindFirst("IdentityId")?.Value;
            if (!string.IsNullOrEmpty(identityId))
            {
                var user = await _userManager.FindByIdAsync(identityId);
                if (user != null) return user;
            }

            var empIdClaim = User.FindFirst("EmpId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(empIdClaim, out int empId) && empId > 0)
            {
                var user = await _dbContext.CApplicationUsers.FirstOrDefaultAsync(u => u.EmpId == empId);
                if (user != null) return user;
            }

            var username = User.Identity?.Name;
            if (!string.IsNullOrEmpty(username))
            {
                return await _userManager.FindByNameAsync(username)
                    ?? await _dbContext.CApplicationUsers.FirstOrDefaultAsync(u => u.UserName == username || u.Email == username);
            }

            return null;
        }

        private async Task<ProfileViewModel> BuildProfileViewModelAsync(ApplicationUser user)
        {
            // 1. Resolve Company Name
            string companyName = "Not Assigned";
            if (user.CompanyId > 0)
            {
                var company = await _dbContext.CCompanies.FirstOrDefaultAsync(c => c.CompanyId == user.CompanyId);
                if (company != null && !string.IsNullOrWhiteSpace(company.CompanyName))
                {
                    companyName = company.CompanyName;
                }
            }

            // 2. Resolve Role Name
            string roleName = "Employee";
            if (user.RoleId > 0)
            {
                var role = await _dbContext.CRoles.FirstOrDefaultAsync(r => r.Id == user.RoleId);
                if (role != null && !string.IsNullOrWhiteSpace(role.RoleName))
                {
                    roleName = role.RoleName;
                }
            }

            // 3. Resolve Designation Name
            string designationName = "General Staff";
            if (user.DesignationId > 0)
            {
                var designation = await _dbContext.CDesignations.FirstOrDefaultAsync(d => d.Id == user.DesignationId);
                if (designation != null && !string.IsNullOrWhiteSpace(designation.Name))
                {
                    designationName = designation.Name;
                }
            }

            // 4. Resolve Department Name
            string departmentName = "General Department";
            if (user.DepartmenId > 0)
            {
                var department = await _dbContext.CDepartments.FirstOrDefaultAsync(d => d.Id == user.DepartmenId);
                if (department != null && !string.IsNullOrWhiteSpace(department.Name))
                {
                    departmentName = department.Name;
                }
            }

            // 5. Resolve Accessible Modules
            var accessibleModules = new List<string>();
            try
            {
                if (user.RoleId > 0)
                {
                    var moduleIds = await _dbContext.CRoleModulesPermissions
                        .Where(p => p.RoleId == user.RoleId)
                        .Select(p => p.ModuleId)
                        .ToListAsync();

                    if (moduleIds.Any())
                    {
                        accessibleModules = await _dbContext.CModules
                            .Where(m => moduleIds.Contains(m.Id) && (m.Active != false))
                            .OrderBy(m => m.Order)
                            .Select(m => m.Name)
                            .ToListAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch accessible modules for role {RoleId}", user.RoleId);
            }

            return new ProfileViewModel
            {
                UserId = user.Id,
                EmpId = user.EmpId,
                Name = user.Name ?? user.UserName ?? "User",
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                CompanyId = user.CompanyId,
                CompanyName = companyName,
                RoleId = user.RoleId,
                RoleName = roleName,
                DesignationId = user.DesignationId,
                DesignationName = designationName,
                DepartmentId = user.DepartmenId,
                DepartmentName = departmentName,
                Active = user.Active,
                CreatedOn = user.CreatedOn,
                ModifiedOn = user.ModifiedOn,
                AccessibleModules = accessibleModules,
                UpdateForm = new UpdateProfileDto
                {
                    Name = user.Name ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    PhoneNumber = user.PhoneNumber ?? string.Empty
                }
            };
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index()
        {
            var user = await GetCurrentApplicationUserAsync();
            if (user == null)
            {
                TempData["ErrorMessage"] = "Unable to resolve user session profile.";
                return RedirectToAction("Index", "Home");
            }

            var viewModel = await BuildProfileViewModelAsync(user);
            return View("~/Views/Profile/Index.cshtml", viewModel);
        }

        [HttpPost]
        [Route("UpdateProfile")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileDto dto)
        {
            var user = await GetCurrentApplicationUserAsync();
            if (user == null)
            {
                TempData["ErrorMessage"] = "Session expired or user not found.";
                return RedirectToAction("Index", "Login");
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please provide valid profile details.";
                var vm = await BuildProfileViewModelAsync(user);
                vm.UpdateForm = dto;
                return View("~/Views/Profile/Index.cshtml", vm);
            }

            if (!string.Equals(user.Email, dto.Email.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var existingUser = await _userManager.FindByEmailAsync(dto.Email.Trim());
                if (existingUser != null && existingUser.Id != user.Id)
                {
                    TempData["ErrorMessage"] = "The specified email address is already associated with another account.";
                    var vm = await BuildProfileViewModelAsync(user);
                    vm.UpdateForm = dto;
                    return View("~/Views/Profile/Index.cshtml", vm);
                }
                user.Email = dto.Email.Trim();
                user.NormalizedEmail = dto.Email.Trim().ToUpper();
            }

            user.Name = dto.Name.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();
            user.ModifiedOn = DateTime.Now;
            user.ModifiedBy = User.Identity?.Name ?? "User";

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Your profile information has been updated successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = string.Join("; ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
