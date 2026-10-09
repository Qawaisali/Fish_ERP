using ERP_Core.Attributes;
using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace ERP_Core.Controllers.UserManagement
{
    [Authorize]
    [FormAccessAuthorize("Register User")]
    [Route("user")]
    public class UserController : Controller
    {
        private readonly ERPDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<UserController> _logger;

        public UserController(
            ERPDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            ILogger<UserController> logger)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async System.Threading.Tasks.Task<IActionResult> Index()
        {
            return await Register();
        }

        [HttpGet]
        [Route("Register")]
        public async System.Threading.Tasks.Task<IActionResult> Register()
        {
            ViewData["Title"] = "Register Employee";
            ViewData["BreadcrumbPrefix"] = "USER MANAGEMENT";
            ViewData["BreadcrumbCurrent"] = "Register Employee";
            ViewData["ActivePage"] = "RegisterEmployee";

            var viewModel = new RegisterViewModel();
            await PopulateDropdownsAsync(viewModel);

            // Suggest the next available Employee ID
            int maxEmpId = await _dbContext.CApplicationUsers.AnyAsync()
                ? await _dbContext.CApplicationUsers.MaxAsync(u => u.EmpId)
                : 1000;
            viewModel.Form.EmpId = maxEmpId + 1;

            return View("Register", viewModel);
        }

        [HttpPost]
        [Route("Register")]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> Register(RegisterViewModel model)
        {
            ViewData["Title"] = "Register Employee";
            ViewData["BreadcrumbPrefix"] = "USER MANAGEMENT";
            ViewData["BreadcrumbCurrent"] = "Register Employee";
            ViewData["ActivePage"] = "RegisterEmployee";

            if (model?.Form == null)
            {
                model = new RegisterViewModel();
                await PopulateDropdownsAsync(model);
                ModelState.AddModelError(string.Empty, "Invalid registration form submission.");
                return View("Register", model);
            }

            // 1. Check if Email already exists
            if (await _userManager.FindByEmailAsync(model.Form.Email) != null ||
                await _dbContext.CApplicationUsers.AnyAsync(u => u.Email == model.Form.Email))
            {
                ModelState.AddModelError("Form.Email", "A user with this email address already exists.");
            }

            // 2. Check if Username already exists
            if (await _userManager.FindByNameAsync(model.Form.UserName) != null ||
                await _dbContext.CApplicationUsers.AnyAsync(u => u.UserName == model.Form.UserName))
            {
                ModelState.AddModelError("Form.UserName", "A user with this username already exists.");
            }

            // 3. Handle Employee ID
            int empId = model.Form.EmpId ?? 0;
            if (empId > 0)
            {
                if (await _dbContext.CApplicationUsers.AnyAsync(u => u.EmpId == empId))
                {
                    ModelState.AddModelError("Form.EmpId", $"Employee ID {empId} is already in use.");
                }
            }
            else
            {
                int maxId = await _dbContext.CApplicationUsers.AnyAsync()
                    ? await _dbContext.CApplicationUsers.MaxAsync(u => u.EmpId)
                    : 1000;
                empId = maxId + 1;
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model);
                return View("Register", model);
            }

            // 4. Create New ApplicationUser
            var user = new ApplicationUser
            {
                UserName = model.Form.UserName.Trim(),
                Email = model.Form.Email.Trim(),
                Name = model.Form.Name.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(model.Form.PhoneNumber) ? null : model.Form.PhoneNumber.Trim(),
                CompanyId = model.Form.CompanyId,
                RoleId = model.Form.RoleId,
                EmpId = empId,
                DesignationId = model.Form.DesignationId > 0 ? model.Form.DesignationId : 1,
                DepartmenId = model.Form.DepartmenId > 0 ? model.Form.DepartmenId : 1,
                Active = model.Form.Active,
                CreatedOn = DateTime.Now,
                CreatedBy = User?.Identity?.Name ?? "System"
            };

            var result = await _userManager.CreateAsync(user, model.Form.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("Employee {Name} (EmpId: {EmpId}) registered successfully by {User}",
                    user.Name, user.EmpId, User?.Identity?.Name ?? "Anonymous");

                TempData["SuccessMessage"] = $"Employee '{user.Name}' (Emp ID: {user.EmpId}) registered successfully!";
                return RedirectToAction(nameof(Register));
            }

            // Add Identity error messages
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await PopulateDropdownsAsync(model);
            return View("Register", model);
        }

        private async System.Threading.Tasks.Task PopulateDropdownsAsync(RegisterViewModel model)
        {
            model.Companies = await _dbContext.CCompanies
                .Where(c => c.Active != false)
                .OrderBy(c => c.CompanyName)
                .Select(c => new CompanyItemDto
                {
                    Id = c.CompanyId,
                    Name = c.CompanyName ?? "Unnamed Company"
                })
                .ToListAsync();

            model.Roles = await _dbContext.CRoles
                .Where(r => r.Active == 1)
                .OrderBy(r => r.RoleName)
                .Select(r => new RoleItemDto
                {
                    Id = r.Id,
                    RoleName = r.RoleName ?? "Unnamed Role"
                })
                .ToListAsync();

            model.Designations = await _dbContext.CDesignations
                .Where(d => d.Active == true || d.Active == null)
                .OrderBy(d => d.Name)
                .Select(d => new DesignationItemDto
                {
                    Id = d.Id,
                    Name = d.Name ?? "Unnamed Designation"
                })
                .ToListAsync();

            model.Departments = await _dbContext.CDepartments
                .Where(d => d.Active == true || d.Active == null)
                .OrderBy(d => d.Name)
                .Select(d => new DepartmentItemDto
                {
                    Id = d.Id,
                    Name = d.Name ?? "Unnamed Department"
                })
                .ToListAsync();
        }
    }
}
