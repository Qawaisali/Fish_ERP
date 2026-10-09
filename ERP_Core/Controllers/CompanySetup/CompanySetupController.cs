using ERP_Core.Attributes;
using ERP_Core.Data;
using ERP_Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ERP_Core.Controllers.CompanySetup
{
    [Authorize]
    [FormAccessAuthorize("CompanySetup")]
    [Route("CompanySetup")]
    [Route("company-setup")]
    [Route("Company")]
    public class CompanySetupController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<CompanySetupController> _logger;

        public CompanySetupController(ERPDbContext context, ILogger<CompanySetupController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Company Setup";
            ViewData["BreadcrumbPrefix"] = "SYSTEM";
            ViewData["BreadcrumbCurrent"] = "Company Setup";
            ViewData["ActivePage"] = "CompanySetup";

            int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 0;
            if (companyId == 0)
            {
                var claimCompanyId = User.FindFirst("CompanyId")?.Value;
                int.TryParse(claimCompanyId, out companyId);
            }

            if (companyId <= 0)
            {
                TempData["ErrorMessage"] = "No active company workspace found.";
                return RedirectToAction("Index", "Login");
            }

            Company? company = await _context.CCompanies.FirstOrDefaultAsync(c => c.CompanyId == companyId);

            //if (company == null)
            //{
            //    company = new Company
            //    {
            //        CompanyId = companyId,
            //        Code = "00",
            //        CompanyName = "Company Profile",
            //        Phone = "",
            //        Mobile = "",
            //        EmailId = "",
            //        Country = "Pakistan",
            //        State = "Sindh"
            //    };
            //}

            ViewBag.ParentCompanies = await _context.CCompanies
                .Where(c => company == null || c.CompanyId != company.CompanyId)
                .ToListAsync();

            return View(company);
        }

        [HttpPost]
        [Route("Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Company model)
        {
            int sessionCompanyId = HttpContext.Session.GetInt32("CompanyId") ?? 0;
            if (sessionCompanyId > 0 && model.CompanyId != sessionCompanyId && !User.IsInRole("Admin") && !User.HasClaim(System.Security.Claims.ClaimTypes.Role, "Admin"))
            {
                TempData["ErrorMessage"] = "Unauthorized to modify this company profile.";
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.CCompanies.FirstOrDefaultAsync(c => c.CompanyId == model.CompanyId);
                    if (existing != null)
                    {
                        existing.CompanyName = model.CompanyName;
                        existing.ParentId = model.ParentId;
                        existing.Address = model.Address;
                        existing.Phone = model.Phone;
                        existing.Mobile = model.Mobile;
                        existing.EmailId = model.EmailId;
                        existing.Country = model.Country;
                        existing.State = model.State;
                        existing.Prefix = model.Prefix;
                        existing.Logo = model.Logo;
                        existing.ModifiedOn = DateTime.Now;
                        existing.ModifiedBy = User.Identity?.Name;

                        _context.CCompanies.Update(existing);
                        await _context.SaveChangesAsync();

                        TempData["SuccessMessage"] = "Company profile updated successfully.";
                    }
                    else
                    {
                        model.CreatedOn = DateTime.Now;
                        model.CreatedBy = User.Identity?.Name;
                        await _context.CCompanies.AddAsync(model);
                        await _context.SaveChangesAsync();

                        TempData["SuccessMessage"] = "Company profile created successfully.";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving company profile");
                    TempData["ErrorMessage"] = "Failed to save company profile: " + ex.Message;
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Please check the input values.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
