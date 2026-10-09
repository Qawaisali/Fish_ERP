using ERP_Core.DataAccessLayer;
using ERP_Core.Dtos;
using ERP_Core.Models;
using ERP_Core.Services.IServices;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ERP_Core.Controllers.Login
{
    public class LoginController : Controller
    {
        private readonly ILoginService _loginService;
        private readonly ModulesDAL _modulesDal;
        private readonly ILogger<LoginController> _logger;

        public LoginController(ILoginService loginService, ModulesDAL modulesDal, ILogger<LoginController> logger)
        {
            _loginService = loginService;
            _modulesDal = modulesDal;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            var viewModel = new LoginViewModel
            {
                Employees = await _loginService.GetEmployeesAsync(),
                Companies = await _loginService.GetCompaniesAsync(),
                FinancialYears = await _loginService.GetAllFinancialYearsAsync(),
                ReturnUrl = returnUrl
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> QuickLogin(string role, string username, string? password = null, string? returnUrl = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    TempData["ErrorMessage"] = "Please enter both username and password.";
                    return RedirectToAction("Index", new { returnUrl });
                }

                var isOwner = string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase);
                var searchInput = username.Trim();
                var passInput = password;

                var user = isOwner 
                    ? await _loginService.VerifyAdminCredentialsAsync(searchInput, passInput)
                    : await _loginService.VerifyEmployeeCredentialsAsync(searchInput, passInput);

                if (user == null)
                {
                    // Secondary attempt in case entered via opposite tab
                    user = await _loginService.VerifyAdminCredentialsAsync(searchInput, passInput)
                        ?? await _loginService.VerifyEmployeeCredentialsAsync(searchInput, passInput);
                }

                if (user == null)
                {
                    TempData["ErrorMessage"] = $"Invalid credentials for '{searchInput}'. Please check your username and password.";
                    return RedirectToAction("Index", new { returnUrl });
                }

                var empIdStr = user.EmpId > 0 ? user.EmpId.ToString() : user.Id;
                var roleVerification = await _modulesDal.VerifyUserRoleByEmpIdAsync(user.EmpId);
                var roleId = roleVerification.IsValid ? roleVerification.RoleId : (user.RoleId > 0 ? user.RoleId : (isOwner ? 1 : 2));
                var roleName = roleVerification.IsValid ? roleVerification.RoleName : (roleId == 1 ? "Owner" : "Staff");
                var companyId = user.CompanyId > 0 ? user.CompanyId : 1;
                
                var financialYears = await _loginService.GetFinancialYearsByCompanyIdAsync(companyId);
                var activeFy = financialYears?.FirstOrDefault()?.Id ?? 1;

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, empIdStr),
                    new Claim("EmpId", empIdStr),
                    new Claim("IdentityId", user.Id),
                    new Claim(ClaimTypes.Name, user.Name ?? user.UserName ?? roleName),
                    new Claim(ClaimTypes.Email, user.Email ?? ""),
                    new Claim("CompanyId", companyId.ToString()),
                    new Claim("FinancialYearId", activeFy.ToString()),
                    new Claim("RoleId", roleId.ToString()),
                    new Claim(ClaimTypes.Role, roleName)
                };

                if (roleName.Equals("Owner", StringComparison.OrdinalIgnoreCase) || roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase) || roleId == 1)
                {
                    claims.Add(new Claim(ClaimTypes.Role, "Admin"));
                }

                var identity = new ClaimsIdentity(claims, "CustomScheme");
                var principal = new ClaimsPrincipal(identity);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                };

                await HttpContext.SignInAsync("CustomScheme", principal, authProperties);

                HttpContext.Session.SetInt32("CompanyId", companyId);
                HttpContext.Session.SetInt32("FinancialYearId", activeFy);
                HttpContext.Session.SetString("EmpId", empIdStr);
                HttpContext.Session.SetString("UserId", empIdStr);
                HttpContext.Session.SetInt32("RoleId", roleId);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in quick login");
                TempData["ErrorMessage"] = "Failed to sign in. Please try again.";
                return RedirectToAction("Index", new { returnUrl });
            }
        }

        [HttpPost]
        public async Task<IActionResult> VerifyAdminCredentials([FromBody] AdminVerifyDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Json(new { success = false, message = errors });
            }

            try
            {
                var user = await _loginService.VerifyAdminCredentialsAsync(dto.Email, dto.Password);
                if (user == null)
                {
                    return Json(new { success = false, message = "Invalid email or password." });
                }

                var empIdStr = user.EmpId > 0 ? user.EmpId.ToString() : user.Id;
                var roleVerification = await _modulesDal.VerifyUserRoleByEmpIdAsync(user.EmpId);
                var adminRoleId = roleVerification.IsValid ? roleVerification.RoleId : (user.RoleId > 0 ? user.RoleId : 1);
                var adminRoleName = roleVerification.IsValid ? roleVerification.RoleName : "Owner";

                HttpContext.Session.SetString("PreAuth_AdminId", user.Id);
                HttpContext.Session.SetString("PreAuth_AdminEmpId", empIdStr);
                HttpContext.Session.SetString("PreAuth_AdminName", user.Name ?? user.UserName ?? "Admin");
                HttpContext.Session.SetString("PreAuth_AdminEmail", user.Email ?? "");
                HttpContext.Session.SetInt32("PreAuth_AdminRoleId", adminRoleId);
                HttpContext.Session.SetString("PreAuth_AdminRoleName", adminRoleName);
                HttpContext.Session.SetString("PreAuth_Timestamp", DateTime.UtcNow.Ticks.ToString());

                var companies = await _loginService.GetCompaniesAsync();
                var financialYears = await _loginService.GetAllFinancialYearsAsync();

                return Json(new
                {
                    success = true,
                    userId = user.Id,
                    identityId = user.Id,
                    userName = user.Name ?? user.UserName ?? "Admin",
                    companies = companies,
                    financialYears = financialYears
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying admin credentials");
                return Json(new { success = false, message = "An error occurred during verification. Please try again." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteAdminLogin([FromForm] AdminWorkspaceDto dto)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please select both Company and Financial Year.";
                return RedirectToAction("Index", new { returnUrl = dto.ReturnUrl });
            }

            try
            {
                var preAuthId = HttpContext.Session.GetString("PreAuth_AdminId");
                var preAuthTimestampStr = HttpContext.Session.GetString("PreAuth_Timestamp");

                if (string.IsNullOrEmpty(preAuthId) || string.IsNullOrEmpty(preAuthTimestampStr))
                {
                    TempData["ErrorMessage"] = "Your session expired. Please enter your credentials again.";
                    return RedirectToAction("Index", new { returnUrl = dto.ReturnUrl });
                }

                var adminEmpId = HttpContext.Session.GetString("PreAuth_AdminEmpId") ?? "1";
                var adminName = HttpContext.Session.GetString("PreAuth_AdminName") ?? "Owner";
                var adminEmail = HttpContext.Session.GetString("PreAuth_AdminEmail") ?? "";
                var adminRoleId = HttpContext.Session.GetInt32("PreAuth_AdminRoleId") ?? 1;
                var adminRoleName = HttpContext.Session.GetString("PreAuth_AdminRoleName") ?? "Owner";

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, adminEmpId),
                    new Claim("EmpId", adminEmpId),
                    new Claim("IdentityId", preAuthId),
                    new Claim(ClaimTypes.Name, adminName),
                    new Claim(ClaimTypes.Email, adminEmail),
                    new Claim("CompanyId", dto.CompanyId.ToString()),
                    new Claim("FinancialYearId", dto.FinancialYearId.ToString()),
                    new Claim("RoleId", adminRoleId.ToString()),
                    new Claim(ClaimTypes.Role, adminRoleName)
                };

                if (adminRoleName.Equals("Owner", StringComparison.OrdinalIgnoreCase) || adminRoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase) || adminRoleId == 1)
                {
                    claims.Add(new Claim(ClaimTypes.Role, "Admin"));
                }

                var identity = new ClaimsIdentity(claims, "CustomScheme");
                var principal = new ClaimsPrincipal(identity);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = dto.RememberMe,
                    ExpiresUtc = dto.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(8)
                };

                await HttpContext.SignInAsync("CustomScheme", principal, authProperties);

                HttpContext.Session.SetInt32("CompanyId", dto.CompanyId);
                HttpContext.Session.SetInt32("FinancialYearId", dto.FinancialYearId);
                HttpContext.Session.SetString("EmpId", adminEmpId);
                HttpContext.Session.SetString("UserId", adminEmpId);
                HttpContext.Session.SetInt32("RoleId", adminRoleId);

                if (!string.IsNullOrEmpty(dto.ReturnUrl) && Url.IsLocalUrl(dto.ReturnUrl))
                {
                    return Redirect(dto.ReturnUrl);
                }

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing admin login");
                TempData["ErrorMessage"] = "Failed to sign in. Please try again.";
                return RedirectToAction("Index", new { returnUrl = dto.ReturnUrl });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EmployeeLogin([FromForm] EmployeeLoginDto dto)
        {
            TempData["ActiveTab"] = "employee";
            TempData["SelectedUserId"] = dto.UserId;

            if (string.IsNullOrEmpty(dto.UserId) || string.IsNullOrEmpty(dto.Password))
            {
                TempData["ErrorMessage"] = "Please select an employee and enter password.";
                return RedirectToAction("Index", new { returnUrl = dto.ReturnUrl, tab = "employee" });
            }

            try
            {
                var user = await _loginService.VerifyEmployeeCredentialsAsync(dto.UserId, dto.Password);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "Invalid password for selected employee.";
                    return RedirectToAction("Index", new { returnUrl = dto.ReturnUrl, tab = "employee" });
                }

                var empIdStr = user.EmpId > 0 ? user.EmpId.ToString() : user.Id;
                var roleVerification = await _modulesDal.VerifyUserRoleByEmpIdAsync(user.EmpId);
                var roleId = roleVerification.IsValid ? roleVerification.RoleId : (user.RoleId > 0 ? user.RoleId : 2);
                var roleName = roleVerification.IsValid ? roleVerification.RoleName : "Staff";

                var financialYears = await _loginService.GetFinancialYearsByCompanyIdAsync(user.CompanyId);
                var activeFy = financialYears?.FirstOrDefault()?.Id ?? 1;

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, empIdStr),
                    new Claim("EmpId", empIdStr),
                    new Claim("IdentityId", user.Id),
                    new Claim(ClaimTypes.Name, user.Name ?? user.UserName ?? roleName),
                    new Claim(ClaimTypes.Email, user.Email ?? ""),
                    new Claim("CompanyId", user.CompanyId.ToString()),
                    new Claim("FinancialYearId", activeFy.ToString()),
                    new Claim("RoleId", roleId.ToString()),
                    new Claim(ClaimTypes.Role, roleName)
                };

                if (roleName.Equals("Owner", StringComparison.OrdinalIgnoreCase) || roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase) || roleId == 1)
                {
                    claims.Add(new Claim(ClaimTypes.Role, "Admin"));
                }

                var identity = new ClaimsIdentity(claims, "CustomScheme");
                var principal = new ClaimsPrincipal(identity);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = dto.RememberMe,
                    ExpiresUtc = dto.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(8)
                };

                await HttpContext.SignInAsync("CustomScheme", principal, authProperties);

                HttpContext.Session.SetInt32("CompanyId", user.CompanyId);
                HttpContext.Session.SetInt32("FinancialYearId", activeFy);
                HttpContext.Session.SetString("EmpId", empIdStr);
                HttpContext.Session.SetString("UserId", empIdStr);
                HttpContext.Session.SetInt32("RoleId", user.RoleId);

                if (!string.IsNullOrEmpty(dto.ReturnUrl) && Url.IsLocalUrl(dto.ReturnUrl))
                {
                    return Redirect(dto.ReturnUrl);
                }

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in employee login");
                TempData["ErrorMessage"] = "An error occurred during employee login.";
                return RedirectToAction("Index", new { returnUrl = dto.ReturnUrl, tab = "employee" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("CustomScheme");
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Login");
        }
    }
}
