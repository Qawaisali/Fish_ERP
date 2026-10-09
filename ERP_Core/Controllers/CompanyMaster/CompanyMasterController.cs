using ERP_Core.Attributes;
using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ERP_Core.Controllers.CompanyMaster
{
    [Authorize]
    [FormAccessAuthorize("Company Master")]
    [Route("CompanyMaster")]
    public class CompanyMasterController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<CompanyMasterController> _logger;

        public CompanyMasterController(ERPDbContext context, ILogger<CompanyMasterController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // =========================================================================
        // 1. READ / LIST (INDEX)
        // =========================================================================
        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(string? search, string? status)
        {
            ViewData["Title"] = "Company Master";
            ViewData["SubTitle"] = "Aap ki company ki bunyadi tafseelaat — branches, contact aur profile records manage karein.";
            ViewData["ActivePage"] = "CompanyMaster";

            try
            {
                var query = _context.CCompanies.AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    string term = search.Trim().ToLower();
                    query = query.Where(c =>
                        (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)) ||
                        (c.Code != null && c.Code.ToLower().Contains(term)) ||
                        (c.Phone != null && c.Phone.ToLower().Contains(term)) ||
                        (c.EmailId != null && c.EmailId.ToLower().Contains(term)) ||
                        (c.Address != null && c.Address.ToLower().Contains(term)) ||
                        (c.State != null && c.State.ToLower().Contains(term)) ||
                        (c.Country != null && c.Country.ToLower().Contains(term))
                    );
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(c => c.Active == true || c.Active == null);
                    }
                    else if (status.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(c => c.Active == false);
                    }
                }

                var allCompanies = await _context.CCompanies.AsNoTracking().OrderBy(c => c.CompanyId).ToListAsync();
                var filteredList = await query.OrderByDescending(c => c.CompanyId).ToListAsync();

                var viewModel = new CompanyMasterViewModel
                {
                    Companies = filteredList,
                    ParentCompanies = allCompanies.Where(c => c.Active != false).ToList(),
                    TotalCompanies = allCompanies.Count,
                    ActiveCompanies = allCompanies.Count(c => c.Active == true || c.Active == null),
                    InactiveCompanies = allCompanies.Count(c => c.Active == false),
                    BranchCompanies = allCompanies.Count(c => c.ParentId != null && c.ParentId > 0),
                    Form = new Company
                    {
                        Country = "Pakistan",
                        State = "Sindh",
                        Active = true
                    }
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching companies in CompanyMasterController.Index");
                TempData["ErrorMessage"] = "Failed to load company records: " + ex.Message;
                return View(new CompanyMasterViewModel());
            }
        }

        // =========================================================================
        // 2. GET BY ID (JSON API for AJAX Edit/View)
        // =========================================================================
        [HttpGet]
        [Route("GetById/{id:int}")]
        [Route("GetCompany/{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var company = await _context.CCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == id);
                if (company == null)
                {
                    return Json(new { success = false, message = "Company not found." });
                }

                return Json(new { success = true, data = company });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving company id {CompanyId}", id);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================================================================
        // 3. CREATE COMPANY
        // =========================================================================
        [HttpPost]
        [Route("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Company model)
        {
            if (string.IsNullOrWhiteSpace(model.CompanyName))
            {
                TempData["ErrorMessage"] = "Company Name is required.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                SanitizeCompany(model);

                // Auto generate Code if not provided
                if (string.IsNullOrWhiteSpace(model.Code))
                {
                    int nextId = (await _context.CCompanies.MaxAsync(c => (int?)c.CompanyId) ?? 0) + 1;
                    model.Code = $"CMP-{nextId:D3}";
                }

                model.CreatedOn = DateTime.Now;
                model.CreatedBy = User.Identity?.Name ?? "System";

                await _context.CCompanies.AddAsync(model);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Company '{CompanyName}' (ID: {CompanyId}) created by {User}",
                    model.CompanyName, model.CompanyId, User.Identity?.Name ?? "System");

                TempData["SuccessMessage"] = $"Company '{model.CompanyName}' created successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating company '{CompanyName}'", model.CompanyName);
                TempData["ErrorMessage"] = "Failed to create company: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 4. EDIT / UPDATE COMPANY
        // =========================================================================
        [HttpPost]
        [Route("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Company model)
        {
            if (model.CompanyId <= 0)
            {
                TempData["ErrorMessage"] = "Invalid Company ID.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(model.CompanyName))
            {
                TempData["ErrorMessage"] = "Company Name is required.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var existing = await _context.CCompanies.FirstOrDefaultAsync(c => c.CompanyId == model.CompanyId);
                if (existing == null)
                {
                    TempData["ErrorMessage"] = "Company record not found.";
                    return RedirectToAction(nameof(Index));
                }

                SanitizeCompany(model);

                // Prevent setting itself as parent
                if (model.ParentId == model.CompanyId)
                {
                    model.ParentId = null;
                }

                existing.CompanyName = model.CompanyName;
                existing.Code = string.IsNullOrWhiteSpace(model.Code) ? existing.Code : model.Code;
                existing.ParentId = model.ParentId > 0 ? model.ParentId : null;
                existing.Address = model.Address;
                existing.Phone = model.Phone;
                existing.Mobile = model.Mobile;
                existing.EmailId = model.EmailId;
                existing.Country = model.Country;
                existing.State = model.State;
                existing.Prefix = model.Prefix;
                existing.Logo = model.Logo;
                existing.Active = model.Active ?? true;
                existing.ModifiedOn = DateTime.Now;
                existing.ModifiedBy = User.Identity?.Name ?? "System";

                _context.CCompanies.Update(existing);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Company '{CompanyName}' (ID: {CompanyId}) updated by {User}",
                    existing.CompanyName, existing.CompanyId, User.Identity?.Name ?? "System");

                TempData["SuccessMessage"] = $"Company '{existing.CompanyName}' updated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating company ID {CompanyId}", model.CompanyId);
                TempData["ErrorMessage"] = "Failed to update company: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        private static void SanitizeCompany(Company model)
        {
            model.CompanyName = model.CompanyName?.Trim() ?? string.Empty;
            model.Code = model.Code?.Trim() ?? string.Empty;
            model.Address = model.Address?.Trim() ?? string.Empty;
            model.Phone = model.Phone?.Trim() ?? string.Empty;
            model.Mobile = model.Mobile?.Trim() ?? string.Empty;
            model.EmailId = model.EmailId?.Trim() ?? string.Empty;
            model.Country = string.IsNullOrWhiteSpace(model.Country) ? "Pakistan" : model.Country.Trim();
            model.State = string.IsNullOrWhiteSpace(model.State) ? "Sindh" : model.State.Trim();
            model.Prefix = model.Prefix?.Trim() ?? string.Empty;
            model.Logo = model.Logo?.Trim() ?? string.Empty;
            model.Active = model.Active ?? true;
        }

        // =========================================================================
        // 5. UNIFIED SAVE ACTION (Supports both Create and Edit)
        // =========================================================================
        [HttpPost]
        [Route("Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Company model)
        {
            if (model.CompanyId > 0)
            {
                return await Edit(model);
            }
            else
            {
                return await Create(model);
            }
        }

        // =========================================================================
        // 6. TOGGLE ACTIVE / INACTIVE STATUS
        // =========================================================================
        [HttpPost]
        [Route("ToggleStatus/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                var company = await _context.CCompanies.FirstOrDefaultAsync(c => c.CompanyId == id);
                if (company == null)
                {
                    TempData["ErrorMessage"] = "Company not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Protect active session company from being deactivated
                int sessionCompanyId = HttpContext.Session.GetInt32("CompanyId") ?? 0;
                if (sessionCompanyId == id && (company.Active == true || company.Active == null))
                {
                    TempData["ErrorMessage"] = "Cannot deactivate the currently active session company workspace.";
                    return RedirectToAction(nameof(Index));
                }

                company.Active = !(company.Active ?? true);
                company.ModifiedOn = DateTime.Now;
                company.ModifiedBy = User.Identity?.Name ?? "System";

                _context.CCompanies.Update(company);
                await _context.SaveChangesAsync();

                string statusText = (company.Active == true) ? "Activated" : "Deactivated";
                TempData["SuccessMessage"] = $"Company '{company.CompanyName}' {statusText} successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling status for company ID {CompanyId}", id);
                TempData["ErrorMessage"] = "Failed to update status: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 7. DELETE COMPANY
        // =========================================================================
        [HttpPost]
        [Route("Delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var company = await _context.CCompanies.FirstOrDefaultAsync(c => c.CompanyId == id);
                if (company == null)
                {
                    TempData["ErrorMessage"] = "Company not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if active session company
                int sessionCompanyId = HttpContext.Session.GetInt32("CompanyId") ?? 0;
                if (sessionCompanyId == id)
                {
                    TempData["ErrorMessage"] = "Cannot delete the active session company workspace.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if any child companies reference this company as ParentId
                bool hasBranches = await _context.CCompanies.AnyAsync(c => c.ParentId == id);
                if (hasBranches)
                {
                    TempData["ErrorMessage"] = "Cannot delete this company because child branch companies are linked to it. Please reassign branches first.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if users exist in this company
                bool hasUsers = await _context.CApplicationUsers.AnyAsync(u => u.CompanyId == id);
                if (hasUsers)
                {
                    TempData["ErrorMessage"] = "Cannot delete this company because user accounts are assigned to it. Deactivate the company instead.";
                    return RedirectToAction(nameof(Index));
                }

                _context.CCompanies.Remove(company);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Company '{CompanyName}' (ID: {CompanyId}) deleted by {User}",
                    company.CompanyName, id, User.Identity?.Name ?? "System");

                TempData["SuccessMessage"] = $"Company '{company.CompanyName}' deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting company ID {CompanyId}", id);
                TempData["ErrorMessage"] = "Failed to delete company: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
