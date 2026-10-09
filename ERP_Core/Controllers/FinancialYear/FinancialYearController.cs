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

namespace ERP_Core.Controllers.FinancialYear
{
    [Authorize]
    [FormAccessAuthorize("Financial Year")]
    [Route("FinancialYear")]
    [Route("financial-year")]
    [Route("FinancailYear")]
    [Route("financail-year")]
    public class FinancialYearController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<FinancialYearController> _logger;

        public FinancialYearController(ERPDbContext context, ILogger<FinancialYearController> logger)
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
        public async Task<IActionResult> Index(string? search, int? companyId, string? status)
        {
            ViewData["Title"] = "Financial Year Master";
            ViewData["SubTitle"] = "Mali saal ke doraniye, live status aur books locking manage karein.";
            ViewData["ActivePage"] = "FinancialYear";

            try
            {
                var query = _context.CFinancialYears.AsNoTracking().AsQueryable();

                if (companyId.HasValue && companyId.Value > 0)
                {
                    query = query.Where(f => f.CompanyID == companyId.Value);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    string term = search.Trim().ToLower();
                    query = query.Where(f =>
                        (f.Description != null && f.Description.ToLower().Contains(term)) ||
                        (f.Code != null && f.Code.ToLower().Contains(term))
                    );
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (status.Equals("Current", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(f => f.CurrentYear == true);
                    }
                    else if (status.Equals("Locked", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(f => f.LockYear == true);
                    }
                    else if (status.Equals("Open", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(f => f.LockYear == false);
                    }
                    else if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(f => f.Active == true || f.Active == null);
                    }
                    else if (status.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(f => f.Active == false);
                    }
                }

                var rawList = await query.OrderByDescending(f => f.StartDate).ToListAsync();
                var companies = await _context.CCompanies.AsNoTracking().OrderBy(c => c.CompanyName).ToListAsync();
                var companyDict = companies
                    .GroupBy(c => c.CompanyId)
                    .ToDictionary(g => g.Key, g => g.First().CompanyName);

                var items = rawList.Select(f => new FinancialYearListDto
                {
                    Id = f.Id,
                    Description = f.Description ?? string.Empty,
                    Code = f.Code ?? string.Empty,
                    StartDate = f.StartDate,
                    EndDate = f.EndDate,
                    CurrentYear = f.CurrentYear,
                    LockYear = f.LockYear,
                    CompanyID = f.CompanyID,
                    CompanyName = companyDict.ContainsKey(f.CompanyID) ? companyDict[f.CompanyID] : $"Company #{f.CompanyID}",
                    Active = f.Active,
                    CreatedOn = f.CreatedOn,
                    CreatedBy = f.CreatedBy
                }).ToList();

                var currentYearObj = items.FirstOrDefault(f => f.CurrentYear);

                int sessionCompanyId = HttpContext.Session.GetInt32("CompanyId") ?? (companies.FirstOrDefault()?.CompanyId ?? 1);

                var viewModel = new FinancialYearViewModel
                {
                    FinancialYears = items,
                    AvailableCompanies = companies,
                    TotalYears = items.Count,
                    ActiveYears = items.Count(f => !f.LockYear && (f.Active == true || f.Active == null)),
                    LockedYears = items.Count(f => f.LockYear),
                    CurrentActiveYearName = currentYearObj?.Description ?? "Not Configured",
                    Form = new Models.FinancialYear
                    {
                        StartDate = new DateTime(DateTime.Now.Year, 7, 1),
                        EndDate = new DateTime(DateTime.Now.Year + 1, 6, 30),
                        Description = $"FY {DateTime.Now.Year}-{DateTime.Now.Year + 1}",
                        Code = $"FY{DateTime.Now.Year % 100}{(DateTime.Now.Year + 1) % 100}",
                        CompanyID = sessionCompanyId,
                        Active = true,
                        CurrentYear = false,
                        LockYear = false
                    }
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading financial years in FinancialYearController.Index");
                TempData["ErrorMessage"] = "Failed to load financial years: " + ex.Message;
                return View(new FinancialYearViewModel());
            }
        }

        // =========================================================================
        // 2. GET BY ID (JSON for AJAX Edit)
        // =========================================================================
        [HttpGet]
        [Route("GetById/{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var fy = await _context.CFinancialYears.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
                if (fy == null)
                {
                    return Json(new { success = false, message = "Financial year not found." });
                }

                return Json(new { success = true, data = fy });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving financial year id {Id}", id);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================================================================
        // 3. CREATE FINANCIAL YEAR
        // =========================================================================
        [HttpPost]
        [Route("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Models.FinancialYear model)
        {
            if (string.IsNullOrWhiteSpace(model.Description))
            {
                TempData["ErrorMessage"] = "Description is required.";
                return RedirectToAction(nameof(Index));
            }

            if (model.EndDate <= model.StartDate)
            {
                TempData["ErrorMessage"] = "End Date must be greater than Start Date.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                SanitizeFinancialYear(model);

                if (model.CompanyID <= 0)
                {
                    model.CompanyID = HttpContext.Session.GetInt32("CompanyId") ?? (await _context.CCompanies.Select(c => c.CompanyId).FirstOrDefaultAsync());
                }

                // If marked as Current Year, reset others for this company
                if (model.CurrentYear)
                {
                    var existingCurrent = await _context.CFinancialYears
                        .Where(f => f.CompanyID == model.CompanyID && f.CurrentYear)
                        .ToListAsync();

                    foreach (var c in existingCurrent)
                    {
                        c.CurrentYear = false;
                        _context.CFinancialYears.Update(c);
                    }
                }

                model.CreatedOn = DateTime.Now;
                model.CreatedBy = User.Identity?.Name ?? "System";

                await _context.CFinancialYears.AddAsync(model);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Financial Year '{Description}' (ID: {Id}) created by {User}",
                    model.Description, model.Id, User.Identity?.Name ?? "System");

                TempData["SuccessMessage"] = $"Financial Year '{model.Description}' created successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating financial year '{Description}'", model.Description);
                TempData["ErrorMessage"] = "Failed to create financial year: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 4. EDIT / UPDATE FINANCIAL YEAR
        // =========================================================================
        [HttpPost]
        [Route("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Models.FinancialYear model)
        {
            if (model.Id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid Financial Year ID.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(model.Description))
            {
                TempData["ErrorMessage"] = "Description is required.";
                return RedirectToAction(nameof(Index));
            }

            if (model.EndDate <= model.StartDate)
            {
                TempData["ErrorMessage"] = "End Date must be greater than Start Date.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var existing = await _context.CFinancialYears.FirstOrDefaultAsync(f => f.Id == model.Id);
                if (existing == null)
                {
                    TempData["ErrorMessage"] = "Financial year record not found.";
                    return RedirectToAction(nameof(Index));
                }

                SanitizeFinancialYear(model);

                // If setting as Current Year, reset other years in the same company
                if (model.CurrentYear)
                {
                    var others = await _context.CFinancialYears
                        .Where(f => f.CompanyID == model.CompanyID && f.Id != model.Id && f.CurrentYear)
                        .ToListAsync();

                    foreach (var o in others)
                    {
                        o.CurrentYear = false;
                        _context.CFinancialYears.Update(o);
                    }
                }

                existing.Description = model.Description;
                existing.Code = model.Code;
                existing.StartDate = model.StartDate;
                existing.EndDate = model.EndDate;
                existing.CurrentYear = model.CurrentYear;
                existing.LockYear = model.LockYear;
                existing.CompanyID = model.CompanyID;
                existing.Active = model.Active ?? true;
                existing.ModifiedOn = DateTime.Now;
                existing.ModifiedBy = User.Identity?.Name ?? "System";

                _context.CFinancialYears.Update(existing);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Financial Year '{Description}' (ID: {Id}) updated by {User}",
                    existing.Description, existing.Id, User.Identity?.Name ?? "System");

                TempData["SuccessMessage"] = $"Financial Year '{existing.Description}' updated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating financial year ID {Id}", model.Id);
                TempData["ErrorMessage"] = "Failed to update financial year: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 5. UNIFIED SAVE ACTION
        // =========================================================================
        [HttpPost]
        [Route("Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Models.FinancialYear model)
        {
            if (model.Id > 0)
            {
                return await Edit(model);
            }
            else
            {
                return await Create(model);
            }
        }

        // =========================================================================
        // 6. SET AS CURRENT / LIVE FINANCIAL YEAR
        // =========================================================================
        [HttpPost]
        [Route("SetCurrent/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetCurrent(int id)
        {
            try
            {
                var target = await _context.CFinancialYears.FirstOrDefaultAsync(f => f.Id == id);
                if (target == null)
                {
                    TempData["ErrorMessage"] = "Financial year not found.";
                    return RedirectToAction(nameof(Index));
                }

                var companyYears = await _context.CFinancialYears
                    .Where(f => f.CompanyID == target.CompanyID)
                    .ToListAsync();

                foreach (var fy in companyYears)
                {
                    fy.CurrentYear = (fy.Id == id);
                    fy.ModifiedOn = DateTime.Now;
                    fy.ModifiedBy = User.Identity?.Name ?? "System";
                    _context.CFinancialYears.Update(fy);
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Financial Year '{target.Description}' is now set as the active current year.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting active current financial year for ID {Id}", id);
                TempData["ErrorMessage"] = "Failed to set current year: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 7. TOGGLE LOCK / UNLOCK (Close/Open books)
        // =========================================================================
        [HttpPost]
        [Route("ToggleLock/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(int id)
        {
            try
            {
                var target = await _context.CFinancialYears.FirstOrDefaultAsync(f => f.Id == id);
                if (target == null)
                {
                    TempData["ErrorMessage"] = "Financial year not found.";
                    return RedirectToAction(nameof(Index));
                }

                target.LockYear = !target.LockYear;
                target.ModifiedOn = DateTime.Now;
                target.ModifiedBy = User.Identity?.Name ?? "System";

                _context.CFinancialYears.Update(target);
                await _context.SaveChangesAsync();

                string lockStatus = target.LockYear ? "locked (Read-Only)" : "unlocked (Open for transactions)";
                TempData["SuccessMessage"] = $"Financial Year '{target.Description}' is now {lockStatus}.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling lock for financial year ID {Id}", id);
                TempData["ErrorMessage"] = "Failed to update lock status: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 8. TOGGLE ACTIVE / INACTIVE STATUS
        // =========================================================================
        [HttpPost]
        [Route("ToggleStatus/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                var fy = await _context.CFinancialYears.FirstOrDefaultAsync(f => f.Id == id);
                if (fy == null)
                {
                    TempData["ErrorMessage"] = "Financial year not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (fy.CurrentYear && (fy.Active == true || fy.Active == null))
                {
                    TempData["ErrorMessage"] = "Cannot deactivate the currently active live financial year.";
                    return RedirectToAction(nameof(Index));
                }

                fy.Active = !(fy.Active ?? true);
                fy.ModifiedOn = DateTime.Now;
                fy.ModifiedBy = User.Identity?.Name ?? "System";

                _context.CFinancialYears.Update(fy);
                await _context.SaveChangesAsync();

                string statusText = (fy.Active == true) ? "Activated" : "Deactivated";
                TempData["SuccessMessage"] = $"Financial Year '{fy.Description}' {statusText} successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling status for financial year ID {Id}", id);
                TempData["ErrorMessage"] = "Failed to update status: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 9. DELETE FINANCIAL YEAR
        // =========================================================================
        [HttpPost]
        [Route("Delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var fy = await _context.CFinancialYears.FirstOrDefaultAsync(f => f.Id == id);
                if (fy == null)
                {
                    TempData["ErrorMessage"] = "Financial year not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (fy.CurrentYear)
                {
                    TempData["ErrorMessage"] = "Cannot delete the active live financial year. Set another financial year as current first.";
                    return RedirectToAction(nameof(Index));
                }

                _context.CFinancialYears.Remove(fy);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Financial Year '{Description}' (ID: {Id}) deleted by {User}",
                    fy.Description, id, User.Identity?.Name ?? "System");

                TempData["SuccessMessage"] = $"Financial Year '{fy.Description}' deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting financial year ID {Id}", id);
                TempData["ErrorMessage"] = "Failed to delete financial year: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        private static void SanitizeFinancialYear(Models.FinancialYear model)
        {
            model.Description = model.Description?.Trim() ?? string.Empty;
            model.Code = model.Code?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(model.Code) && model.StartDate != default && model.EndDate != default)
            {
                model.Code = $"FY{model.StartDate:yy}{model.EndDate:yy}";
            }
            model.Active = model.Active ?? true;
        }
    }
}
