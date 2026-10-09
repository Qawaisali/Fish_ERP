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

namespace ERP_Core.Controllers.AccountSetup
{
    [Authorize]
    [Route("AccountSetup")]
    [Route("account-setup")]
    public class AccountSetupController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<AccountSetupController> _logger;

        public AccountSetupController(ERPDbContext context, ILogger<AccountSetupController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // =========================================================================
        // 1. GET: INDEX - Main Form & Setup Overview
        // =========================================================================
        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(int? companyId)
        {
            ViewData["Title"] = "Account Setup";
            ViewData["SubTitle"] = "Configure default chart of accounts posting heads for customers, suppliers, taxes, and bank charges.";
            ViewData["ActivePage"] = "AccountSetup";

            await EnsureTableCreatedAsync();

            var companies = await _context.CCompanies.AsNoTracking().OrderBy(c => c.CompanyName).ToListAsync();

            // Determine active company: Query Param -> Session -> Claims -> First Company
            int selectedCompanyId = companyId ?? 0;
            if (selectedCompanyId <= 0)
            {
                selectedCompanyId = HttpContext.Session.GetInt32("CompanyId") ?? 0;
            }
            if (selectedCompanyId <= 0)
            {
                var claimCompanyId = User.FindFirst("CompanyId")?.Value;
                int.TryParse(claimCompanyId, out selectedCompanyId);
            }
            if (selectedCompanyId <= 0 && companies.Any())
            {
                selectedCompanyId = companies.First().CompanyId;
            }

            // 1. Ensure default accounts exist if table is fresh
            await EnsureDefaultAccountsAsync();

            // 2. Fetch Chart of Accounts for dropdown selection where Transable == true directly from ChartOfAccounts table
            var accounts = await _context.ChartOfAccounts
                .AsNoTracking()
                .Where(c => c.Transable == true)
                .OrderBy(c => c.AccountCode)
                .ThenBy(c => c.AccountName)
                .Select(c => new ChartOfAccountOptionDto
                {
                    Id = c.Id,
                    AccountCode = c.AccountCode ?? "",
                    AccountName = c.AccountName ?? "",
                    Nature = "",
                    Transable = c.Transable
                })
                .ToListAsync();

            ViewBag.AccountsCount = accounts.Count;
            _logger.LogInformation("AccountSetup: Loaded {Count} transable accounts from ChartofAccounts table.", accounts.Count);

            // Fetch existing setup for selected company
            Models.AccountSetup? currentSetup = null;
            if (selectedCompanyId > 0)
            {
                currentSetup = await _context.AccountSetups
                    .FirstOrDefaultAsync(s => s.CompanyId == selectedCompanyId);
            }

            if (currentSetup == null)
            {
                currentSetup = new Models.AccountSetup
                {
                    CompanyId = selectedCompanyId
                };
            }

            // Fetch all configured setups for overview table
            var allSetups = await _context.AccountSetups.AsNoTracking().ToListAsync();
            var allAccountsLookup = await _context.ChartOfAccounts
                .AsNoTracking()
                .Select(c => new { c.Id, Display = string.IsNullOrWhiteSpace(c.AccountCode) ? c.AccountName : $"{c.AccountCode} — {c.AccountName}" })
                .ToListAsync();
            var accountsDict = allAccountsLookup.ToDictionary(a => a.Id, a => a.Display);
            var companiesDict = companies.ToDictionary(c => c.CompanyId, c => c.CompanyName);

            var summaryList = allSetups.Select(s => new AccountSetupSummaryDto
            {
                AccountSetupId = s.AccountSetupId,
                CompanyId = s.CompanyId,
                CompanyName = s.CompanyId.HasValue && companiesDict.ContainsKey(s.CompanyId.Value) 
                    ? companiesDict[s.CompanyId.Value] 
                    : (s.CompanyId.HasValue ? $"Company #{s.CompanyId.Value}" : "General"),
                SupplierAccountName = s.SupplierAccID.HasValue && accountsDict.ContainsKey(s.SupplierAccID.Value) 
                    ? accountsDict[s.SupplierAccID.Value] 
                    : "Not Set",
                CustomerAccountName = s.CustomerAccID.HasValue && accountsDict.ContainsKey(s.CustomerAccID.Value) 
                    ? accountsDict[s.CustomerAccID.Value] 
                    : "Not Set",
                VatInputAccountName = s.VatInputAccID.HasValue && accountsDict.ContainsKey(s.VatInputAccID.Value) 
                    ? accountsDict[s.VatInputAccID.Value] 
                    : "Not Set",
                VatOutputAccountName = s.VatOutputAccID.HasValue && accountsDict.ContainsKey(s.VatOutputAccID.Value) 
                    ? accountsDict[s.VatOutputAccID.Value] 
                    : "Not Set",
                BankChargesAccountName = s.BankChargesId.HasValue && accountsDict.ContainsKey(s.BankChargesId.Value) 
                    ? accountsDict[s.BankChargesId.Value] 
                    : "Not Set"
            }).ToList();

            var viewModel = new AccountSetupViewModel
            {
                Form = currentSetup,
                SelectedCompanyId = selectedCompanyId,
                AvailableCompanies = companies,
                AvailableAccounts = accounts,
                ConfiguredSetups = summaryList
            };

            return View(viewModel);
        }

        // =========================================================================
        // 2. GET: AJAX - Fetch AccountSetup for Company
        // =========================================================================
        [HttpGet]
        [Route("GetByCompany/{companyId}")]
        public async Task<IActionResult> GetByCompany(int companyId)
        {
            await EnsureTableCreatedAsync();

            var setup = await _context.AccountSetups
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.CompanyId == companyId);

            if (setup == null)
            {
                return Json(new
                {
                    success = true,
                    exists = false,
                    data = new Models.AccountSetup
                    {
                        CompanyId = companyId
                    }
                });
            }

            return Json(new
            {
                success = true,
                exists = true,
                data = setup
            });
        }

