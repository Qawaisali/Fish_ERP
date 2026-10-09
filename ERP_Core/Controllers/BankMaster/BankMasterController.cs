using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ERP_Core.Attributes;
using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP_Core.Controllers.BankMaster
{
    [Authorize]
    [FormAccessAuthorize("Bank Master")]
    [Route("BankMaster")]
    [Route("BankCash")]
    public class BankMasterController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<BankMasterController> _logger;

        public BankMasterController(ERPDbContext context, ILogger<BankMasterController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Bank Master";
            ViewData["SubTitle"] = "Bank accounts and cash ledgers management.";

            int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;

            var banks = await _context.BankMasters
                .Include(b => b.ChartOfAccount)
                .Include(b => b.ParentBank)
                .Where(b => b.CompanyId == null || b.CompanyId == companyId)
                .OrderBy(b => b.Id)
                .ToListAsync();

            var listDtos = banks.Select(b => new BankMasterListDto
            {
                Id = b.Id,
                BankName = b.BankName,
                ParentBankId = b.ParentBankId ?? 0,
                ParentBankName = b.ParentBank?.BankName ?? (b.ParentBankId.HasValue && b.ParentBankId > 0 ? b.ParentBankId.ToString() ?? "None" : "None"),
                AccountName = b.AccountName ?? "",
                AccountCode = b.ChartOfAccount?.AccountCode ?? (b.ChartOfAccountId.HasValue ? b.ChartOfAccountId.ToString() ?? "" : ""),
                AccountNo = b.AccountNo ?? "",
                IbanNo = b.IbanNo ?? "",
                SwiftCode = b.SwiftCode ?? "",
                Active = b.Active ?? true,
                IsBank = b.IsBank,
                IsCash = b.IsCash,
                ChartOfAccountId = b.ChartOfAccountId
            }).ToList();

            // Populate Chart of Accounts Dropdown (Postable/Transable accounts or All)
            var coaAccounts = await _context.ChartOfAccounts
                .Where(c => c.Active == true)
                .OrderBy(c => c.AccountCode)
                .Select(c => new CoaAccountDropdownDto
                {
                    Id = c.Id,
                    AccountCode = c.AccountCode ?? "",
                    AccountName = c.AccountName
                })
                .ToListAsync();

            // Populate Parent Bank Dropdown
            var parentBankOptions = banks
                .Select(b => new ParentBankDropdownDto
                {
                    Id = b.Id,
                    BankName = b.BankName
                })
                .ToList();

            // Build hierarchical tree
            var treeNodes = BuildBankTree(banks);

            var model = new BankMasterViewModel
            {
                Banks = listDtos,
                TreeNodes = treeNodes,
                AvailableAccounts = coaAccounts,
                AvailableParentBanks = parentBankOptions,
                Form = new Models.BankMaster { Active = true, IsBank = true, IsCash = false }
            };

            return View(model);
        }

        [HttpGet]
        [Route("GetById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var bank = await _context.BankMasters
                .Include(b => b.ChartOfAccount)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bank == null)
            {
                return NotFound(new { success = false, message = "Bank record not found." });
            }

            return Json(new
            {
                success = true,
                data = new
                {
                    id = bank.Id,
                    bankName = bank.BankName,
                    parentBankId = bank.ParentBankId ?? 0,
                    accountNo = bank.AccountNo ?? "",
                    accountName = bank.AccountName ?? "",
                    ibanNo = bank.IbanNo ?? "",
                    swiftCode = bank.SwiftCode ?? "",
                    chartOfAccountId = bank.ChartOfAccountId,
                    accountCode = bank.ChartOfAccount?.AccountCode ?? "",
                    active = bank.Active ?? true,
                    isBank = bank.IsBank,
                    isCash = bank.IsCash
                }
            });
        }

        [HttpPost]
        [Route("Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Models.BankMaster model)
        {
            if (string.IsNullOrWhiteSpace(model.BankName))
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return BadRequest(new { success = false, message = "Bank Name is required." });
                }
                TempData["ErrorMessage"] = "Bank Name is required.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;
                bool compExists = await _context.CCompanies.AnyAsync(c => c.CompanyId == companyId);
                int? finalCompId = compExists ? companyId : (await _context.CCompanies.Select(c => (int?)c.CompanyId).FirstOrDefaultAsync());
                string userName = User.Identity?.Name ?? "System";

                // Sanitize ParentBankId (must be null if 0, negative, self-referencing, or not found in BankMasters)
                int? parentBankId = (model.ParentBankId.HasValue && model.ParentBankId.Value > 0) ? model.ParentBankId.Value : null;
                if (parentBankId.HasValue)
                {
                    if (model.Id > 0 && parentBankId.Value == model.Id)
                    {
                        parentBankId = null; // Cannot be parent of itself
                    }
                    else
                    {
                        bool parentExists = await _context.BankMasters.AnyAsync(b => b.Id == parentBankId.Value);
                        if (!parentExists)
                        {
                            parentBankId = null;
                        }
                    }
                }

                // Sanitize ChartOfAccountId
                int? coaId = (model.ChartOfAccountId.HasValue && model.ChartOfAccountId.Value > 0) ? model.ChartOfAccountId.Value : null;
                if (coaId.HasValue)
                {
                    bool coaExists = await _context.ChartOfAccounts.AnyAsync(c => c.Id == coaId.Value);
                    if (!coaExists)
                    {
                        coaId = null;
                    }
                }

                if (model.Id > 0)
                {
                    // UPDATE
                    var existing = await _context.BankMasters.FirstOrDefaultAsync(b => b.Id == model.Id);
                    if (existing == null)
                    {
                        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        {
                            return NotFound(new { success = false, message = "Record not found." });
                        }
                        TempData["ErrorMessage"] = "Record not found.";
                        return RedirectToAction(nameof(Index));
                    }

                    existing.BankName = model.BankName.Trim();
                    existing.ParentBankId = parentBankId;
                    existing.AccountNo = model.AccountNo?.Trim();
                    existing.AccountName = model.AccountName?.Trim();
                    existing.IbanNo = model.IbanNo?.Trim();
                    existing.SwiftCode = model.SwiftCode?.Trim();
                    existing.ChartOfAccountId = coaId;
                    existing.IsBank = model.IsBank;
                    existing.IsCash = model.IsCash;
                    existing.Active = model.Active ?? true;
                    existing.ModifiedBy = userName;
                    existing.ModifiedOn = DateTime.Now;

                    _context.BankMasters.Update(existing);
                    await _context.SaveChangesAsync();

                    string msg = $"Bank / Cash '{existing.BankName}' updated successfully.";
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = true, message = msg });
                    }
                    TempData["SuccessMessage"] = msg;
                }
                else
                {
                    // CREATE
                    model.BankName = model.BankName.Trim();
                    model.ParentBankId = parentBankId;
                    model.AccountNo = model.AccountNo?.Trim();
                    model.AccountName = model.AccountName?.Trim();
                    model.IbanNo = model.IbanNo?.Trim();
                    model.SwiftCode = model.SwiftCode?.Trim();
                    model.ChartOfAccountId = coaId;
                    model.CompanyId = finalCompId;
                    model.Active = model.Active ?? true;
                    model.CreatedBy = userName;
                    model.CreatedOn = DateTime.Now;

                    await _context.BankMasters.AddAsync(model);
                    await _context.SaveChangesAsync();

                    string msg = $"Bank / Cash '{model.BankName}' added successfully.";
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = true, message = msg });
                    }
                    TempData["SuccessMessage"] = msg;
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving BankMaster");
                var innerDetail = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                string errorMsg = "An error occurred while saving the bank record: " + innerDetail;
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return StatusCode(500, new { success = false, message = errorMsg });
                }
                TempData["ErrorMessage"] = errorMsg;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [Route("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var bank = await _context.BankMasters
                    .Include(b => b.SubBanks)
                    .FirstOrDefaultAsync(b => b.Id == id);

                if (bank == null)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return NotFound(new { success = false, message = "Bank record not found." });
                    }
                    TempData["ErrorMessage"] = "Bank record not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (bank.SubBanks != null && bank.SubBanks.Any())
                {
                    string conflictMsg = $"Cannot delete '{bank.BankName}' because it has {bank.SubBanks.Count} child bank account(s) linked to it.";
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return BadRequest(new { success = false, message = conflictMsg });
                    }
                    TempData["ErrorMessage"] = conflictMsg;
                    return RedirectToAction(nameof(Index));
                }

                _context.BankMasters.Remove(bank);
                await _context.SaveChangesAsync();

                string successMsg = $"Bank '{bank.BankName}' deleted successfully.";
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = successMsg });
                }
                TempData["SuccessMessage"] = successMsg;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting BankMaster {Id}", id);
                string errorMsg = "Error deleting bank record: " + ex.Message;
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return StatusCode(500, new { success = false, message = errorMsg });
                }
                TempData["ErrorMessage"] = errorMsg;
                return RedirectToAction(nameof(Index));
            }
        }

        private static List<BankTreeNodeDto> BuildBankTree(List<Models.BankMaster> allBanks)
        {
            var nodes = new List<BankTreeNodeDto>();

            // Find roots (ParentBankId is 0, null, or references non-existent parent)
            var bankIds = allBanks.Select(b => b.Id).ToHashSet();
            var roots = allBanks.Where(b => !b.ParentBankId.HasValue || b.ParentBankId == 0 || !bankIds.Contains(b.ParentBankId.Value)).ToList();

            foreach (var root in roots)
            {
                nodes.Add(CreateTreeNode(root, allBanks));
            }

            return nodes;
        }

        private static BankTreeNodeDto CreateTreeNode(Models.BankMaster bank, List<Models.BankMaster> allBanks)
        {
            var node = new BankTreeNodeDto
            {
                Id = bank.Id,
                Name = bank.BankName,
                Code = bank.ChartOfAccount?.AccountCode ?? bank.ChartOfAccountId?.ToString() ?? "",
                AccountNo = bank.AccountNo ?? "",
                AccountName = bank.AccountName ?? "",
                Type = bank.IsCash ? "Cash" : "Bank",
                Active = bank.Active ?? true
            };

            var children = allBanks.Where(b => b.ParentBankId == bank.Id).ToList();
            foreach (var child in children)
            {
                node.Children.Add(CreateTreeNode(child, allBanks));
            }

            return node;
        }
    }
}
