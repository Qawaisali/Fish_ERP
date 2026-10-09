using ERP_Core.Attributes;
using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ERP_Core.Controllers.ProductCategory
{
    [Authorize]
    [FormAccessAuthorize("Product Category")]
    [Route("ProductCategory")]
    [Route("product-category")]
    public class ProductCategoryController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<ProductCategoryController> _logger;

        public ProductCategoryController(ERPDbContext context, ILogger<ProductCategoryController> logger)
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
            ViewData["Title"] = "Product Category Master";
            ViewData["SubTitle"] = "Define item categories and link default financial posting accounts.";
            ViewData["ActivePage"] = "ProductCategory";

            try
            {
                await EnsureDefaultCategoriesAsync();

                var query = _context.ProductCategories
                    .Include(c => c.PurchaseAccount)
                    .Include(c => c.SaleAccount)
                    .Include(c => c.PurchaseReturnAccount)
                    .Include(c => c.SaleReturnAccount)
                    .AsNoTracking()
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    string term = search.Trim().ToLower();
                    query = query.Where(c =>
                        (c.Name != null && c.Name.ToLower().Contains(term)) ||
                        (c.Code != null && c.Code.ToLower().Contains(term))
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

                var rawList = await query
                    .OrderBy(c => c.Code)
                    .ThenBy(c => c.Name)
                    .ToListAsync();

                var allCategories = await _context.ProductCategories.AsNoTracking().ToListAsync();

                // Load only TRANSABLE Chart of Accounts for linking
                var transableAccounts = await _context.ChartOfAccounts
                    .AsNoTracking()
                    .Where(a => a.Transable && (a.Active == true || a.Active == null))
                    .OrderBy(a => a.AccountCode)
                    .ThenBy(a => a.AccountName)
                    .Select(a => new ChartOfAccountDropdownDto
                    {
                        Id = a.Id,
                        AccountCode = a.AccountCode,
                        AccountName = a.AccountName
                    })
                    .ToListAsync();

                var categoryDtos = rawList.Select(c => new ProductCategoryListDto
                {
                    Id = c.Id,
                    Code = c.Code ?? $"CTG-{c.Id:D4}",
                    Name = c.Name,
                    Active = c.Active,
                    PurchaseAccountId = c.PurchaseAccountId,
                    PurchaseAccountName = c.PurchaseAccount?.AccountName,
                    PurchaseAccountCode = c.PurchaseAccount?.AccountCode,
                    SaleAccountId = c.SaleAccountId,
                    SaleAccountName = c.SaleAccount?.AccountName,
                    SaleAccountCode = c.SaleAccount?.AccountCode,
                    PurchaseReturnAccountId = c.PurchaseReturnAccountId,
                    PurchaseReturnAccountName = c.PurchaseReturnAccount?.AccountName,
                    PurchaseReturnAccountCode = c.PurchaseReturnAccount?.AccountCode,
                    SaleReturnAccountId = c.SaleReturnAccountId,
                    SaleReturnAccountName = c.SaleReturnAccount?.AccountName,
                    SaleReturnAccountCode = c.SaleReturnAccount?.AccountCode,
                    CreatedOn = c.CreatedOn,
                    CreatedBy = c.CreatedBy
                }).ToList();

                var viewModel = new ProductCategoryViewModel
                {
                    Categories = categoryDtos,
                    AvailableTransableAccounts = transableAccounts,
                    TotalCategories = allCategories.Count,
                    ActiveCategories = allCategories.Count(c => c.Active == true || c.Active == null),
                    InactiveCategories = allCategories.Count(c => c.Active == false),
                    Form = new Models.ProductCategory
                    {
                        Active = true
                    }
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Product Categories");
                TempData["ErrorMessage"] = "Failed to load product categories: " + ex.Message;
                return View(new ProductCategoryViewModel());
            }
        }

        // =========================================================================
        // 2. GET BY ID (JSON for AJAX Edit Form)
        // =========================================================================
        [HttpGet]
        [Route("GetById/{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var cat = await _context.ProductCategories
                    .Include(c => c.PurchaseAccount)
                    .Include(c => c.SaleAccount)
                    .Include(c => c.PurchaseReturnAccount)
                    .Include(c => c.SaleReturnAccount)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (cat == null)
                {
                    return Json(new { success = false, message = "Category not found." });
                }

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        id = cat.Id,
                        code = cat.Code ?? string.Empty,
                        name = cat.Name,
                        active = cat.Active ?? true,
                        purchaseAccountId = cat.PurchaseAccountId,
                        saleAccountId = cat.SaleAccountId,
                        purchaseReturnAccountId = cat.PurchaseReturnAccountId,
                        saleReturnAccountId = cat.SaleReturnAccountId
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product category ID {Id}", id);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================================================================
        // 3. GET NEXT CATEGORY CODE (AJAX Helper)
        // =========================================================================
        [HttpGet]
        [Route("GetNextCode")]
        public async Task<IActionResult> GetNextCode()
        {
            try
            {
                string nextCode = await GenerateNextCategoryCodeAsync();
                return Json(new { success = true, nextCode });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================================================================
        // 4. CREATE PRODUCT CATEGORY
        // =========================================================================
        [HttpPost]
        [Route("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Models.ProductCategory model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                TempData["ErrorMessage"] = "Category Name is required.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                SanitizeCategory(model);

                if (string.IsNullOrWhiteSpace(model.Code))
                {
                    model.Code = await GenerateNextCategoryCodeAsync();
                }

                if (!string.IsNullOrWhiteSpace(model.Code))
                {
                    bool codeExists = await _context.ProductCategories.AnyAsync(c => c.Code == model.Code);
                    if (codeExists)
                    {
                        TempData["ErrorMessage"] = $"Category Code '{model.Code}' is already in use.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                bool isActive = true;
                if (Request.HasFormContentType && Request.Form.ContainsKey("Active"))
                {
                    var val = Request.Form["Active"].ToString().ToLowerInvariant();
                    isActive = val.Contains("true") || val.Contains("on") || val == "1";
                }
                model.Active = isActive;

                model.CreatedOn = DateTime.Now;
                model.CreatedBy = User.Identity?.Name ?? "System";
                model.CompanyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;

                await _context.ProductCategories.AddAsync(model);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Product Category '{Name}' ({Code}) created successfully", model.Name, model.Code);
                TempData["SuccessMessage"] = $"Product Category '{model.Name}' ({model.Code}) added successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category '{Name}'", model.Name);
                TempData["ErrorMessage"] = "Failed to create category: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 5. EDIT PRODUCT CATEGORY
        // =========================================================================
        [HttpPost]
        [Route("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Models.ProductCategory model)
        {
            if (model.Id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid Category ID.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                TempData["ErrorMessage"] = "Category Name is required.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var existing = await _context.ProductCategories.FirstOrDefaultAsync(c => c.Id == model.Id);
                if (existing == null)
                {
                    TempData["ErrorMessage"] = "Category record not found.";
                    return RedirectToAction(nameof(Index));
                }

                SanitizeCategory(model);

                if (!string.IsNullOrWhiteSpace(model.Code))
                {
                    bool codeExists = await _context.ProductCategories.AnyAsync(c => c.Id != model.Id && c.Code == model.Code);
                    if (codeExists)
                    {
                        TempData["ErrorMessage"] = $"Category Code '{model.Code}' is already used by another category.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                bool isActive = true;
                if (Request.HasFormContentType && Request.Form.ContainsKey("Active"))
                {
                    var val = Request.Form["Active"].ToString().ToLowerInvariant();
                    isActive = val.Contains("true") || val.Contains("on") || val == "1";
                }

                existing.Code = model.Code;
                existing.Name = model.Name;
                existing.Active = isActive;
                existing.PurchaseAccountId = (model.PurchaseAccountId.HasValue && model.PurchaseAccountId.Value > 0) ? model.PurchaseAccountId : null;
                existing.SaleAccountId = (model.SaleAccountId.HasValue && model.SaleAccountId.Value > 0) ? model.SaleAccountId : null;
                existing.PurchaseReturnAccountId = (model.PurchaseReturnAccountId.HasValue && model.PurchaseReturnAccountId.Value > 0) ? model.PurchaseReturnAccountId : null;
                existing.SaleReturnAccountId = (model.SaleReturnAccountId.HasValue && model.SaleReturnAccountId.Value > 0) ? model.SaleReturnAccountId : null;
                existing.ModifiedOn = DateTime.Now;
                existing.ModifiedBy = User.Identity?.Name ?? "System";

                _context.ProductCategories.Update(existing);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Product Category '{Name}' (ID: {Id}) updated successfully", existing.Name, existing.Id);
                TempData["SuccessMessage"] = $"Product Category '{existing.Name}' updated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category ID {Id}", model.Id);
                TempData["ErrorMessage"] = "Failed to update category: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 6. UNIFIED SAVE ACTION
        // =========================================================================
        [HttpPost]
        [Route("Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Models.ProductCategory model)
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
                var cat = await _context.ProductCategories.FirstOrDefaultAsync(c => c.Id == id);
                if (cat == null)
                {
                    TempData["ErrorMessage"] = "Category record not found.";
                    return RedirectToAction(nameof(Index));
                }

                cat.Active = !(cat.Active ?? true);
                cat.ModifiedOn = DateTime.Now;
                cat.ModifiedBy = User.Identity?.Name ?? "System";

                _context.ProductCategories.Update(cat);
                await _context.SaveChangesAsync();

                string statusText = (cat.Active == true) ? "Activated" : "Deactivated";
                TempData["SuccessMessage"] = $"Product Category '{cat.Name}' {statusText} successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling status for category ID {Id}", id);
                TempData["ErrorMessage"] = "Failed to update status: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 8. DELETE CATEGORY
        // =========================================================================
        [HttpPost]
        [Route("Delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var cat = await _context.ProductCategories.FirstOrDefaultAsync(c => c.Id == id);
                if (cat == null)
                {
                    TempData["ErrorMessage"] = "Category record not found.";
                    return RedirectToAction(nameof(Index));
                }

                _context.ProductCategories.Remove(cat);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Product Category '{Name}' (ID: {Id}) deleted successfully", cat.Name, id);
                TempData["SuccessMessage"] = $"Product Category '{cat.Name}' deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category ID {Id}", id);
                TempData["ErrorMessage"] = "Failed to delete category: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // PRIVATE HELPERS
        // =========================================================================
        private static void SanitizeCategory(Models.ProductCategory model)
        {
            model.Name = model.Name?.Trim() ?? string.Empty;
            model.Code = model.Code?.Trim();
            if (model.PurchaseAccountId.HasValue && model.PurchaseAccountId.Value <= 0) model.PurchaseAccountId = null;
            if (model.SaleAccountId.HasValue && model.SaleAccountId.Value <= 0) model.SaleAccountId = null;
            if (model.PurchaseReturnAccountId.HasValue && model.PurchaseReturnAccountId.Value <= 0) model.PurchaseReturnAccountId = null;
            if (model.SaleReturnAccountId.HasValue && model.SaleReturnAccountId.Value <= 0) model.SaleReturnAccountId = null;
        }

        private async Task<string> GenerateNextCategoryCodeAsync()
        {
            var codes = await _context.ProductCategories
                .Where(c => c.Code != null && c.Code.StartsWith("CTG-"))
                .Select(c => c.Code)
                .ToListAsync();

            int maxNum = 0;
            foreach (var code in codes)
            {
                if (!string.IsNullOrEmpty(code))
                {
                    var numStr = code.Replace("CTG-", "").Trim();
                    if (int.TryParse(numStr, out int parsed))
                    {
                        if (parsed > maxNum) maxNum = parsed;
                    }
                }
            }

            return $"CTG-{(maxNum + 1):D4}";
        }

        private async Task EnsureDefaultCategoriesAsync()
        {
            if (!await _context.ProductCategories.AnyAsync())
            {
                // Find sample transable accounts
                var salesAcc = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.Transable && a.AccountName.Contains("Sale"));
                var purchaseAcc = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.Transable && a.AccountName.Contains("Payable"));

                var defaults = new List<Models.ProductCategory>
                {
                    new Models.ProductCategory { Code = "CTG-0001", Name = "Fresh Sea Fish", Active = true, SaleAccountId = salesAcc?.Id, PurchaseAccountId = purchaseAcc?.Id, CreatedBy = "System", CreatedOn = DateTime.Now },
                    new Models.ProductCategory { Code = "CTG-0002", Name = "River Fish (Meetha Pani)", Active = true, SaleAccountId = salesAcc?.Id, PurchaseAccountId = purchaseAcc?.Id, CreatedBy = "System", CreatedOn = DateTime.Now },
                    new Models.ProductCategory { Code = "CTG-0003", Name = "Shrimps & Prawns", Active = true, SaleAccountId = salesAcc?.Id, PurchaseAccountId = purchaseAcc?.Id, CreatedBy = "System", CreatedOn = DateTime.Now },
                    new Models.ProductCategory { Code = "CTG-0004", Name = "Frozen Seafood Export", Active = true, SaleAccountId = salesAcc?.Id, PurchaseAccountId = purchaseAcc?.Id, CreatedBy = "System", CreatedOn = DateTime.Now },
                    new Models.ProductCategory { Code = "CTG-0005", Name = "Dry Fish & Fish Powder", Active = true, SaleAccountId = salesAcc?.Id, PurchaseAccountId = purchaseAcc?.Id, CreatedBy = "System", CreatedOn = DateTime.Now }
                };

                await _context.ProductCategories.AddRangeAsync(defaults);
                await _context.SaveChangesAsync();
            }
        }
    }
}
