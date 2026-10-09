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

namespace ERP_Core.Controllers.FishMaster
{
    [Authorize]
    [FormAccessAuthorize("Fish Master")]
    [Route("FishMaster")]
    [Route("fish-master")]
    public class FishMasterController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<FishMasterController> _logger;

        public FishMasterController(ERPDbContext context, ILogger<FishMasterController> logger)
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
        public async Task<IActionResult> Index(string? search, string? type, int? categoryId, string? status)
        {
            ViewData["Title"] = "Fish Master";
            ViewData["SubTitle"] = "Machli ki varieties, codes, types aur product categories manage karein.";
            ViewData["ActivePage"] = "FishMaster";

            try
            {
                await EnsureDefaultFishesAsync();

                var query = _context.Fishes
                    .Include(f => f.Category)
                    .AsNoTracking()
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    string term = search.Trim().ToLower();
                    query = query.Where(f =>
                        (f.Name != null && f.Name.ToLower().Contains(term)) ||
                        (f.Code != null && f.Code.ToLower().Contains(term)) ||
                        (f.Type != null && f.Type.ToLower().Contains(term)) ||
                        (f.Category != null && f.Category.Name.ToLower().Contains(term)) ||
                        (f.Category != null && f.Category.Code != null && f.Category.Code.ToLower().Contains(term))
                    );
                }

                if (!string.IsNullOrWhiteSpace(type))
                {
                    query = query.Where(f => f.Type == type);
                }

                if (categoryId.HasValue && categoryId.Value > 0)
                {
                    query = query.Where(f => f.CategoryId == categoryId.Value);
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(f => f.Active == true || f.Active == null);
                    }
                    else if (status.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(f => f.Active == false);
                    }
                }

                var rawList = await query
                    .OrderBy(f => f.Code)
                    .ThenBy(f => f.Name)
                    .ToListAsync();

                var allFishes = await _context.Fishes.AsNoTracking().ToListAsync();

                // Load active Product Categories for dropdown
                var availableCategories = await _context.ProductCategories
                    .AsNoTracking()
                    .Where(c => c.Active == true || c.Active == null)
                    .OrderBy(c => c.Code)
                    .ThenBy(c => c.Name)
                    .Select(c => new ProductCategoryDropdownDto
                    {
                        Id = c.Id,
                        Code = c.Code ?? "",
                        Name = c.Name
                    })
                    .ToListAsync();

                var fishDtos = rawList.Select(f => new FishDto
                {
                    Id = f.Id,
                    Code = f.Code ?? $"PRO-{f.Id:D4}",
                    Name = f.Name,
                    Type = f.Type,
                    Unit = f.Unit ?? "Kg",
                    CategoryId = f.CategoryId,
                    CategoryName = f.Category?.Name,
                    CategoryCode = f.Category?.Code,
                    Active = f.Active,
                    CreatedOn = f.CreatedOn,
                    CreatedBy = f.CreatedBy
                }).ToList();

                var availableTypes = allFishes
                    .Where(f => !string.IsNullOrWhiteSpace(f.Type))
                    .Select(f => f.Type.Trim())
                    .Distinct()
                    .OrderBy(t => t)
                    .ToList();


                var viewModel = new FishMasterViewModel
                {
                    Fishes = fishDtos,
                    TotalFish = allFishes.Count,
                    ActiveFish = allFishes.Count(f => f.Active == true || f.Active == null),
                    InactiveFish = allFishes.Count(f => f.Active == false),
                    UniqueTypesCount = availableTypes.Count,
                    AvailableTypes = availableTypes,
                    AvailableCategories = availableCategories,
                    Form = new Fish
                    {
                        Type = "Sea Fish",
                        Unit = "Kg",
                        Active = true
                    }
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Fish Master");
                TempData["ErrorMessage"] = "Failed to load fish master records: " + ex.Message;
                return View(new FishMasterViewModel());
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
                var fish = await _context.Fishes
                    .Include(f => f.Category)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == id);

