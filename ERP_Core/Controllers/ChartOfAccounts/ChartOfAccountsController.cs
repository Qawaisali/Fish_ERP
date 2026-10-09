using ERP_Core.Attributes;
using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using ERP_Core.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ERP_Core.Controllers.ChartOfAccounts
{
    [Authorize]
    [FormAccessAuthorize("Chart of Accounts")]
    [Route("ChartOfAccounts")]
    [Route("chart-of-accounts")]
    public class ChartOfAccountsController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<ChartOfAccountsController> _logger;

        public ChartOfAccountsController(ERPDbContext context, ILogger<ChartOfAccountsController> logger)
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
        public async Task<IActionResult> Index(string? search, int? natureId, int? parentId, string? status)
        {
            ViewData["Title"] = "Chart of Accounts";
            ViewData["SubTitle"] = "Manage account hierarchy, nature of accounts, and financial posting heads.";
            ViewData["ActivePage"] = "ChartOfAccounts";

            try
            {
                // Ensure default starter accounts if table is empty
                await EnsureDefaultAccountsAsync();

                var query = _context.ChartOfAccounts
                    .Include(a => a.ParentAccount)
                    .Include(a => a.NatureOfAccount)
                    .Include(a => a.SubAccounts)
                    .AsNoTracking()
                    .AsQueryable();

                if (natureId.HasValue && natureId.Value > 0)
                {
                    query = query.Where(a => a.NatureOfAccountId == natureId.Value);
                }

                if (parentId.HasValue && parentId.Value > 0)
                {
                    query = query.Where(a => a.ParentAccountId == parentId.Value);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    string term = search.Trim().ToLower();
                    query = query.Where(a =>
                        (a.AccountName != null && a.AccountName.ToLower().Contains(term)) ||
                        (a.AccountCode != null && a.AccountCode.ToLower().Contains(term))
                    );
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (status.Equals("Transable", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(a => a.Transable);
                    }
                    else if (status.Equals("Group", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(a => !a.Transable);
                    }
                    else if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(a => a.Active == true || a.Active == null);
                    }
                    else if (status.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(a => a.Active == false);
                    }
                }

                var allAccounts = await _context.ChartOfAccounts
                    .Include(a => a.NatureOfAccount)
                    .AsNoTracking()
                    .OrderBy(a => a.AccountCode)
                    .ThenBy(a => a.AccountName)
                    .ToListAsync();

                var filteredList = await query
                    .OrderBy(a => a.AccountCode)
                    .ThenBy(a => a.AccountName)
                    .ToListAsync();

                var natureList = await _context.NatureofAccounts
                    .AsNoTracking()
                    .OrderBy(n => n.AccountName)
                    .Select(n => new NatureOfAccountDropdownDto
                    {
                        Id = n.Id,
                        AccountName = n.AccountName,
                        DebtorOrCreditor = n.DebtorOrCreditor
                    })
                    .ToListAsync();

                var parentDropdownList = allAccounts
                    .Select(a => new ChartOfAccountDropdownDto
                    {
                        Id = a.Id,
                        AccountCode = a.AccountCode,
                        AccountName = a.AccountName,
                        LevelNo = a.LevelNo
                    })
                    .ToList();

                var accountDtos = filteredList.Select(a => new ChartOfAccountListDto
                {
                    Id = a.Id,
                    AccountCode = a.AccountCode ?? string.Empty,
                    AccountName = a.AccountName ?? string.Empty,
                    ParentAccountId = a.ParentAccountId,
                    ParentAccountName = a.ParentAccount?.AccountName,
                    ParentAccountCode = a.ParentAccount?.AccountCode,
                    NatureOfAccountId = a.NatureOfAccountId,
                    NatureOfAccountName = a.NatureOfAccount?.AccountName,
                    DebtorOrCreditor = a.NatureOfAccount?.DebtorOrCreditor,
                    Currency = a.Currency ?? "PKR",
                    Transable = a.Transable,
                    Active = a.Active,
                    CompanyId = a.CompanyId,
                    LevelNo = a.LevelNo,
                    SubAccountsCount = a.SubAccounts?.Count ?? 0,
                    CreatedOn = a.CreatedOn,
                    CreatedBy = a.CreatedBy
                }).ToList();

                var treeNodes = BuildAccountTree(allAccounts);

                var viewModel = new ChartOfAccountsViewModel
                {
                    Accounts = accountDtos,
                    AvailableParentAccounts = parentDropdownList,
                    AvailableNatureOfAccounts = natureList,
                    TotalAccounts = allAccounts.Count,
                    TransableAccounts = allAccounts.Count(a => a.Transable),
                    GroupAccounts = allAccounts.Count(a => !a.Transable),
                    ActiveAccounts = allAccounts.Count(a => a.Active == true || a.Active == null),
                    TreeNodes = treeNodes,
                    Form = new ChartOfAccount
                    {
                        Currency = "PKR",
                        Transable = true,
                        Active = true
                    }
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Chart of Accounts");
                TempData["ErrorMessage"] = "Failed to load Chart of Accounts: " + ex.Message;
                return View(new ChartOfAccountsViewModel());
            }
        }

        // =========================================================================
        // 2. GET BY ID (JSON for AJAX Edit Modal)
        // =========================================================================
        [HttpGet]
        [Route("GetById/{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var acc = await _context.ChartOfAccounts
                    .Include(a => a.NatureOfAccount)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (acc == null)
                {
                    return Json(new { success = false, message = "Account not found." });
                }

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        id = acc.Id,
                        accountCode = acc.AccountCode,
                        accountName = acc.AccountName,
                        parentAccountId = acc.ParentAccountId,
                        natureOfAccountId = acc.NatureOfAccountId,
                        currency = acc.Currency ?? "PKR",
                        transable = acc.Transable,
                        active = acc.Active ?? true,
                        levelNo = acc.LevelNo
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving account id {Id}", id);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================================================================
        // 3. GET NEXT ACCOUNT CODE (AJAX Helper)
        // =========================================================================
        [HttpGet]
        [Route("GetNextCode")]
        public async Task<IActionResult> GetNextCode(int? parentAccountId)
        {
            try
            {
                string nextCode = await GenerateNextAccountCodeAsync(parentAccountId);
                return Json(new { success = true, nextCode });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================================================================
        // 4. CREATE ACCOUNT
        // =========================================================================
        [HttpPost]
        [Route("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ChartOfAccount model)
        {
            if (string.IsNullOrWhiteSpace(model.AccountName))
            {
                TempData["ErrorMessage"] = "Account Name is required.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                SanitizeAccount(model);

                // Set level based on parent account
                if (model.ParentAccountId.HasValue && model.ParentAccountId.Value > 0)
                {
                    var parentAcc = await _context.ChartOfAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == model.ParentAccountId.Value);
                    model.LevelNo = (parentAcc?.LevelNo ?? 1) + 1;
                }
                else
                {
                    model.LevelNo = 1;
                }

                if (string.IsNullOrWhiteSpace(model.AccountCode))
                {
                    model.AccountCode = await GenerateNextAccountCodeAsync(model.ParentAccountId);
                }

                // Check for duplicate account code
                if (!string.IsNullOrWhiteSpace(model.AccountCode))
                {
                    bool codeExists = await _context.ChartOfAccounts
                        .AnyAsync(a => a.AccountCode == model.AccountCode);

                    if (codeExists)
                    {
                        TempData["ErrorMessage"] = $"Account Code '{model.AccountCode}' is already in use.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                model.CreatedOn = DateTime.Now;
                model.CreatedBy = User.Identity?.Name ?? "System";
                model.CompanyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;

                // Explicit boolean resolution for HTML checkboxes
                if (Request.HasFormContentType && Request.Form.ContainsKey("Transable"))
                {
                    var val = Request.Form["Transable"].ToString().ToLowerInvariant();
                    model.Transable = val.Contains("true") || val.Contains("on") || val == "1";
                }

                if (Request.HasFormContentType && Request.Form.ContainsKey("Active"))
                {
                    var val = Request.Form["Active"].ToString().ToLowerInvariant();
                    model.Active = val.Contains("true") || val.Contains("on") || val == "1";
                }

                await _context.ChartOfAccounts.AddAsync(model);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Account '{Name}' (Code: {Code}, LevelNo: {LevelNo}) created successfully", model.AccountName, model.AccountCode, model.LevelNo);
                TempData["SuccessMessage"] = $"Account '{model.AccountName}' ({model.AccountCode}) created successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating account '{Name}'", model.AccountName);
                TempData["ErrorMessage"] = "Failed to create account: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 5. EDIT ACCOUNT
        // =========================================================================
        [HttpPost]
        [Route("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ChartOfAccount model)
        {
            if (model.Id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid Account ID.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(model.AccountName))
            {
                TempData["ErrorMessage"] = "Account Name is required.";
                return RedirectToAction(nameof(Index));
            }

            // Prevent self-referencing hierarchy
            if (model.ParentAccountId.HasValue && model.ParentAccountId.Value == model.Id)
            {
                TempData["ErrorMessage"] = "An account cannot be set as its own parent account.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var existing = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.Id == model.Id);
                if (existing == null)
                {
                    TempData["ErrorMessage"] = "Account record not found.";
                    return RedirectToAction(nameof(Index));
                }

                SanitizeAccount(model);

                // Check duplicate code on other accounts
                if (!string.IsNullOrWhiteSpace(model.AccountCode))
                {
                    bool codeExists = await _context.ChartOfAccounts
                        .AnyAsync(a => a.Id != model.Id && a.AccountCode == model.AccountCode);

                    if (codeExists)
                    {
                        TempData["ErrorMessage"] = $"Account Code '{model.AccountCode}' is already used by another account.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                // Set level based on parent account
                if (model.ParentAccountId.HasValue && model.ParentAccountId.Value > 0)
                {
                    var parentAcc = await _context.ChartOfAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == model.ParentAccountId.Value);
                    existing.LevelNo = (parentAcc?.LevelNo ?? 1) + 1;
                }
                else
                {
                    existing.LevelNo = 1;
                }

                // Explicit boolean resolution for HTML checkboxes
                bool isTransable = false;
                if (Request.HasFormContentType && Request.Form.ContainsKey("Transable"))
                {
                    var val = Request.Form["Transable"].ToString().ToLowerInvariant();
                    isTransable = val.Contains("true") || val.Contains("on") || val == "1";
                }

                bool isActive = true;
                if (Request.HasFormContentType && Request.Form.ContainsKey("Active"))
                {
                    var val = Request.Form["Active"].ToString().ToLowerInvariant();
                    isActive = val.Contains("true") || val.Contains("on") || val == "1";
                }

                existing.AccountCode = model.AccountCode;
                existing.AccountName = model.AccountName;
                existing.ParentAccountId = (model.ParentAccountId.HasValue && model.ParentAccountId.Value > 0) ? model.ParentAccountId : null;
                existing.NatureOfAccountId = (model.NatureOfAccountId.HasValue && model.NatureOfAccountId.Value > 0) ? model.NatureOfAccountId : null;
                existing.Currency = string.IsNullOrWhiteSpace(model.Currency) ? "PKR" : model.Currency.Trim().ToUpperInvariant();
                existing.Transable = isTransable;
                existing.Active = isActive;
                existing.ModifiedOn = DateTime.Now;
                existing.ModifiedBy = User.Identity?.Name ?? "System";

                _context.ChartOfAccounts.Update(existing);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Account '{Name}' (ID: {Id}, LevelNo: {LevelNo}) updated successfully", existing.AccountName, existing.Id, existing.LevelNo);
                TempData["SuccessMessage"] = $"Account '{existing.AccountName}' updated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating account ID {Id}", model.Id);
                TempData["ErrorMessage"] = "Failed to update account: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 6. UNIFIED SAVE ACTION
        // =========================================================================
        [HttpPost]
        [Route("Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(ChartOfAccount model)
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
        // 7. TOGGLE ACTIVE STATUS
        // =========================================================================
        [HttpPost]
        [Route("ToggleStatus/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                var acc = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.Id == id);
                if (acc == null)
                {
                    TempData["ErrorMessage"] = "Account not found.";
                    return RedirectToAction(nameof(Index));
                }

                acc.Active = !(acc.Active ?? true);
                acc.ModifiedOn = DateTime.Now;
                acc.ModifiedBy = User.Identity?.Name ?? "System";

                _context.ChartOfAccounts.Update(acc);
                await _context.SaveChangesAsync();

                string statusText = (acc.Active == true) ? "Activated" : "Deactivated";
                TempData["SuccessMessage"] = $"Account '{acc.AccountName}' {statusText} successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling status for account ID {Id}", id);
                TempData["ErrorMessage"] = "Failed to toggle status: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 8. DELETE ACCOUNT
        // =========================================================================
        [HttpPost]
        [Route("Delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var acc = await _context.ChartOfAccounts
                    .Include(a => a.SubAccounts)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (acc == null)
                {
                    TempData["ErrorMessage"] = "Account not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (acc.SubAccounts != null && acc.SubAccounts.Any())
                {
                    TempData["ErrorMessage"] = $"Cannot delete '{acc.AccountName}' because it contains {acc.SubAccounts.Count} sub-account(s). Please reassign or delete sub-accounts first.";
                    return RedirectToAction(nameof(Index));
                }

                _context.ChartOfAccounts.Remove(acc);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Account '{Name}' (ID: {Id}) deleted successfully", acc.AccountName, id);
                TempData["SuccessMessage"] = $"Account '{acc.AccountName}' deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting account ID {Id}", id);
                TempData["ErrorMessage"] = "Failed to delete account: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // PRIVATE HELPERS
        // =========================================================================
        private static void SanitizeAccount(ChartOfAccount model)
        {
            model.AccountName = model.AccountName?.Trim() ?? string.Empty;
            model.AccountCode = model.AccountCode?.Trim();
            model.Currency = string.IsNullOrWhiteSpace(model.Currency) ? "PKR" : model.Currency.Trim().ToUpperInvariant();
            model.Active = model.Active ?? true;
            if (model.ParentAccountId.HasValue && model.ParentAccountId.Value <= 0)
            {
                model.ParentAccountId = null;
            }
            if (model.NatureOfAccountId.HasValue && model.NatureOfAccountId.Value <= 0)
            {
                model.NatureOfAccountId = null;
            }
        }

        private async Task<string> GenerateNextAccountCodeAsync(int? parentAccountId)
        {
            if (parentAccountId.HasValue && parentAccountId.Value > 0)
            {
                var parent = await _context.ChartOfAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == parentAccountId.Value);
                if (parent != null && !string.IsNullOrWhiteSpace(parent.AccountCode))
                {
                    string parentCode = parent.AccountCode.Trim();

                    // Use the parent's Level column to identify the child level:
                    // If Parent is Level 1 (e.g. "01") -> Child is Level 2 (e.g. "0101", suffix +2 digits)
                    // If Parent is Level 2 (e.g. "0101") -> Child is Level 3 (e.g. "0101001", suffix +3 digits)
                    // If Parent is Level 3 (e.g. "0101001") -> Child is Level 4 (e.g. "01010010001", suffix +4 digits)
                    int parentLevel = parent.LevelNo ?? (parentCode.Length switch
                    {
                        2 => 1,
                        4 => 2,
                        7 => 3,
                        11 => 4,
                        _ => 1
                    });

                    int targetLevel = parentLevel + 1;

                    int suffixLength = targetLevel switch
                    {
                        2 => 2, // Level 2: +2 digits (01 -> 0101)
                        3 => 3, // Level 3: +3 digits (0101 -> 0101001)
                        4 => 4, // Level 4: +4 digits (0101001 -> 01010010001)
                        _ => 4  // Level 5+: +4 digits
                    };

                    var siblingCodes = await _context.ChartOfAccounts
                        .Where(a => a.ParentAccountId == parentAccountId.Value && a.AccountCode != null)
                        .Select(a => a.AccountCode!)
                        .ToListAsync();

                    long maxSubIndex = 0;
                    foreach (var code in siblingCodes)
                    {
                        if (!string.IsNullOrEmpty(code))
                        {
                            string trimmed = code.Trim();
                            if (trimmed.StartsWith(parentCode, StringComparison.OrdinalIgnoreCase))
                            {
                                var remainder = trimmed.Substring(parentCode.Length).TrimStart('-', '.');
                                if (long.TryParse(remainder, out long parsed))
                                {
                                    if (parsed > maxSubIndex) maxSubIndex = parsed;
                                }
                            }
                        }
                    }

                    long nextIndex = maxSubIndex + 1;
                    string format = new string('0', suffixLength);
                    return $"{parentCode}{nextIndex.ToString(format)}";
                }
            }

            // Top-level / Parent account code generation (01, 02, 03, etc.) - Level 1
            var topCodes = await _context.ChartOfAccounts
                .Where(a => (a.ParentAccountId == null || a.LevelNo == 1) && a.AccountCode != null)
                .Select(a => a.AccountCode!)
                .ToListAsync();

            long maxTop = 0;
            foreach (var code in topCodes)
            {
                if (!string.IsNullOrWhiteSpace(code))
                {
                    if (long.TryParse(code.Trim(), out long parsed))
                    {
                        if (parsed > maxTop && parsed < 100)
                        {
                            maxTop = parsed;
                        }
                    }
                }
            }

            long nextTop = maxTop + 1;
            return nextTop.ToString("D2");
        }

        private static List<CoaNodeDto> BuildAccountTree(List<ChartOfAccount> allAccounts)
        {
            var lookup = allAccounts.ToLookup(a => a.ParentAccountId);

            List<CoaNodeDto> GetChildren(int? parentId)
            {
                return lookup[parentId].Select(a => new CoaNodeDto
                {
                    Id = a.Id,
                    Code = a.AccountCode ?? string.Empty,
                    Name = a.AccountName ?? string.Empty,
                    LevelNo = a.LevelNo,
                    NatureName = a.NatureOfAccount?.AccountName,
                    Currency = a.Currency ?? "PKR",
                    Transable = a.Transable,
                    Active = a.Active ?? true,
                    IsLeaf = !lookup[a.Id].Any(),
                    Children = GetChildren(a.Id)
                }).ToList();
            }

            return GetChildren(null);
        }

        private async Task EnsureDefaultAccountsAsync()
        {
            // Normalize any legacy hyphenated sub-accounts so Chart of Accounts displays uniform unhyphenated codes
            try
            {
                var legacyHyphenated = await _context.ChartOfAccounts
                    .Where(a => a.ParentAccountId != null && a.AccountCode != null && a.AccountCode.Contains('-'))
                    .Include(a => a.ParentAccount)
                    .ToListAsync();

                if (legacyHyphenated.Any())
                {
                    foreach (var acc in legacyHyphenated)
                    {
                        if (acc.ParentAccount != null && !string.IsNullOrWhiteSpace(acc.ParentAccount.AccountCode))
                        {
                            string pCode = acc.ParentAccount.AccountCode;
                            if (acc.AccountCode!.StartsWith(pCode))
                            {
                                var rem = acc.AccountCode.Substring(pCode.Length).TrimStart('-', '.');
                                if (int.TryParse(rem, out int num))
                                {
                                    int pad = pCode.Length >= 7 ? 5 : (pCode.Length == 4 ? 3 : 2);
                                    string clean = $"{pCode}{num.ToString().PadLeft(pad, '0')}";
                                    acc.AccountCode = clean;

                                    var v = await _context.Vendors.FirstOrDefaultAsync(x => x.ChartOfAccountId == acc.Id || x.AccountNo == acc.AccountCode);
                                    if (v != null)
                                    {
                                        v.AccountNo = clean;
                                    }
                                }
                            }
                        }
                    }
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not normalize legacy hyphenated accounts: {Message}", ex.Message);
            }

            if (!await _context.ChartOfAccounts.AnyAsync())
            {
                var debtorNature = await _context.NatureofAccounts.FirstOrDefaultAsync(n => n.DebtorOrCreditor == "D");
                var creditorNature = await _context.NatureofAccounts.FirstOrDefaultAsync(n => n.DebtorOrCreditor == "C");

                // Level 1: Parent Accounts (01, 02, 03, 04, 05)
                var assets = new ChartOfAccount { AccountCode = "01", AccountName = "Assets", LevelNo = 1, Transable = false, Active = true, NatureOfAccountId = debtorNature?.Id, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };
                var liab = new ChartOfAccount { AccountCode = "02", AccountName = "Liabilities", LevelNo = 1, Transable = false, Active = true, NatureOfAccountId = creditorNature?.Id, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };
                var equity = new ChartOfAccount { AccountCode = "03", AccountName = "Equity & Capital", LevelNo = 1, Transable = false, Active = true, NatureOfAccountId = creditorNature?.Id, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };
                var revenue = new ChartOfAccount { AccountCode = "04", AccountName = "Revenue & Sales", LevelNo = 1, Transable = false, Active = true, NatureOfAccountId = creditorNature?.Id, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };
                var expense = new ChartOfAccount { AccountCode = "05", AccountName = "Expenses", LevelNo = 1, Transable = false, Active = true, NatureOfAccountId = debtorNature?.Id, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };

                await _context.ChartOfAccounts.AddRangeAsync(assets, liab, equity, revenue, expense);
                await _context.SaveChangesAsync();

                // Level 2: Child Accounts (0101, 0102, 0201, 0501, etc.)
                var currentAssets = new ChartOfAccount { AccountCode = "0101", AccountName = "Current Assets", LevelNo = 2, ParentAccountId = assets.Id, NatureOfAccountId = debtorNature?.Id, Transable = false, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };
                var nonCurrentAssets = new ChartOfAccount { AccountCode = "0102", AccountName = "Non-Current / Fixed Assets", LevelNo = 2, ParentAccountId = assets.Id, NatureOfAccountId = debtorNature?.Id, Transable = false, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };
                var currentLiab = new ChartOfAccount { AccountCode = "0201", AccountName = "Current Liabilities", LevelNo = 2, ParentAccountId = liab.Id, NatureOfAccountId = creditorNature?.Id, Transable = false, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };
                var operatingExp = new ChartOfAccount { AccountCode = "0501", AccountName = "Operating & General Expenses", LevelNo = 2, ParentAccountId = expense.Id, NatureOfAccountId = debtorNature?.Id, Transable = false, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };

                await _context.ChartOfAccounts.AddRangeAsync(currentAssets, nonCurrentAssets, currentLiab, operatingExp);
                await _context.SaveChangesAsync();

                // Level 3: Subchild Accounts (0101001, 0101002, 0201001, 0201002, 0501001)
                var cashBankGroup = new ChartOfAccount { AccountCode = "0101001", AccountName = "Cash & Bank Balances", LevelNo = 3, ParentAccountId = currentAssets.Id, NatureOfAccountId = debtorNature?.Id, Transable = false, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };
                var receivablesGroup = new ChartOfAccount { AccountCode = "0101002", AccountName = "Customer Receivables", LevelNo = 3, ParentAccountId = currentAssets.Id, NatureOfAccountId = debtorNature?.Id, Transable = false, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };
                var payablesGroup = new ChartOfAccount { AccountCode = "0201001", AccountName = "Supplier Payables", LevelNo = 3, ParentAccountId = currentLiab.Id, NatureOfAccountId = creditorNature?.Id, Transable = false, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };
                var vatPayableGroup = new ChartOfAccount { AccountCode = "0201002", AccountName = "Tax & VAT Payables", LevelNo = 3, ParentAccountId = currentLiab.Id, NatureOfAccountId = creditorNature?.Id, Transable = false, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };
                var expenseGroup = new ChartOfAccount { AccountCode = "0501001", AccountName = "Administrative & Financial Expenses", LevelNo = 3, ParentAccountId = operatingExp.Id, NatureOfAccountId = debtorNature?.Id, Transable = false, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };

                await _context.ChartOfAccounts.AddRangeAsync(cashBankGroup, receivablesGroup, payablesGroup, vatPayableGroup, expenseGroup);
                await _context.SaveChangesAsync();

                // Level 4: Subchild Posting / Transable Accounts (01010010001, 01010010002, 01010020001, 02010010001, 02010020001, 05010010001, 05010010002)
                var cashInHand = new ChartOfAccount { AccountCode = "01010010001", AccountName = "Cash in Hand", LevelNo = 4, ParentAccountId = cashBankGroup.Id, NatureOfAccountId = debtorNature?.Id, Transable = true, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };
                var bankAccounts = new ChartOfAccount { AccountCode = "01010010002", AccountName = "Main Bank Account", LevelNo = 4, ParentAccountId = cashBankGroup.Id, NatureOfAccountId = debtorNature?.Id, Transable = true, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };
                var customerReceivables = new ChartOfAccount { AccountCode = "01010020001", AccountName = "Trade Debtors / Customer Account", LevelNo = 4, ParentAccountId = receivablesGroup.Id, NatureOfAccountId = debtorNature?.Id, Transable = true, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };
                var supplierPayables = new ChartOfAccount { AccountCode = "02010010001", AccountName = "Trade Creditors / Supplier Account", LevelNo = 4, ParentAccountId = payablesGroup.Id, NatureOfAccountId = creditorNature?.Id, Transable = true, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };
                var vatOutput = new ChartOfAccount { AccountCode = "02010020001", AccountName = "VAT Output Account", LevelNo = 4, ParentAccountId = vatPayableGroup.Id, NatureOfAccountId = creditorNature?.Id, Transable = true, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };
                var vatInput = new ChartOfAccount { AccountCode = "05010010001", AccountName = "VAT Input Account", LevelNo = 4, ParentAccountId = expenseGroup.Id, NatureOfAccountId = debtorNature?.Id, Transable = true, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };
                var bankCharges = new ChartOfAccount { AccountCode = "05010010002", AccountName = "Bank Charges Account", LevelNo = 4, ParentAccountId = expenseGroup.Id, NatureOfAccountId = debtorNature?.Id, Transable = true, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now };

                await _context.ChartOfAccounts.AddRangeAsync(cashInHand, bankAccounts, customerReceivables, supplierPayables, vatOutput, vatInput, bankCharges);
                await _context.SaveChangesAsync();
            }
        }
    }
}
