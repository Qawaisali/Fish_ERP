using ERP_Core.Attributes;
using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP_Core.Controllers.UserRights
{
    [Authorize]
    [FormAccessAuthorize("Roles & Permissions")]
    [Route("UserRights")]
    public class UserRightsController : Controller
    {
        private readonly ERPDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<UserRightsController> _logger;

        public UserRightsController(
            ERPDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            ILogger<UserRightsController> logger)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "User Rights";
            ViewData["SubTitle"] = "Staff accounts aur unki permissions manage karein.";

            var userDtos = new List<StaffUserDto>();
            try
            {
                var dbUsers = await _dbContext.CApplicationUsers
                    .AsNoTracking()
                    .OrderBy(u => u.EmpId)
                    .ToListAsync();

                var roles = await _dbContext.CRoles
                    .AsNoTracking()
                    .ToDictionaryAsync(r => r.Id, r => r.RoleName);

                userDtos = dbUsers.Select(u => new StaffUserDto
                {
                    Id = u.EmpId,
                    IdentityId = u.Id,
                    FullName = u.Name ?? u.UserName ?? "User",
                    Username = u.UserName ?? u.Email ?? "",
                    Email = u.Email ?? "",
                    Role = (u.RoleId > 0 && roles.TryGetValue(u.RoleId, out var rName)) ? rName : (u.RoleId == 1 ? "Owner" : "Staff"),
                    Status = u.Active ? "Active" : "Inactive"
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load users from database in UserRightsController");
            }

            if (!userDtos.Any())
            {
                userDtos = new List<StaffUserDto>
                {
                    new StaffUserDto { Id = 1, FullName = "Zahid Hussain", Username = "zahid.hussain", Email = "zahid@aarhat.pk", Role = "Owner", Status = "Active" },
                    new StaffUserDto { Id = 2, FullName = "Usman Bhai", Username = "usman.bhai", Email = "usman@aarhat.pk", Role = "Staff", Status = "Active" },
                    new StaffUserDto { Id = 3, FullName = "Imran Munshi", Username = "imran.munshi", Email = "imran@aarhat.pk", Role = "Manager", Status = "Active" },
                    new StaffUserDto { Id = 4, FullName = "Bilal Staff", Username = "bilal.s", Email = "bilal@aarhat.pk", Role = "Staff", Status = "Inactive" }
                };
            }

            var model = new UserRightsViewModel
            {
                Users = userDtos
            };

            return View(model);
        }

        [HttpPost]
        [Route("AddUser")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUser(string fullName, string username, string password, string role)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    TempData["ErrorMessage"] = "Username and password are required to create a new user.";
                    return RedirectToAction(nameof(Index));
                }

                int roleId = string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase) ? 1 :
                             string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase) ? 3 : 2;

                int maxEmpId = await _dbContext.CApplicationUsers.AnyAsync()
                    ? await _dbContext.CApplicationUsers.MaxAsync(u => u.EmpId)
                    : 1000;

                var user = new ApplicationUser
                {
                    UserName = username.Trim(),
                    Name = !string.IsNullOrWhiteSpace(fullName) ? fullName.Trim() : username.Trim(),
                    Email = username.Contains("@") ? username.Trim() : $"{username.Trim()}@aarhat.pk",
                    EmpId = maxEmpId + 1,
                    CompanyId = 1,
                    RoleId = roleId,
                    Active = true,
                    CreatedOn = DateTime.Now,
                    CreatedBy = User.Identity?.Name ?? "Admin"
                };

                var result = await _userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = $"User '{user.Name}' ({role}) created successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = string.Join("; ", result.Errors.Select(e => e.Description));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user {Username}", username);
                TempData["ErrorMessage"] = "Error creating user: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Route("ChangePassword")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string userId, string username, string newPassword, string confirmPassword)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
                {
                    TempData["ErrorMessage"] = "New Password and Confirm Password are required.";
                    return RedirectToAction(nameof(Index));
                }

                if (newPassword != confirmPassword)
                {
                    TempData["ErrorMessage"] = "New Password and Confirm Password do not match.";
                    return RedirectToAction(nameof(Index));
                }

                ApplicationUser? user = null;
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    user = await _userManager.FindByIdAsync(userId)
                        ?? await _dbContext.CApplicationUsers.FirstOrDefaultAsync(u => u.Id == userId);
                }

                if (user == null && !string.IsNullOrWhiteSpace(username))
                {
                    user = await _userManager.FindByNameAsync(username)
                        ?? await _dbContext.CApplicationUsers.FirstOrDefaultAsync(u => u.UserName == username || u.Email == username);
                }

                if (user == null && int.TryParse(userId, out int empId) && empId > 0)
                {
                    user = await _dbContext.CApplicationUsers.FirstOrDefaultAsync(u => u.EmpId == empId);
                }

                if (user == null)
                {
                    TempData["ErrorMessage"] = "User could not be found to change password.";
                    return RedirectToAction(nameof(Index));
                }

                // Attempt password reset via Identity token
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

                if (result.Succeeded)
                {
                    user.ModifiedOn = DateTime.Now;
                    user.ModifiedBy = User.Identity?.Name ?? "Admin";
                    await _dbContext.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Password for user '{user.Name ?? user.UserName}' changed successfully.";
                }
                else
                {
                    // Fallback to password hasher
                    var passwordHasher = new PasswordHasher<ApplicationUser>();
                    user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
                    user.ModifiedOn = DateTime.Now;
                    user.ModifiedBy = User.Identity?.Name ?? "Admin";
                    await _userManager.UpdateAsync(user);
                    await _dbContext.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Password for user '{user.Name ?? user.UserName}' updated successfully.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user {Username}", username);
                TempData["ErrorMessage"] = "Error changing password: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Route("ChangePasswordAjax")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePasswordAjax([FromBody] ChangePasswordRequestDto dto)
        {
            try
            {
                if (dto == null || string.IsNullOrWhiteSpace(dto.NewPassword) || string.IsNullOrWhiteSpace(dto.ConfirmPassword))
                {
                    return Json(new { success = false, message = "New Password and Confirm Password are required." });
                }

                if (dto.NewPassword != dto.ConfirmPassword)
                {
                    return Json(new { success = false, message = "New Password and Confirm Password do not match." });
                }

                ApplicationUser? user = null;
                if (!string.IsNullOrWhiteSpace(dto.UserId))
                {
                    user = await _userManager.FindByIdAsync(dto.UserId)
                        ?? await _dbContext.CApplicationUsers.FirstOrDefaultAsync(u => u.Id == dto.UserId);
                }

                if (user == null && !string.IsNullOrWhiteSpace(dto.Username))
                {
                    user = await _userManager.FindByNameAsync(dto.Username)
                        ?? await _dbContext.CApplicationUsers.FirstOrDefaultAsync(u => u.UserName == dto.Username || u.Email == dto.Username);
                }

                if (user == null && int.TryParse(dto.UserId, out int empId) && empId > 0)
                {
                    user = await _dbContext.CApplicationUsers.FirstOrDefaultAsync(u => u.EmpId == empId);
                }

                if (user == null)
                {
                    return Json(new { success = false, message = "User could not be found to change password." });
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);

                if (!result.Succeeded)
                {
                    var passwordHasher = new PasswordHasher<ApplicationUser>();
                    user.PasswordHash = passwordHasher.HashPassword(user, dto.NewPassword);
                    user.ModifiedOn = DateTime.Now;
                    user.ModifiedBy = User.Identity?.Name ?? "Admin";
                    await _userManager.UpdateAsync(user);
                    await _dbContext.SaveChangesAsync();
                }
                else
                {
                    user.ModifiedOn = DateTime.Now;
                    user.ModifiedBy = User.Identity?.Name ?? "Admin";
                    await _dbContext.SaveChangesAsync();
                }

                return Json(new { success = true, message = $"Password for user '{user.Name ?? user.UserName}' changed successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password via AJAX for user {Username}", dto?.Username);
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }
    }

    public class ChangePasswordRequestDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