                if (fish == null)
                {
                    return Json(new { success = false, message = "Fish record not found." });
                }

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        id = fish.Id,
                        code = fish.Code ?? string.Empty,
                        name = fish.Name,
                        type = fish.Type,
                        unit = fish.Unit,
                        categoryId = fish.CategoryId,
                        categoryName = fish.Category?.Name ?? string.Empty,
                        active = fish.Active ?? true
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving fish ID {Id}", id);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================================================================
        // 3. GET NEXT FISH CODE (AJAX Helper)
        // =========================================================================
        [HttpGet]
        [Route("GetNextCode")]
        public async Task<IActionResult> GetNextCode()
        {
            try
            {
                string nextCode = await GenerateNextFishCodeAsync();
                return Json(new { success = true, nextCode });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================================================================
        // 4. CREATE FISH
        // =========================================================================
        [HttpPost]
        [Route("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Fish model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                TempData["ErrorMessage"] = "Fish Name is required.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                SanitizeFish(model);

                if (string.IsNullOrWhiteSpace(model.Code))
                {
                    model.Code = await GenerateNextFishCodeAsync();
                }

                // Check duplicate code
                if (!string.IsNullOrWhiteSpace(model.Code))
                {
                    bool codeExists = await _context.Fishes.AnyAsync(f => f.Code == model.Code);
                    if (codeExists)
                    {
                        TempData["ErrorMessage"] = $"Fish Code '{model.Code}' is already in use.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                // Explicit boolean check from Request.Form
                if (Request.HasFormContentType && Request.Form.ContainsKey("Active"))
                {
                    var val = Request.Form["Active"].ToString().ToLowerInvariant();
                    model.Active = val.Contains("true") || val.Contains("on") || val == "1";
                }

                model.CreatedOn = DateTime.Now;
                model.CreatedBy = User.Identity?.Name ?? "System";
                model.CompanyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;

                await _context.Fishes.AddAsync(model);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Fish '{Name}' (Code: {Code}) created successfully", model.Name, model.Code);
                TempData["SuccessMessage"] = $"Fish variety '{model.Name}' ({model.Code}) added successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating fish '{Name}'", model.Name);
                TempData["ErrorMessage"] = "Failed to add fish: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 5. EDIT FISH
        // =========================================================================
        [HttpPost]
        [Route("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Fish model)
        {
            if (model.Id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid Fish ID.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                TempData["ErrorMessage"] = "Fish Name is required.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var existing = await _context.Fishes.FirstOrDefaultAsync(f => f.Id == model.Id);
                if (existing == null)
                {
                    TempData["ErrorMessage"] = "Fish record not found.";
                    return RedirectToAction(nameof(Index));
                }

                SanitizeFish(model);

                // Check duplicate code on other records
                if (!string.IsNullOrWhiteSpace(model.Code))
                {
                    bool codeExists = await _context.Fishes.AnyAsync(f => f.Id != model.Id && f.Code == model.Code);
                    if (codeExists)
                    {
                        TempData["ErrorMessage"] = $"Fish Code '{model.Code}' is already used by another fish.";
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
                existing.Type = model.Type;
                existing.Unit = model.Unit;
                existing.CategoryId = (model.CategoryId.HasValue && model.CategoryId.Value > 0) ? model.CategoryId : null;
                existing.Active = isActive;
                existing.ModifiedOn = DateTime.Now;
                existing.ModifiedBy = User.Identity?.Name ?? "System";

                _context.Fishes.Update(existing);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Fish '{Name}' (ID: {Id}) updated successfully", existing.Name, existing.Id);
                TempData["SuccessMessage"] = $"Fish variety '{existing.Name}' updated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating fish ID {Id}", model.Id);
                TempData["ErrorMessage"] = "Failed to update fish: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 6. UNIFIED SAVE ACTION (For form submission)
        // =========================================================================
        [HttpPost]
        [Route("Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Fish model)
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
                var fish = await _context.Fishes.FirstOrDefaultAsync(f => f.Id == id);
                if (fish == null)
                {
                    TempData["ErrorMessage"] = "Fish record not found.";
                    return RedirectToAction(nameof(Index));
                }

                fish.Active = !(fish.Active ?? true);
                fish.ModifiedOn = DateTime.Now;
                fish.ModifiedBy = User.Identity?.Name ?? "System";

                _context.Fishes.Update(fish);
                await _context.SaveChangesAsync();

                string statusText = (fish.Active == true) ? "Activated" : "Deactivated";
                TempData["SuccessMessage"] = $"Fish '{fish.Name}' {statusText} successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling status for fish ID {Id}", id);
                TempData["ErrorMessage"] = "Failed to update status: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 8. DELETE FISH
        // =========================================================================
        [HttpPost]
        [Route("Delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var fish = await _context.Fishes.FirstOrDefaultAsync(f => f.Id == id);
                if (fish == null)
                {
                    TempData["ErrorMessage"] = "Fish record not found.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Fishes.Remove(fish);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Fish '{Name}' (ID: {Id}) deleted successfully", fish.Name, id);
                TempData["SuccessMessage"] = $"Fish variety '{fish.Name}' deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting fish ID {Id}", id);
                TempData["ErrorMessage"] = "Failed to delete fish: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // PRIVATE HELPERS
        // =========================================================================
        private static void SanitizeFish(Fish model)
        {
            model.Name = model.Name?.Trim() ?? string.Empty;
            model.Code = model.Code?.Trim();
            model.Type = string.IsNullOrWhiteSpace(model.Type) ? "Sea Fish" : model.Type.Trim();
            model.Unit = string.IsNullOrWhiteSpace(model.Unit) ? "Kg" : model.Unit.Trim();
            if (model.CategoryId.HasValue && model.CategoryId.Value <= 0) model.CategoryId = null;
            model.Active = model.Active ?? true;
        }

        private async Task<string> GenerateNextFishCodeAsync()
        {
            var codes = await _context.Fishes
                .Where(f => f.Code != null && f.Code.StartsWith("PRO-"))
                .Select(f => f.Code!)
                .ToListAsync();

            int maxNum = 0;
            foreach (var code in codes)
            {
                if (!string.IsNullOrEmpty(code))
                {
                    var numStr = code.Replace("PRO-", "").Trim();
                    if (int.TryParse(numStr, out int parsed))
                    {
                        if (parsed > maxNum) maxNum = parsed;
                    }
                }
            }

            int nextNum = maxNum + 1;
            string nextCode;
            do
            {
                nextCode = $"PRO-{nextNum:D4}";
                nextNum++;
            } while (codes.Contains(nextCode));

            return nextCode;
        }

        private async Task EnsureDefaultFishesAsync()
        {
            // Normalize any legacy fish codes with fewer than 4 digits (e.g. PRO-025 -> PRO-0025)
            try
            {
                var legacyFishes = await _context.Fishes
                    .Where(f => f.Code != null && f.Code.StartsWith("PRO-") && f.Code.Length < 8)
                    .ToListAsync();

                if (legacyFishes.Any())
                {
                    bool changed = false;
                    foreach (var f in legacyFishes)
                    {
                        var numStr = f.Code!.Replace("PRO-", "").Trim();
                        if (int.TryParse(numStr, out int parsed))
                        {
                            string standardCode = $"PRO-{parsed:D4}";
                            if (!_context.Fishes.Any(x => x.Code == standardCode && x.Id != f.Id))
                            {
                                f.Code = standardCode;
                                changed = true;
                            }
                        }
                    }
                    if (changed)
                    {
                        await _context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not normalize legacy fish codes: {Message}", ex.Message);
            }

            if (!await _context.Fishes.AnyAsync())
            {
                var defaults = new List<Fish>
                {
                    new Fish { Code = "PRO-0001", Name = "Rohu", Type = "River Fish", Unit = "Kg", Active = true, CreatedBy = "System", CreatedOn = DateTime.Now },
                    new Fish { Code = "PRO-0002", Name = "Pomfret (White)", Type = "Sea Fish", Unit = "Kg", Active = true, CreatedBy = "System", CreatedOn = DateTime.Now },
                    new Fish { Code = "PRO-0003", Name = "Sole Fish", Type = "Sea Fish", Unit = "Kg", Active = true, CreatedBy = "System", CreatedOn = DateTime.Now },
                    new Fish { Code = "PRO-0004", Name = "Shrimp (Jumbo / Tiger)", Type = "Shrimp / Prawn", Unit = "Kg", Active = true, CreatedBy = "System", CreatedOn = DateTime.Now },
                    new Fish { Code = "PRO-0005", Name = "Surmai (King Mackerel)", Type = "Sea Fish", Unit = "Kg", Active = true, CreatedBy = "System", CreatedOn = DateTime.Now },
                    new Fish { Code = "PRO-0006", Name = "Palla", Type = "River Fish", Unit = "Kg", Active = true, CreatedBy = "System", CreatedOn = DateTime.Now },
                    new Fish { Code = "PRO-0007", Name = "Heera (Red Snapper)", Type = "Sea Fish", Unit = "Kg", Active = true, CreatedBy = "System", CreatedOn = DateTime.Now },
                    new Fish { Code = "PRO-0008", Name = "Mushka", Type = "Sea Fish", Unit = "Kg", Active = true, CreatedBy = "System", CreatedOn = DateTime.Now }
                };

                await _context.Fishes.AddRangeAsync(defaults);
                await _context.SaveChangesAsync();
            }
        }
    }
}