        // =========================================================================
        // 3. POST: SAVE - Create or Update AccountSetup
        // =========================================================================
        [HttpPost]
        [Route("Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Models.AccountSetup model)
        {
            if (!model.CompanyId.HasValue || model.CompanyId.Value <= 0)
            {
                TempData["ErrorMessage"] = "Please select a target company to configure account setup.";
                return RedirectToAction(nameof(Index));
            }

            await EnsureTableCreatedAsync();

            try
            {
                // Look for existing record by AccountSetupId or CompanyId
                Models.AccountSetup? existing = null;
                if (model.AccountSetupId > 0)
                {
                    existing = await _context.AccountSetups.FirstOrDefaultAsync(s => s.AccountSetupId == model.AccountSetupId);
                }
                
                if (existing == null && model.CompanyId.HasValue)
                {
                    existing = await _context.AccountSetups.FirstOrDefaultAsync(s => s.CompanyId == model.CompanyId.Value);
                }

                if (existing != null)
                {
                    existing.CompanyId = model.CompanyId;
                    existing.SupplierAccID = model.SupplierAccID;
                    existing.CustomerAccID = model.CustomerAccID;
                    existing.VatInputAccID = model.VatInputAccID;
                    existing.VatOutputAccID = model.VatOutputAccID;
                    existing.BankChargesId = model.BankChargesId;

                    _context.AccountSetups.Update(existing);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Account Setup configuration updated successfully.";
                }
                else
                {
                    var newSetup = new Models.AccountSetup
                    {
                        CompanyId = model.CompanyId,
                        SupplierAccID = model.SupplierAccID,
                        CustomerAccID = model.CustomerAccID,
                        VatInputAccID = model.VatInputAccID,
                        VatOutputAccID = model.VatOutputAccID,
                        BankChargesId = model.BankChargesId
                    };

                    _context.AccountSetups.Add(newSetup);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Account Setup configuration created successfully.";
                }

                return RedirectToAction(nameof(Index), new { companyId = model.CompanyId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while saving Account Setup for company {CompanyId}", model.CompanyId);
                TempData["ErrorMessage"] = $"Failed to save Account Setup: {ex.Message}";
                return RedirectToAction(nameof(Index), new { companyId = model.CompanyId });
            }
        }

        // =========================================================================
        // 4. POST: CLEAR - Reset or Delete AccountSetup for Company
        // =========================================================================
        [HttpPost]
        [Route("Clear")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Clear(int companyId)
        {
            if (companyId <= 0)
            {
                TempData["ErrorMessage"] = "No company selected to clear.";
                return RedirectToAction(nameof(Index));
            }

            await EnsureTableCreatedAsync();

            try
            {
                var existing = await _context.AccountSetups.FirstOrDefaultAsync(s => s.CompanyId == companyId);
                if (existing != null)
                {
                    _context.AccountSetups.Remove(existing);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Account Setup for selected company has been cleared.";
                }
                else
                {
                    TempData["SuccessMessage"] = "Account Setup fields have been reset.";
                }

                return RedirectToAction(nameof(Index), new { companyId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing Account Setup for company {CompanyId}", companyId);
                TempData["ErrorMessage"] = $"Failed to clear Account Setup: {ex.Message}";
                return RedirectToAction(nameof(Index), new { companyId });
            }
        }

        // =========================================================================
        // 5. HELPER: Auto-ensure AccountSetup schema exists in database
        // =========================================================================
        private async Task EnsureTableCreatedAsync()
        {
            try
            {
                const string sql = @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AccountSetup')
BEGIN
    CREATE TABLE [dbo].[AccountSetup] (
        [AccountSetupId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [CompanyId] INT NULL,
        [SupplierAccID] INT NULL,
        [CustomerAccID] INT NULL,
        [VatInputAccID] INT NULL,
        [VatOutputAccID] INT NULL,
        [BankChargesId] INT NULL
    );
END";
                await _context.Database.ExecuteSqlRawAsync(sql);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not verify/create AccountSetup table schema automatically.");
            }
        }

        // =========================================================================
        // 6. HELPER: Auto-ensure default Starter Accounts exist if table is fresh
        // =========================================================================
        private async Task EnsureDefaultAccountsAsync()
        {
            try
            {
                if (!await _context.ChartOfAccounts.AnyAsync())
                {
                    var debtorNature = await _context.NatureofAccounts.FirstOrDefaultAsync(n => n.DebtorOrCreditor == "D");
                    var creditorNature = await _context.NatureofAccounts.FirstOrDefaultAsync(n => n.DebtorOrCreditor == "C");

                    var defaultAccounts = new List<ChartOfAccount>
                    {
                        new ChartOfAccount { AccountCode = "1000", AccountName = "Assets", Transable = false, Active = true, NatureOfAccountId = debtorNature?.Id, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now },
                        new ChartOfAccount { AccountCode = "2000", AccountName = "Liabilities", Transable = false, Active = true, NatureOfAccountId = creditorNature?.Id, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now },
                        new ChartOfAccount { AccountCode = "3000", AccountName = "Equity & Capital", Transable = false, Active = true, NatureOfAccountId = creditorNature?.Id, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now },
                        new ChartOfAccount { AccountCode = "4000", AccountName = "Revenue & Sales", Transable = false, Active = true, NatureOfAccountId = creditorNature?.Id, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now },
                        new ChartOfAccount { AccountCode = "5000", AccountName = "Expenses", Transable = false, Active = true, NatureOfAccountId = debtorNature?.Id, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now }
                    };

                    await _context.ChartOfAccounts.AddRangeAsync(defaultAccounts);
                    await _context.SaveChangesAsync();

                    var assets = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.AccountCode == "1000");
                    var liab = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.AccountCode == "2000");
                    var exp = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.AccountCode == "5000");

                    var subAccounts = new List<ChartOfAccount>();
                    if (assets != null)
                    {
                        subAccounts.Add(new ChartOfAccount { AccountCode = "1001", AccountName = "Cash in Hand", ParentAccountId = assets.Id, NatureOfAccountId = debtorNature?.Id, Transable = true, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now });
                        subAccounts.Add(new ChartOfAccount { AccountCode = "1002", AccountName = "Bank Accounts", ParentAccountId = assets.Id, NatureOfAccountId = debtorNature?.Id, Transable = true, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now });
                        subAccounts.Add(new ChartOfAccount { AccountCode = "1003", AccountName = "Customer Receivables", ParentAccountId = assets.Id, NatureOfAccountId = debtorNature?.Id, Transable = true, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now });
                    }

                    if (liab != null)
                    {
                        subAccounts.Add(new ChartOfAccount { AccountCode = "2001", AccountName = "Supplier Payables", ParentAccountId = liab.Id, NatureOfAccountId = creditorNature?.Id, Transable = true, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now });
                        subAccounts.Add(new ChartOfAccount { AccountCode = "2002", AccountName = "VAT Output Account", ParentAccountId = liab.Id, NatureOfAccountId = creditorNature?.Id, Transable = true, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now });
                    }

                    if (exp != null)
                    {
                        subAccounts.Add(new ChartOfAccount { AccountCode = "5001", AccountName = "VAT Input Account", ParentAccountId = exp.Id, NatureOfAccountId = debtorNature?.Id, Transable = true, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now });
                        subAccounts.Add(new ChartOfAccount { AccountCode = "5002", AccountName = "Bank Charges Account", ParentAccountId = exp.Id, NatureOfAccountId = debtorNature?.Id, Transable = true, Active = true, Currency = "PKR", CreatedBy = "System", CreatedOn = DateTime.Now });
                    }

                    if (subAccounts.Any())
                    {
                        await _context.ChartOfAccounts.AddRangeAsync(subAccounts);
                        await _context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not verify/seed default chart of accounts.");
            }
        }
    }
}
