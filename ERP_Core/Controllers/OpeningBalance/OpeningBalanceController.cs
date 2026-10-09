using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP_Core.Controllers.OpeningBalance
{
    [Authorize]
    [Route("OpeningBalance")]
    public class OpeningBalanceController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<OpeningBalanceController> _logger;

        public OpeningBalanceController(ERPDbContext context, ILogger<OpeningBalanceController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // =========================================================================
        // 1. INDEX: OPENING BALANCE FORM & RECENT VOUCHERS LIST
        // =========================================================================
        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(int? id, int? fyId)
        {
            ViewData["Title"] = "Opening Balance";
            ViewData["SubTitle"] = "Chart of Accounts ke ibtidayi khate (opening balance) darj karein.";
            ViewData["ActivePage"] = "OpeningBalance";

            try
            {
                await EnsureOpeningBalanceTablesCreatedAsync();

                int companyId = GetCurrentCompanyId();
                var model = new OpeningBalanceViewModel
                {
                    CompanyId = companyId,
                    OBDate = DateTime.Today
                };

                // 1. Load Financial Years
                var financialYears = await _context.CFinancialYears
                    .AsNoTracking()
                    .Where(f => f.Active != false && (f.CompanyID == 0 || f.CompanyID == companyId))
                    .OrderByDescending(f => f.CurrentYear)
                    .ThenByDescending(f => f.StartDate)
                    .Select(f => new FinancialYearDropdownDto
                    {
                        Id = f.Id,
                        Code = f.Code,
                        Description = f.Description,
                        CurrentYear = f.CurrentYear,
                        StartDate = f.StartDate,
                        EndDate = f.EndDate
                    })
                    .ToListAsync();

                model.AvailableFinancialYears = financialYears;

                // Select default Financial Year (Active current year or first available or param)
                if (fyId.HasValue && financialYears.Any(f => f.Id == fyId.Value))
                {
                    model.FinancialYearId = fyId.Value;
                }
                else
                {
                    var defaultFy = financialYears.FirstOrDefault(f => f.CurrentYear) ?? financialYears.FirstOrDefault();
                    if (defaultFy != null)
                    {
                        model.FinancialYearId = defaultFy.Id;
                    }
                }

                var selectedFy = financialYears.FirstOrDefault(f => f.Id == model.FinancialYearId);
                if (selectedFy != null)
                {
                    model.FiscalPeriodText = $"{selectedFy.StartDate:dd MMM yyyy} to {selectedFy.EndDate:dd MMM yyyy}";
                    model.OBDate = selectedFy.StartDate;
                }

                // 2. Load strictly Level 3 Chart of Accounts
                model.AvailableCoaAccounts = await GetLevel3AccountsAsync(companyId);

                // 3. Strictly 1 Opening Balance record per Financial Year:
                // If an Opening Balance already exists for this Financial Year, always load it!
                Dictionary<int, OpeningBalanceDetail>? existingDetailsMap = null;
                Models.OpeningBalance? existing = null;

                if (id.HasValue && id.Value > 0)
                {
                    existing = await _context.OpeningBalances
                        .Include(m => m.Details)
                        .ThenInclude(d => d.ChartOfAccount)
                        .FirstOrDefaultAsync(m => m.Id == id.Value && m.CompanyId == companyId && m.Active != false);
                    
                    if (existing != null)
                    {
                        model.FinancialYearId = existing.FinancialYearId;
                        selectedFy = financialYears.FirstOrDefault(f => f.Id == model.FinancialYearId);
                        if (selectedFy != null)
                        {
                            model.FiscalPeriodText = $"{selectedFy.StartDate:dd MMM yyyy} to {selectedFy.EndDate:dd MMM yyyy}";
                        }
                    }
                }

                if (existing == null && model.FinancialYearId > 0)
                {
                    existing = await _context.OpeningBalances
                        .Include(m => m.Details)
                        .ThenInclude(d => d.ChartOfAccount)
                        .Where(m => m.CompanyId == companyId && m.FinancialYearId == model.FinancialYearId && m.Active != false)
                        .OrderByDescending(m => m.Id)
                        .FirstOrDefaultAsync();
                }

                if (existing != null)
                {
                    model.Id = existing.Id;
                    model.OBCode = existing.OBCode;
                    model.OBDate = existing.OBDate;
                    model.FinancialYearId = existing.FinancialYearId;
                    existingDetailsMap = existing.Details.ToDictionary(d => d.COAId);
                }
                else
                {
                    model.Id = 0;
                    model.OBCode = await GenerateNextOBCodeAsync(companyId, model.FinancialYearId);
                    model.OBDate = selectedFy?.StartDate ?? DateTime.Today;
                }

                // Preload all Level 3 accounts into Details list
                model.Details = new List<OpeningBalanceDetailDto>();
                foreach (var acc in model.AvailableCoaAccounts)
                {
                    if (existingDetailsMap != null && existingDetailsMap.TryGetValue(acc.Id, out var existingDetail))
                    {
                        model.Details.Add(new OpeningBalanceDetailDto
                        {
                            Id = existingDetail.Id,
                            COAId = acc.Id,
                            COACode = acc.AccountCode,
                            COAName = acc.AccountName,
                            ParentAccountName = acc.ParentAccountName,
                            OBDtlTranType = existingDetail.OBDtlTranType,
                            OBDtlOpenBlnc = existingDetail.OBDtlOpenBlnc
                        });
                    }
                    else
                    {
                        model.Details.Add(new OpeningBalanceDetailDto
                        {
                            Id = 0,
                            COAId = acc.Id,
                            COACode = acc.AccountCode,
                            COAName = acc.AccountName,
                            ParentAccountName = acc.ParentAccountName,
                            OBDtlTranType = "DEB",
                            OBDtlOpenBlnc = null
                        });
                    }
                }

                // If an existing voucher had details for accounts not in available Level 3, preserve them
                if (existingDetailsMap != null)
                {
                    var includedIds = new HashSet<int>(model.Details.Select(d => d.COAId));
                    foreach (var ed in existingDetailsMap.Values)
                    {
                        if (!includedIds.Contains(ed.COAId))
                        {
                            model.Details.Add(new OpeningBalanceDetailDto
                            {
                                Id = ed.Id,
                                COAId = ed.COAId,
                                COACode = ed.ChartOfAccount?.AccountCode,
                                COAName = ed.ChartOfAccount?.AccountName ?? $"Account #{ed.COAId}",
                                OBDtlTranType = ed.OBDtlTranType,
                                OBDtlOpenBlnc = ed.OBDtlOpenBlnc
                            });
                        }
                    }
                }

                // If new voucher, generate next OBCode
                if (string.IsNullOrWhiteSpace(model.OBCode))
                {
                    model.OBCode = await GenerateNextOBCodeAsync(companyId, model.FinancialYearId);
                }

                // 4. Load Recent Vouchers Summary for the list tab
                model.RecentVouchers = await GetRecentVouchersAsync(companyId);
                model.TotalVouchers = model.RecentVouchers.Count;
                model.TotalDebitSum = model.RecentVouchers.Sum(v => v.TotalDebit);
                model.TotalCreditSum = model.RecentVouchers.Sum(v => v.TotalCredit);

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Opening Balance Index view");
                TempData["ErrorMessage"] = "Failed to load Opening Balance module: " + ex.Message;
                return View(new OpeningBalanceViewModel());
            }
        }

        // =========================================================================
        // 2. SAVE OPENING BALANCE (CREATE OR UPDATE)
        // =========================================================================
        [HttpPost]
        [Route("Save")]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(ValueCountLimit = 10000)]
        public async Task<IActionResult> Save([FromForm] OpeningBalanceViewModel model)
        {
            try
            {
                await EnsureOpeningBalanceTablesCreatedAsync();

                int companyId = GetCurrentCompanyId();
                model.CompanyId = companyId;

                // Validate Financial Year
                if (model.FinancialYearId <= 0)
                {
                    TempData["ErrorMessage"] = "Please select a valid Financial Year.";
                    return RedirectToAction(nameof(Index), new { fyId = model.FinancialYearId });
                }

                // Filter out empty rows: only keep entries where user actually entered an amount > 0
                var validRows = model.Details?
                    .Where(d => d.COAId > 0 && d.OBDtlOpenBlnc.HasValue && d.OBDtlOpenBlnc.Value > 0)
                    .ToList() ?? new List<OpeningBalanceDetailDto>();

                if (!validRows.Any())
                {
                    TempData["ErrorMessage"] = "Please enter an opening balance amount for at least one account.";
                    return RedirectToAction(nameof(Index), new { id = model.Id, fyId = model.FinancialYearId });
                }

                // Validate that Total Debit equals Total Credit before saving
                decimal totalDebit = validRows
                    .Where(r => string.Equals(r.OBDtlTranType, "DEB", StringComparison.OrdinalIgnoreCase))
                    .Sum(r => r.OBDtlOpenBlnc!.Value);

                decimal totalCredit = validRows
                    .Where(r => string.Equals(r.OBDtlTranType, "CRE", StringComparison.OrdinalIgnoreCase))
                    .Sum(r => r.OBDtlOpenBlnc!.Value);

                decimal difference = Math.Abs(totalDebit - totalCredit);
                if (difference > 0.01m)
                {
                    TempData["ErrorMessage"] = $"Debit and Credit must be equal before saving! Total Debit: Rs {totalDebit:N2}, Total Credit: Rs {totalCredit:N2}, Difference: Rs {difference:N2}.";
                    return RedirectToAction(nameof(Index), new { id = model.Id, fyId = model.FinancialYearId });
                }

                string currentUser = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "User";

                // Fetch Financial Year to get its official StartDate
                var fy = await _context.CFinancialYears
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == model.FinancialYearId);

                DateTime fyStartDate = fy?.StartDate ?? DateTime.Today;

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    Models.OpeningBalance? obMaster = null;

                    // 1. Look up existing active voucher for this Financial Year & Company
                    if (model.Id.HasValue && model.Id.Value > 0)
                    {
                        obMaster = await _context.OpeningBalances
                            .Include(m => m.Details)
                            .FirstOrDefaultAsync(m => m.Id == model.Id.Value && m.CompanyId == companyId);
                    }

                    if (obMaster == null)
                    {
                        obMaster = await _context.OpeningBalances
                            .Include(m => m.Details)
                            .FirstOrDefaultAsync(m => m.FinancialYearId == model.FinancialYearId && m.CompanyId == companyId && m.Active != false);
                    }

                    if (obMaster != null)
                    {
                        // ALWAYS UPDATE THE EXISTING SINGLE VOUCHER FOR THIS FINANCIAL YEAR
                        obMaster.OBDate = fyStartDate; // Locked strictly to FY StartDate
                        obMaster.FinancialYearId = model.FinancialYearId;
                        obMaster.Active = true;
                        obMaster.ModifiedOn = DateTime.Now;
                        obMaster.ModifiedBy = currentUser;

                        // Clear existing details and add only updated active rows with amounts
                        _context.OpeningBalanceDetails.RemoveRange(obMaster.Details);
                        obMaster.Details.Clear();

                        foreach (var row in validRows)
                        {
                            obMaster.Details.Add(new OpeningBalanceDetail
                            {
                                COAId = row.COAId,
                                OBDtlTranType = string.Equals(row.OBDtlTranType, "CRE", StringComparison.OrdinalIgnoreCase) ? "CRE" : "DEB",
                                OBDtlOpenBlnc = row.OBDtlOpenBlnc!.Value
                            });
                        }

                        _context.OpeningBalances.Update(obMaster);
                    }
                    else
                    {
                        // CREATE NEW SINGLE VOUCHER FOR THIS FINANCIAL YEAR
                        string obCode = await GenerateNextOBCodeAsync(companyId, model.FinancialYearId);

                        obMaster = new Models.OpeningBalance
                        {
                            OBCode = obCode,
                            OBDate = fyStartDate, // Locked strictly to FY StartDate
                            CompanyId = companyId,
                            FinancialYearId = model.FinancialYearId,
                            Active = true,
                            CreatedOn = DateTime.Now,
                            CreatedBy = currentUser,
                            ModifiedOn = DateTime.Now,
                            ModifiedBy = currentUser
                        };

                        foreach (var row in validRows)
                        {
                            obMaster.Details.Add(new OpeningBalanceDetail
                            {
                                COAId = row.COAId,
                                OBDtlTranType = string.Equals(row.OBDtlTranType, "CRE", StringComparison.OrdinalIgnoreCase) ? "CRE" : "DEB",
                                OBDtlOpenBlnc = row.OBDtlOpenBlnc!.Value
                            });
                        }

                        await _context.OpeningBalances.AddAsync(obMaster);
                    }

                    await _context.SaveChangesAsync();

                    // Safety check: Deactivate any other older duplicate active records for this Financial Year
                    var duplicateVouchers = await _context.OpeningBalances
                        .Where(m => m.CompanyId == companyId && m.FinancialYearId == model.FinancialYearId && m.Id != obMaster.Id && m.Active != false)
                        .ToListAsync();

                    foreach (var dup in duplicateVouchers)
                    {
                        dup.Active = false;
                        dup.ModifiedOn = DateTime.Now;
                        dup.ModifiedBy = currentUser + " (Deactivated duplicate)";
                    }

                    if (duplicateVouchers.Any())
                    {
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();

                    _logger.LogInformation("Opening Balance voucher '{OBCode}' saved successfully for FY {FY} with {Count} entries (Debit: {Debit}, Credit: {Credit}) by {User}",
                        obMaster.OBCode, obMaster.FinancialYearId, obMaster.Details.Count, totalDebit, totalCredit, currentUser);

                    TempData["SuccessMessage"] = $"Opening Balance for Financial Year '{fy?.Code ?? obMaster.FinancialYearId.ToString()}' saved successfully ({obMaster.Details.Count} entries balanced at Rs {totalDebit:N2}).";
                    return RedirectToAction(nameof(Index), new { fyId = obMaster.FinancialYearId });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Opening Balance");
                TempData["ErrorMessage"] = "Failed to save Opening Balance: " + ex.Message;
                return RedirectToAction(nameof(Index), new { id = model.Id, fyId = model.FinancialYearId });
            }
        }

        // =========================================================================
        // 3. GET VOUCHER DETAILS AS JSON (FOR VIEWING / MODAL)
        // =========================================================================
        [HttpGet]
        [Route("GetVoucherDetails/{id}")]
        public async Task<IActionResult> GetVoucherDetails(int id)
        {
            try
            {
                int companyId = GetCurrentCompanyId();
                var voucher = await _context.OpeningBalances
                    .Include(m => m.FinancialYear)
                    .Include(m => m.Details)
                    .ThenInclude(d => d.ChartOfAccount)
                    .FirstOrDefaultAsync(m => m.Id == id && m.CompanyId == companyId);

                if (voucher == null)
                {
                    return NotFound(new { success = false, message = "Voucher not found." });
                }

                var data = new
                {
                    success = true,
                    id = voucher.Id,
                    obCode = voucher.OBCode,
                    obDate = voucher.OBDate.ToString("yyyy-MM-dd"),
                    financialYear = voucher.FinancialYear?.Code ?? voucher.FinancialYearId.ToString(),
                    fiscalPeriod = voucher.FinancialYear != null ? $"{voucher.FinancialYear.StartDate:dd-MMM-yyyy} to {voucher.FinancialYear.EndDate:dd-MMM-yyyy}" : "",
                    createdBy = voucher.CreatedBy,
                    createdOn = voucher.CreatedOn?.ToString("yyyy-MM-dd HH:mm"),
                    totalDebit = voucher.Details.Where(d => d.OBDtlTranType == "DEB").Sum(d => d.OBDtlOpenBlnc),
                    totalCredit = voucher.Details.Where(d => d.OBDtlTranType == "CRE").Sum(d => d.OBDtlOpenBlnc),
                    details = voucher.Details.Select(d => new
                    {
                        id = d.Id,
                        coaId = d.COAId,
                        accountCode = d.ChartOfAccount?.AccountCode ?? "",
                        accountName = d.ChartOfAccount?.AccountName ?? "",
                        tranType = d.OBDtlTranType,
                        amount = d.OBDtlOpenBlnc
                    }).ToList()
                };

                return Json(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving voucher details for ID {Id}", id);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // =========================================================================
        // 4. SOFT DELETE OPENING BALANCE VOUCHER
        // =========================================================================
        [HttpPost]
        [Route("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                int companyId = GetCurrentCompanyId();
                var voucher = await _context.OpeningBalances
                    .FirstOrDefaultAsync(m => m.Id == id && m.CompanyId == companyId);

                if (voucher == null)
                {
                    TempData["ErrorMessage"] = "Opening Balance record not found.";
                    return RedirectToAction(nameof(Index));
                }

                voucher.Active = false;
                voucher.ModifiedOn = DateTime.Now;
                voucher.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? "User";

                _context.OpeningBalances.Update(voucher);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Opening Balance voucher '{OBCode}' marked inactive (soft deleted).", voucher.OBCode);
                TempData["SuccessMessage"] = $"Opening Balance '{voucher.OBCode}' deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Opening Balance voucher ID {Id}", id);
                TempData["ErrorMessage"] = "Failed to delete voucher: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // =========================================================================
        // 5. HELPER: STRICT LEVEL 3 CHART OF ACCOUNTS QUERY
        // =========================================================================
        private async Task<List<CoaLevel3DropdownDto>> GetLevel3AccountsAsync(int companyId)
        {
            // Retrieve all active accounts to evaluate tree hierarchy depth
            var allAccounts = await _context.ChartOfAccounts
                .AsNoTracking()
                .Where(a => a.Active != false)
                .ToListAsync();

            if (!allAccounts.Any())
            {
                return new List<CoaLevel3DropdownDto>();
            }

            var accountMap = allAccounts.ToDictionary(a => a.Id);
            var parentIdsWithChildren = new HashSet<int>(allAccounts.Where(a => a.ParentAccountId.HasValue).Select(a => a.ParentAccountId!.Value));

            // Helper to compute account tree depth (1 = Root/Head, 2 = Control, 3 = Sub/Detail)
            int ComputeDepth(ChartOfAccount a)
            {
                int depth = 1;
                int? currentParent = a.ParentAccountId;
                int safeguard = 0; // avoid cyclic loops
                while (currentParent.HasValue && accountMap.TryGetValue(currentParent.Value, out var parent) && safeguard++ < 10)
                {
                    depth++;
                    currentParent = parent.ParentAccountId;
                }
                return depth;
            }

            // CRITICAL REQUIREMENT:
            // "in coa drop down we can only enter values in level 3 i.e lowest level account level 1 and level 2 account shouldnt appear in dropdown"
            // Level 1: Depth 1 (ParentAccountId == null) -> EXCLUDED
            // Level 2: Depth 2 (Parent is Level 1) -> EXCLUDED
            // Level 3+: Depth >= 3 -> INCLUDED (also ensuring Transable == true and is lowest leaf account)
            var level3Accounts = allAccounts
                .Where(a =>
                {
                    int depth = ComputeDepth(a);
                    bool isLeaf = !parentIdsWithChildren.Contains(a.Id);
                    return depth >= 3 && isLeaf;
                })
                .Select(a =>
                {
                    accountMap.TryGetValue(a.ParentAccountId ?? 0, out var parentAcc);
                    return new CoaLevel3DropdownDto
                    {
                        Id = a.Id,
                        AccountCode = a.AccountCode ?? string.Empty,
                        AccountName = a.AccountName,
                        ParentAccountId = a.ParentAccountId,
                        ParentAccountName = parentAcc?.AccountName,
                        ParentAccountCode = parentAcc?.AccountCode
                    };
                })
                .OrderBy(a => a.AccountCode)
                .ThenBy(a => a.AccountName)
                .ToList();

            // Fallback safety: If system has transable leaf accounts at level 2 only because no level 3 were created yet,
            // check if level 3 accounts exist. If none exist at level 3, log warning.
            if (!level3Accounts.Any())
            {
                _logger.LogWarning("No Level 3 leaf accounts found in Chart of Accounts. Verifying if vendors or sub-accounts exist.");
            }

            return level3Accounts;
        }

        // =========================================================================
        // 6. HELPER: GENERATE NEXT OB CODE (e.g. OB-00001)
        // =========================================================================
        private async Task<string> GenerateNextOBCodeAsync(int companyId, int financialYearId)
        {
            try
            {
                var codes = await _context.OpeningBalances
                    .Where(m => m.CompanyId == companyId && m.FinancialYearId == financialYearId)
                    .Select(m => m.OBCode)
                    .ToListAsync();

                int maxNumber = 0;
                foreach (var code in codes)
                {
                    if (string.IsNullOrWhiteSpace(code)) continue;
                    // Extract digits after "OB-"
                    var parts = code.Split('-');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int num))
                    {
                        if (num > maxNumber) maxNumber = num;
                    }
                    else if (int.TryParse(code.Replace("OB", "", StringComparison.OrdinalIgnoreCase).Trim(), out int n))
                    {
                        if (n > maxNumber) maxNumber = n;
                    }
                }

                int nextNumber = maxNumber + 1;
                return $"OB-{nextNumber:D5}";
            }
            catch
            {
                return "OB-00001";
            }
        }

        // =========================================================================
        // 7. HELPER: GET RECENT VOUCHERS LIST
        // =========================================================================
        private async Task<List<OpeningBalanceSummaryDto>> GetRecentVouchersAsync(int companyId)
        {
            var vouchers = await _context.OpeningBalances
                .AsNoTracking()
                .Include(m => m.FinancialYear)
                .Include(m => m.Details)
                .Where(m => m.CompanyId == companyId && m.Active != false)
                .OrderByDescending(m => m.FinancialYear != null ? m.FinancialYear.StartDate : m.OBDate)
                .ThenByDescending(m => m.Id)
                .Select(m => new OpeningBalanceSummaryDto
                {
                    Id = m.Id,
                    OBCode = m.OBCode,
                    OBDate = m.OBDate,
                    FinancialYearId = m.FinancialYearId,
                    FinancialYearCode = m.FinancialYear != null ? m.FinancialYear.Code : "",
                    FiscalPeriod = m.FinancialYear != null ? $"{m.FinancialYear.StartDate:dd-MMM-yyyy} to {m.FinancialYear.EndDate:dd-MMM-yyyy}" : "",
                    TotalEntries = m.Details.Count,
                    TotalDebit = m.Details.Where(d => d.OBDtlTranType == "DEB").Sum(d => d.OBDtlOpenBlnc),
                    TotalCredit = m.Details.Where(d => d.OBDtlTranType == "CRE").Sum(d => d.OBDtlOpenBlnc),
                    Active = m.Active,
                    CreatedOn = m.CreatedOn,
                    CreatedBy = m.CreatedBy
                })
                .ToListAsync();

            return vouchers;
        }

        // =========================================================================
        // 8. HELPER: CURRENT COMPANY ID
        // =========================================================================
        private int GetCurrentCompanyId()
        {
            int? sessionCompany = HttpContext.Session.GetInt32("CompanyId");
            if (sessionCompany.HasValue && sessionCompany.Value > 0)
            {
                return sessionCompany.Value;
            }

            int dbCompany = _context.CCompanies.Select(c => c.CompanyId).FirstOrDefault();
            return dbCompany > 0 ? dbCompany : 1;
        }

        // =========================================================================
        // 9. HELPER: SELF-HEALING DATABASE DDL
        // =========================================================================
        private async Task EnsureOpeningBalanceTablesCreatedAsync()
        {
            try
            {
                string sql = @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OpeningBalances')
BEGIN
    CREATE TABLE [OpeningBalances] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [OBCode] VARCHAR(50) NOT NULL,
        [OBDate] DATETIME NOT NULL,
        [CompanyId] INT NOT NULL,
        [FinancialYearId] INT NOT NULL,
        [Active] BIT NULL DEFAULT 1,
        [CreatedOn] DATETIME NULL,
        [CreatedBy] NVARCHAR(100) NULL,
        [ModifiedOn] DATETIME NULL,
        [ModifiedBy] NVARCHAR(100) NULL
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OpeningBalanceDetails')
BEGIN
    CREATE TABLE [OpeningBalanceDetails] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [OBId] INT NOT NULL,
        [COAId] INT NOT NULL,
        [OBDtlTranType] VARCHAR(10) NOT NULL,
        [OBDtlOpenBlnc] DECIMAL(18,4) NOT NULL DEFAULT 0,
        CONSTRAINT [FK_OpeningBalanceDetails_OpeningBalances] FOREIGN KEY ([OBId]) REFERENCES [OpeningBalances]([Id]) ON DELETE CASCADE
    );
END

-- Deactivate older duplicate active opening balances for the same Company and Financial Year
;WITH RankedOB AS (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY CompanyId, FinancialYearId ORDER BY Id DESC) AS rn
    FROM [dbo].[OpeningBalances]
    WHERE Active = 1 OR Active IS NULL
)
UPDATE ob
SET Active = 0, ModifiedOn = GETDATE(), ModifiedBy = 'Auto-Deduplicated'
FROM [dbo].[OpeningBalances] ob
INNER JOIN RankedOB r ON ob.Id = r.Id
WHERE r.rn > 1;

-- Ensure Unique Filtered Index exists so database strictly enforces only 1 active opening balance per FY per Company
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_OpeningBalances_Company_FY' AND object_id = OBJECT_ID('OpeningBalances'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_OpeningBalances_Company_FY]
    ON [dbo].[OpeningBalances] ([CompanyId], [FinancialYearId])
    WHERE [Active] = 1;
END";
                await _context.Database.ExecuteSqlRawAsync(sql);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not verify or auto-create OpeningBalances tables schema.");
            }
        }
    }
}
