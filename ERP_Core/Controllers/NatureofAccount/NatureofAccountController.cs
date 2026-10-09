using ERP_Core.Attributes;
using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP_Core.Controllers.NatureofAccount
{
    [Authorize]
    [FormAccessAuthorize("Nature of Account")]
    [Route("NatureofAccount")]
    public class NatureofAccountController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<NatureofAccountController> _logger;

        public NatureofAccountController(ERPDbContext context, ILogger<NatureofAccountController> logger)
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
        public async Task<IActionResult> Index(string? search, string? type)
        {
            ViewData["Title"] = "Nature of Account Master";
            ViewData["SubTitle"] = "Debtors (D) aur Creditors (C) ki classification aur account nature manage karein.";
            ViewData["ActivePage"] = "NatureofAccount";

            try
            {
                var allRecords = await _context.NatureofAccounts
                    .AsNoTracking()
                    .OrderBy(a => a.AccountName)
                    .ToListAsync();

                var query = allRecords.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    string term = search.Trim().ToLower();
                    query = query.Where(a =>
                        (a.AccountName != null && a.AccountName.ToLower().Contains(term)) ||
                        (a.DebtorOrCreditor != null && a.DebtorOrCreditor.ToLower().Contains(term))
                    );
                }

                if (!string.IsNullOrWhiteSpace(type))
                {
                    string t = type.Trim().ToUpper();
                    query = query.Where(a => string.Equals(a.DebtorOrCreditor?.Trim(), t, StringComparison.OrdinalIgnoreCase));
                }

                var filteredList = query.ToList();

                var viewModel = new NatureofAccountViewModel
                {
                    Accounts = filteredList,
                    TotalCount = allRecords.Count,
                    DebtorsCount = allRecords.Count(a => string.Equals(a.DebtorOrCreditor?.Trim(), "D", StringComparison.OrdinalIgnoreCase)),
                    CreditorsCount = allRecords.Count(a => string.Equals(a.DebtorOrCreditor?.Trim(), "C", StringComparison.OrdinalIgnoreCase)),
                    UnspecifiedCount = allRecords.Count(a => string.IsNullOrEmpty(a.DebtorOrCreditor) || (a.DebtorOrCreditor.Trim() != "D" && a.DebtorOrCreditor.Trim() != "C")),
                    SearchTerm = search,
                    SelectedType = type,
                    Form = new Models.NatureofAccount()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Nature of Accounts in NatureofAccountController.Index");
                TempData["ErrorMessage"] = "Failed to load Nature of Accounts: " + ex.Message;
                return View(new NatureofAccountViewModel());
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
                var account = await _context.NatureofAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
                if (account == null)
                {
                    return Json(new { success = false, message = "Record not found." });
                }

                return Json(new { 
                    success = true, 
                    data = new {
                        id = account.Id,
                        accountName = account.AccountName?.Trim() ?? string.Empty,
                        debtorOrCreditor = account.DebtorOrCreditor?.Trim() ?? string.Empty
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Nature of Account ID {Id}", id);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================================================================
        // 3. CREATE / UPDATE (SAVE)
        // =========================================================================
        [HttpPost]
        [Route("Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Models.NatureofAccount model)
        {
            if (string.IsNullOrWhiteSpace(model.AccountName))
            {
                TempData["ErrorMessage"] = "Account Name is required.";
                return RedirectToAction(nameof(Index));
            }

            model.AccountName = model.AccountName.Trim();
            if (!string.IsNullOrWhiteSpace(model.DebtorOrCreditor))
            {
                model.DebtorOrCreditor = model.DebtorOrCreditor.Trim().ToUpper();
                if (model.DebtorOrCreditor.Length > 1)
                {
                    model.DebtorOrCreditor = model.DebtorOrCreditor.Substring(0, 1);
                }
            }
            else
            {
                model.DebtorOrCreditor = null;
            }

            try
            {
                if (model.Id > 0)
                {
                    // Edit / Update
                    var existing = await _context.NatureofAccounts.FirstOrDefaultAsync(a => a.Id == model.Id);
                    if (existing == null)
                    {
                        TempData["ErrorMessage"] = "Nature of Account record not found.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Duplicate check (ignoring current record)
                    bool duplicate = await _context.NatureofAccounts
                        .AnyAsync(a => a.Id != model.Id && a.AccountName != null && a.AccountName.ToLower() == model.AccountName.ToLower());

                    if (duplicate)
                    {
                        TempData["ErrorMessage"] = $"An account with name '{model.AccountName}' already exists.";
                        return RedirectToAction(nameof(Index));
                    }

                    existing.AccountName = model.AccountName;
                    existing.DebtorOrCreditor = model.DebtorOrCreditor;

                    _context.NatureofAccounts.Update(existing);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Nature of Account ID {Id} ('{Name}') updated by {User}",
                        model.Id, model.AccountName, User.Identity?.Name ?? "System");

                    TempData["SuccessMessage"] = $"Nature of Account '{model.AccountName}' updated successfully.";
                }
                else
                {
                    // Create / Insert
                    bool duplicate = await _context.NatureofAccounts
                        .AnyAsync(a => a.AccountName != null && a.AccountName.ToLower() == model.AccountName.ToLower());

                    if (duplicate)
                    {
                        TempData["ErrorMessage"] = $"An account with name '{model.AccountName}' already exists.";
                        return RedirectToAction(nameof(Index));
                    }

                    await _context.NatureofAccounts.AddAsync(model);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Nature of Account ID {Id} ('{Name}') created by {User}",
                        model.Id, model.AccountName, User.Identity?.Name ?? "System");

                    TempData["SuccessMessage"] = $"Nature of Account '{model.AccountName}' created successfully.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Nature of Account '{Name}'", model.AccountName);
                TempData["ErrorMessage"] = "Failed to save Nature of Account: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 4. DELETE
        // =========================================================================
        [HttpPost]
        [Route("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid Account ID.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var existing = await _context.NatureofAccounts.FirstOrDefaultAsync(a => a.Id == id);
                if (existing == null)
                {
                    TempData["ErrorMessage"] = "Record not found or already deleted.";
                    return RedirectToAction(nameof(Index));
                }

                string name = existing.AccountName ?? $"ID #{id}";
                _context.NatureofAccounts.Remove(existing);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Nature of Account ID {Id} ('{Name}') deleted by {User}",
                    id, name, User.Identity?.Name ?? "System");

                TempData["SuccessMessage"] = $"Nature of Account '{name}' deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Nature of Account ID {Id}", id);
                TempData["ErrorMessage"] = "Failed to delete Nature of Account: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
