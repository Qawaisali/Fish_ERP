using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ERP_Core.Attributes;
using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using ERP_Core.Repositories.IRepositories;
using ERP_Core.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP_Core.Controllers.SupplierDistribution
{
    [Authorize]
    [Route("SupplierDistribution")]
    [Route("supplier-distribution")]
    public class SupplierDistributionController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IVendorService _vendorService;
        private readonly ILogger<SupplierDistributionController> _logger;
        private static bool _schemaEnsured = false;

        public SupplierDistributionController(
            ERPDbContext context,
            IUnitOfWork unitOfWork,
            IVendorService vendorService,
            ILogger<SupplierDistributionController> logger)
        {
            _context = context;
            _unitOfWork = unitOfWork;
            _vendorService = vendorService;
            _logger = logger;
        }

        private async Task EnsureSchemaAsync()
        {
            if (_schemaEnsured) return;

            try
            {
                string sql = @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SupplierDistributionMasters')
BEGIN
    CREATE TABLE [dbo].[SupplierDistributionMasters] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [DistributionNo] VARCHAR(50) NOT NULL,
        [DistributionDate] DATETIME NOT NULL,
        [ParentSupplierId] INT NOT NULL,
        [TotalAmount] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [Remarks] NVARCHAR(500) NULL,
        [VoucherId] INT NULL,
        [VoucherNo] VARCHAR(50) NULL,
        [CompanyId] INT NULL,
        [Active] BIT NULL DEFAULT 1,
        [CreatedOn] DATETIME NULL,
        [CreatedBy] NVARCHAR(100) NULL,
        [ModifiedOn] DATETIME NULL,
        [ModifiedBy] NVARCHAR(100) NULL,
        CONSTRAINT [FK_SupplierDistributionMasters_Vendors] FOREIGN KEY ([ParentSupplierId]) REFERENCES [Vendors]([Id])
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SupplierDistributionDetails')
BEGIN
    CREATE TABLE [dbo].[SupplierDistributionDetails] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [SupplierDistributionMasterId] INT NOT NULL,
        [ChildSupplierId] INT NOT NULL,
        [Amount] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [Remarks] NVARCHAR(300) NULL,
        [Active] BIT NULL DEFAULT 1,
        [CreatedOn] DATETIME NULL,
        [CreatedBy] NVARCHAR(100) NULL,
        [ModifiedOn] DATETIME NULL,
        [ModifiedBy] NVARCHAR(100) NULL,
        CONSTRAINT [FK_SupplierDistributionDetails_SupplierDistributionMasters] FOREIGN KEY ([SupplierDistributionMasterId]) REFERENCES [SupplierDistributionMasters]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SupplierDistributionDetails_Vendors] FOREIGN KEY ([ChildSupplierId]) REFERENCES [Vendors]([Id])
    );
END";
                await _context.Database.ExecuteSqlRawAsync(sql);
                _schemaEnsured = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not run automatic schema creation for SupplierDistribution tables.");
            }
        }

        private int GetCurrentCompanyId() => HttpContext.Session.GetInt32("CompanyId") ?? 1;

        private async Task<int> GetEffectiveFinancialYearIdAsync(int companyId, DateTime txDate)
        {
            try
            {
                int? sessionFy = HttpContext.Session.GetInt32("FinancialYearId");
                if (sessionFy.HasValue && sessionFy.Value > 0)
                    return sessionFy.Value;

                var claim = User.Claims.FirstOrDefault(c => c.Type == "FinancialYearId");
                if (claim != null && int.TryParse(claim.Value, out int fyId) && fyId > 0)
                    return fyId;

                var dateMatch = await _context.CFinancialYears
                    .AsNoTracking()
                    .Where(f => (f.CompanyID == companyId || f.CompanyID == 0) && f.StartDate <= txDate && f.EndDate >= txDate && (f.Active == null || f.Active == true))
                    .Select(f => f.Id)
                    .FirstOrDefaultAsync();

                if (dateMatch > 0)
                    return dateMatch;

                var defaultFy = await _context.CFinancialYears
                    .AsNoTracking()
                    .OrderByDescending(f => f.CurrentYear)
                    .ThenByDescending(f => f.StartDate)
                    .Select(f => f.Id)
                    .FirstOrDefaultAsync();

                return defaultFy > 0 ? defaultFy : 1;
            }
            catch
            {
                return 1;
            }
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out int uid) ? uid : (int?)null;
        }

        private async Task<string> GenerateNextDistributionNoAsync()
        {
            try
            {
                int currentYear = DateTime.Today.Year;
                string prefix = $"SD-{currentYear}-";

                var last = await _context.SupplierDistributionMasters
                    .AsNoTracking()
                    .Where(m => m.DistributionNo != null && m.DistributionNo.StartsWith(prefix))
                    .OrderByDescending(m => m.Id)
                    .Select(m => m.DistributionNo)
                    .FirstOrDefaultAsync();

                int nextSeq = 1;
                if (!string.IsNullOrEmpty(last))
                {
                    string numPart = last.Substring(prefix.Length);
                    if (int.TryParse(numPart, out int parsed))
                    {
                        nextSeq = parsed + 1;
                    }
                }

                return $"{prefix}{nextSeq:D4}";
            }
            catch
            {
                return $"SD-{DateTime.Today:yyyyMMdd}-{new Random().Next(100, 999)}";
            }
        }

        private async Task<decimal> CalculateSupplierPayableBalanceAsync(int vendorId, int companyId)
        {
            try
            {
                var vendor = await _context.Vendors
                    .AsNoTracking()
                    .Include(v => v.ChartOfAccount)
                    .FirstOrDefaultAsync(v => v.Id == vendorId && v.CompanyId == companyId);

                if (vendor == null) return 0;

                decimal balance = vendor.OpeningBalance;

                if (vendor.ChartOfAccountId.HasValue && vendor.ChartOfAccountId.Value > 0)
                {
                    var coaId = vendor.ChartOfAccountId.Value;

                    var obSum = await _context.OpeningBalanceDetails
                        .AsNoTracking()
                        .Include(d => d.OpeningBalance)
                        .Where(d => d.COAId == coaId && d.OpeningBalance != null && (d.OpeningBalance.CompanyId == companyId || d.OpeningBalance.CompanyId == 0) && (d.OpeningBalance.Active == null || d.OpeningBalance.Active == true))
                        .SumAsync(d => string.Equals(d.OBDtlTranType, "CRE", StringComparison.OrdinalIgnoreCase) ? d.OBDtlOpenBlnc : -d.OBDtlOpenBlnc);

                    balance += obSum;

                    var txSummary = await _context.AccountTransactions
                        .AsNoTracking()
                        .Where(t => t.ChartofAccountId == coaId && (t.CompanyId == null || t.CompanyId == companyId))
                        .GroupBy(t => 1)
                        .Select(g => new
                        {
                            TotalCredit = g.Sum(t => t.Credit),
                            TotalDebit = g.Sum(t => t.Debit)
                        })
                        .FirstOrDefaultAsync();

                    if (txSummary != null)
                    {
                        balance += (txSummary.TotalCredit - txSummary.TotalDebit);
                    }
                }

                return balance;
            }
            catch
            {
                return 0;
            }
        }

        // =========================================================================
        // 1. INDEX: MAIN FORM & TRANSFER HISTORY
        // =========================================================================
        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, int? parentSupplierId)
        {
            ViewData["Title"] = "Supplier Child Distribution";
            ViewData["SubTitle"] = "Parent supplier (kashti/nakhuda) se amount less karke child suppliers ke accounts me direct distribute karein.";
            ViewData["ActivePage"] = "SupplierDistribution";

            await EnsureSchemaAsync();
            int companyId = GetCurrentCompanyId();

            var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var parsedFrom = fromDate ?? startOfMonth;
            var parsedTo = toDate ?? DateTime.Today;

            var model = new SupplierDistributionViewModel
            {
                DistributionNo = await GenerateNextDistributionNoAsync(),
                Date = DateTime.Today.ToString("yyyy-MM-dd"),
                FromDate = parsedFrom.ToString("yyyy-MM-dd"),
                ToDate = parsedTo.ToString("yyyy-MM-dd"),
                FilterParentId = parentSupplierId
            };

            // 1. Fetch All Active Suppliers
            var allSuppliers = await _context.Vendors
                .AsNoTracking()
                .Include(v => v.ChartOfAccount)
                .Include(v => v.ParentSupplier)
                .Where(v => v.VendorType == VendorType.Supplier && v.CompanyId == companyId && (v.Active == null || v.Active == true))
                .OrderBy(v => v.Name)
                .ToListAsync();

            // Distinct Parent Suppliers (those that are either assigned as parent or have no parent themselves)
            var parentIdsWithChildren = allSuppliers
                .Where(v => v.ParentSupplierId.HasValue && v.ParentSupplierId.Value > 0)
                .Select(v => v.ParentSupplierId!.Value)
                .ToHashSet();

            model.ParentSuppliersList = allSuppliers
                .Where(v => !v.ParentSupplierId.HasValue || parentIdsWithChildren.Contains(v.Id))
                .Select(v => new PartyDropdownDto
                {
                    Id = v.Id,
                    Name = v.Name,
                    Code = v.ChartOfAccount != null ? v.ChartOfAccount.AccountCode : (v.AccountNo ?? ""),
                    Phone = v.Phone,
                    CityOrVillage = v.City
                })
                .ToList();

            // All child suppliers for selectable row options
            model.AllChildSuppliers = allSuppliers
                .Select(v => new ChildSupplierOptionDto
                {
                    Id = v.Id,
                    Name = v.Name,
                    Code = v.ChartOfAccount != null ? v.ChartOfAccount.AccountCode : (v.AccountNo ?? ""),
                    Phone = v.Phone,
                    City = v.City,
                    ParentSupplierId = v.ParentSupplierId,
                    ParentSupplierName = v.ParentSupplier?.Name,
                    CurrentBalance = v.OpeningBalance
                })
                .ToList();

            // 2. Fetch Recent Distributions
            var query = _context.SupplierDistributionMasters
                .Include(m => m.ParentSupplier)
                    .ThenInclude(p => p!.ChartOfAccount)
                .Include(m => m.Details)
                    .ThenInclude(d => d.ChildSupplier)
                        .ThenInclude(c => c!.ChartOfAccount)
                .AsNoTracking()
                .Where(m => (m.CompanyId == null || m.CompanyId == companyId) &&
                            m.DistributionDate >= parsedFrom.Date &&
                            m.DistributionDate <= parsedTo.Date.AddDays(1).AddTicks(-1));

            if (parentSupplierId.HasValue && parentSupplierId.Value > 0)
            {
                query = query.Where(m => m.ParentSupplierId == parentSupplierId.Value);
            }

            var recentRecords = await query
                .OrderByDescending(m => m.DistributionDate)
                .ThenByDescending(m => m.Id)
                .Take(100)
                .ToListAsync();

            model.RecentDistributions = recentRecords.Select(m => new SupplierDistributionItemDto
            {
                Id = m.Id,
                DistributionNo = m.DistributionNo,
                Date = m.DistributionDate,
                ParentSupplierId = m.ParentSupplierId,
                ParentSupplierName = m.ParentSupplier?.Name ?? "Unknown Parent",
                ParentAccountCode = m.ParentSupplier?.ChartOfAccount?.AccountCode ?? m.ParentSupplier?.AccountNo,
                TotalAmount = m.TotalAmount,
                Remarks = m.Remarks,
                VoucherId = m.VoucherId,
                VoucherNo = m.VoucherNo,
                ChildrenCount = m.Details.Count,
                Details = m.Details.Select(d => new SupplierDistributionDetailItemDto
                {
                    Id = d.Id,
                    ChildSupplierId = d.ChildSupplierId,
                    ChildSupplierName = d.ChildSupplier?.Name ?? "Sub-Supplier",
                    AccountCode = d.ChildSupplier?.ChartOfAccount?.AccountCode ?? d.ChildSupplier?.AccountNo,
                    Phone = d.ChildSupplier?.Phone,
                    Amount = d.Amount,
                    Remarks = d.Remarks
                }).ToList()
            }).ToList();

            model.TotalHistoricalAmount = model.RecentDistributions.Sum(r => r.TotalAmount);
            model.TotalHistoricalCount = model.RecentDistributions.Count;

            return View(model);
        }

        // =========================================================================
        // 2. AJAX: GET PARENT SUPPLIER DETAILS & LINKED CHILD SUPPLIERS
        // =========================================================================
        [HttpGet]
        [Route("GetParentSupplierDetails")]
        public async Task<IActionResult> GetParentSupplierDetails(int parentSupplierId)
        {
            if (parentSupplierId <= 0)
            {
                return Json(new { success = false, message = "Invalid Supplier ID." });
            }

            try
            {
                int companyId = GetCurrentCompanyId();
                var parent = await _context.Vendors
                    .AsNoTracking()
                    .Include(v => v.ChartOfAccount)
                    .FirstOrDefaultAsync(v => v.Id == parentSupplierId && v.CompanyId == companyId);

                if (parent == null)
                {
                    return Json(new { success = false, message = "Supplier not found." });
                }

                decimal parentBalance = await CalculateSupplierPayableBalanceAsync(parent.Id, companyId);

                // Fetch linked child suppliers
                var children = await _context.Vendors
                    .AsNoTracking()
                    .Include(v => v.ChartOfAccount)
                    .Where(v => v.VendorType == VendorType.Supplier &&
                                v.CompanyId == companyId &&
                                v.ParentSupplierId == parentSupplierId &&
                                (v.Active == null || v.Active == true))
                    .OrderBy(v => v.Name)
                    .ToListAsync();

                var childDtos = new List<ChildSupplierOptionDto>();
                foreach (var c in children)
                {
                    decimal cBal = await CalculateSupplierPayableBalanceAsync(c.Id, companyId);
                    childDtos.Add(new ChildSupplierOptionDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Code = c.ChartOfAccount?.AccountCode ?? c.AccountNo ?? "",
                        Phone = c.Phone ?? "-",
                        City = c.City ?? "-",
                        ParentSupplierId = parentSupplierId,
                        ParentSupplierName = parent.Name,
                        CurrentBalance = cBal
                    });
                }

                return Json(new
                {
                    success = true,
                    data = new ParentSupplierDetailsDto
                    {
                        Id = parent.Id,
                        Name = parent.Name,
                        Code = parent.ChartOfAccount?.AccountCode ?? parent.AccountNo ?? "",
                        Phone = parent.Phone ?? "-",
                        City = parent.City ?? "-",
                        CurrentBalance = parentBalance,
                        LinkedChildren = childDtos
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching parent supplier details ID {ParentId}", parentSupplierId);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================================================================
        // 3. AJAX: GET DISTRIBUTION DETAILS FOR VIEW / PRINT / EDIT
        // =========================================================================
        [HttpGet]
        [Route("GetDistributionDetails")]
        public async Task<IActionResult> GetDistributionDetails(int id)
        {
            if (id <= 0) return Json(new { success = false, message = "Invalid ID." });

            try
            {
                int companyId = GetCurrentCompanyId();
                var dist = await _context.SupplierDistributionMasters
                    .Include(m => m.ParentSupplier)
                        .ThenInclude(p => p!.ChartOfAccount)
                    .Include(m => m.Details)
                        .ThenInclude(d => d.ChildSupplier)
                            .ThenInclude(c => c!.ChartOfAccount)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == id && (m.CompanyId == null || m.CompanyId == companyId));

                if (dist == null)
                {
                    return Json(new { success = false, message = "Record not found." });
                }

                decimal parentBalance = await CalculateSupplierPayableBalanceAsync(dist.ParentSupplierId, companyId);

                var dto = new SupplierDistributionItemDto
                {
                    Id = dist.Id,
                    DistributionNo = dist.DistributionNo,
                    Date = dist.DistributionDate,
                    ParentSupplierId = dist.ParentSupplierId,
                    ParentSupplierName = dist.ParentSupplier?.Name ?? "Parent Supplier",
                    ParentAccountCode = dist.ParentSupplier?.ChartOfAccount?.AccountCode ?? dist.ParentSupplier?.AccountNo,
                    TotalAmount = dist.TotalAmount,
                    Remarks = dist.Remarks,
                    VoucherId = dist.VoucherId,
                    VoucherNo = dist.VoucherNo,
                    ChildrenCount = dist.Details.Count,
                    Details = dist.Details.Select(d => new SupplierDistributionDetailItemDto
                    {
                        Id = d.Id,
                        ChildSupplierId = d.ChildSupplierId,
                        ChildSupplierName = d.ChildSupplier?.Name ?? "Child Supplier",
                        AccountCode = d.ChildSupplier?.ChartOfAccount?.AccountCode ?? d.ChildSupplier?.AccountNo,
                        Phone = d.ChildSupplier?.Phone ?? "-",
                        Amount = d.Amount,
                        Remarks = d.Remarks
                    }).ToList()
                };

                return Json(new { success = true, data = dto, parentCurrentBalance = parentBalance });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================================================================
        // 4. POST: SAVE SUPPLIER DISTRIBUTION & AUTOMATIC JV POSTING
        // =========================================================================
        [HttpPost]
        [Route("SaveDistribution")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDistribution(SupplierDistributionViewModel model)
        {
            await EnsureSchemaAsync();

            if (!model.SelectedParentSupplierId.HasValue || model.SelectedParentSupplierId.Value <= 0)
            {
                TempData["ErrorMessage"] = "Please select a Parent Supplier.";
                return RedirectToAction(nameof(Index));
            }

            var validItems = (model.DistributionItems ?? new List<SupplierDistributionDetailItemDto>())
                .Where(i => i.ChildSupplierId > 0 && i.Amount > 0)
                .ToList();

            if (!validItems.Any())
            {
                TempData["ErrorMessage"] = "Please add at least one Child Supplier with an amount greater than zero.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                int companyId = GetCurrentCompanyId();
                DateTime txDate = DateTime.TryParse(model.Date, out var pDate) ? pDate : DateTime.Today;
                int fyId = await GetEffectiveFinancialYearIdAsync(companyId, txDate);
                int? userId = GetCurrentUserId();
                string currentUser = User.Identity?.Name ?? "System";

                decimal totalTransfer = validItems.Sum(i => i.Amount);
                if (string.IsNullOrWhiteSpace(model.DistributionNo))
                {
                    model.DistributionNo = await GenerateNextDistributionNoAsync();
                }

                // 1. Resolve Parent Supplier & Ensure COA
                var parentSupplier = await _context.Vendors
                    .Include(v => v.ChartOfAccount)
                    .FirstOrDefaultAsync(v => v.Id == model.SelectedParentSupplierId.Value && v.CompanyId == companyId);

                if (parentSupplier == null)
                {
                    TempData["ErrorMessage"] = "Selected Parent Supplier does not exist.";
                    return RedirectToAction(nameof(Index));
                }

                if (!parentSupplier.ChartOfAccountId.HasValue || parentSupplier.ChartOfAccountId.Value <= 0)
                {
                    await _vendorService.UpdateVendorAsync(parentSupplier, currentUser);
                }

                var parentCoa = await _context.ChartOfAccounts.FindAsync(parentSupplier.ChartOfAccountId);
                if (parentCoa == null)
                {
                    TempData["ErrorMessage"] = $"Could not resolve Chart of Accounts for Parent Supplier '{parentSupplier.Name}'.";
                    return RedirectToAction(nameof(Index));
                }

                // 2. Resolve Child Suppliers & Ensure COA
                var childCoaMap = new Dictionary<int, ChartOfAccount>();
                foreach (var item in validItems)
                {
                    var childVendor = await _context.Vendors
                        .Include(v => v.ChartOfAccount)
                        .FirstOrDefaultAsync(v => v.Id == item.ChildSupplierId && v.CompanyId == companyId);

                    if (childVendor == null)
                    {
                        TempData["ErrorMessage"] = $"Child Supplier with ID {item.ChildSupplierId} not found.";
                        return RedirectToAction(nameof(Index));
                    }

                    if (!childVendor.ChartOfAccountId.HasValue || childVendor.ChartOfAccountId.Value <= 0)
                    {
                        await _vendorService.UpdateVendorAsync(childVendor, currentUser);
                    }

                    var cCoa = await _context.ChartOfAccounts.FindAsync(childVendor.ChartOfAccountId);
                    if (cCoa == null)
                    {
                        TempData["ErrorMessage"] = $"Could not resolve Chart of Accounts for Child Supplier '{childVendor.Name}'.";
                        return RedirectToAction(nameof(Index));
                    }

                    childCoaMap[item.ChildSupplierId] = cCoa;
                }

                // 3. Save or Update SupplierDistributionMaster
                SupplierDistributionMaster? distMaster = null;
                if (model.Id > 0)
                {
                    distMaster = await _context.SupplierDistributionMasters
                        .Include(m => m.Details)
                        .FirstOrDefaultAsync(m => m.Id == model.Id && (m.CompanyId == null || m.CompanyId == companyId));

                    if (distMaster == null)
                    {
                        TempData["ErrorMessage"] = "Distribution record not found for update.";
                        return RedirectToAction(nameof(Index));
                    }

                    distMaster.DistributionDate = txDate;
                    distMaster.ParentSupplierId = parentSupplier.Id;
                    distMaster.TotalAmount = totalTransfer;
                    distMaster.Remarks = model.Remarks;
                    distMaster.ModifiedOn = DateTime.Now;
                    distMaster.ModifiedBy = currentUser;

                    _context.SupplierDistributionDetails.RemoveRange(distMaster.Details);
                    distMaster.Details.Clear();

                    foreach (var item in validItems)
                    {
                        distMaster.Details.Add(new SupplierDistributionDetail
                        {
                            ChildSupplierId = item.ChildSupplierId,
                            Amount = item.Amount,
                            Remarks = item.Remarks,
                            CreatedOn = DateTime.Now,
                            CreatedBy = currentUser,
                            Active = true
                        });
                    }

                    _context.SupplierDistributionMasters.Update(distMaster);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    distMaster = new SupplierDistributionMaster
                    {
                        DistributionNo = model.DistributionNo,
                        DistributionDate = txDate,
                        ParentSupplierId = parentSupplier.Id,
                        TotalAmount = totalTransfer,
                        Remarks = model.Remarks,
                        CompanyId = companyId,
                        CreatedOn = DateTime.Now,
                        CreatedBy = currentUser,
                        Active = true
                    };

                    foreach (var item in validItems)
                    {
                        distMaster.Details.Add(new SupplierDistributionDetail
                        {
                            ChildSupplierId = item.ChildSupplierId,
                            Amount = item.Amount,
                            Remarks = item.Remarks,
                            CreatedOn = DateTime.Now,
                            CreatedBy = currentUser,
                            Active = true
                        });
                    }

                    await _context.SupplierDistributionMasters.AddAsync(distMaster);
                    await _context.SaveChangesAsync();
                }

                // 4. CREATE / UPDATE BALANCED JOURNAL VOUCHER (JV)
                Voucher? voucher = null;
                if (distMaster.VoucherId.HasValue && distMaster.VoucherId.Value > 0)
                {
                    voucher = await _context.Vouchers
                        .Include(v => v.AccountTransactions)
                        .FirstOrDefaultAsync(v => v.VoucherId == distMaster.VoucherId.Value);
                }

                string childNamesSummary = string.Join(", ", validItems.Select(i => $"{childCoaMap[i.ChildSupplierId].AccountName}: Rs {i.Amount:N0}"));
                string generalNarration = $"Supplier Distribution #{distMaster.DistributionNo} - Less from Parent ({parentSupplier.Name}) to [{childNamesSummary}]";

                if (voucher == null)
                {
                    string voucherNo = await _unitOfWork.GenerateVoucherNoAsync("JV", fyId, companyId);
                    voucher = new Voucher
                    {
                        VoucherNo = voucherNo,
                        VoucherDate = txDate,
                        VoucherType = "JV",
                        Amount = totalTransfer,
                        VendorId = parentSupplier.Id,
                        InvoiceId = distMaster.Id,
                        InvoiceNo = distMaster.DistributionNo,
                        Narration = generalNarration,
                        CompanyId = companyId,
                        FinancialYearID = fyId,
                        UserId = userId,
                        Username = currentUser,
                        ApprovalStatus = "Approved",
                        DeleteStatus = 0,
                        CreatedBy = currentUser,
                        CreatedOn = DateTime.Now
                    };

                    await _context.Vouchers.AddAsync(voucher);
                    await _context.SaveChangesAsync(); // Generates VoucherId

                    distMaster.VoucherId = voucher.VoucherId;
                    distMaster.VoucherNo = voucher.VoucherNo;
                    _context.SupplierDistributionMasters.Update(distMaster);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    voucher.VoucherDate = txDate;
                    voucher.Amount = totalTransfer;
                    voucher.VendorId = parentSupplier.Id;
                    voucher.InvoiceId = distMaster.Id;
                    voucher.InvoiceNo = distMaster.DistributionNo;
                    voucher.Narration = generalNarration;
                    voucher.ModifiedBy = currentUser;
                    voucher.ModifiedOn = DateTime.Now;

                    if (voucher.AccountTransactions != null && voucher.AccountTransactions.Any())
                    {
                        _context.AccountTransactions.RemoveRange(voucher.AccountTransactions);
                    }
                    _context.Vouchers.Update(voucher);
                    await _context.SaveChangesAsync();
                }

                // 5. POST ACCOUNT TRANSACTIONS
                var transactions = new List<AccountTransaction>();

                // A. DEBIT ENTRY: PARENT SUPPLIER (LESSENING LIABILITY/PAYABLE)
                transactions.Add(new AccountTransaction
                {
                    VoucherId = voucher.VoucherId,
                    VoucherNo = voucher.VoucherNo,
                    ChartofAccountId = parentCoa.Id,
                    ChartofAccountName = parentCoa.AccountName,
                    Code = parentCoa.AccountCode ?? "",
                    AccountName = parentCoa.AccountName,
                    Debit = totalTransfer,
                    Credit = 0,
                    Amount = totalTransfer,
                    InvoiceNo = distMaster.DistributionNo,
                    Narration = $"Distribution #{distMaster.DistributionNo}: Transferred to Sub-Suppliers ({childNamesSummary})",
                    EntryTypeName = "Journal Voucher",
                    Type = "Supplier Distribution",
                    ExtraDate = txDate,
                    CompanyId = companyId,
                    UserId = userId
                });

                // B. CREDIT ENTRIES: EACH CHILD SUPPLIER (INCREASING CHILD LIABILITY/PAYABLE)
                foreach (var item in validItems)
                {
                    var cCoa = childCoaMap[item.ChildSupplierId];
                    string itemRemark = !string.IsNullOrWhiteSpace(item.Remarks) ? $" - {item.Remarks}" : "";
                    string itemNarration = $"Distribution #{distMaster.DistributionNo}: Received from Parent Supplier ({parentSupplier.Name}){itemRemark}";

                    transactions.Add(new AccountTransaction
                    {
                        VoucherId = voucher.VoucherId,
                        VoucherNo = voucher.VoucherNo,
                        ChartofAccountId = cCoa.Id,
                        ChartofAccountName = cCoa.AccountName,
                        Code = cCoa.AccountCode ?? "",
                        AccountName = cCoa.AccountName,
                        Debit = 0,
                        Credit = item.Amount,
                        Amount = item.Amount,
                        InvoiceNo = distMaster.DistributionNo,
                        Narration = itemNarration,
                        EntryTypeName = "Journal Voucher",
                        Type = "Supplier Distribution",
                        ExtraDate = txDate,
                        CompanyId = companyId,
                        UserId = userId
                    });
                }

                await _context.AccountTransactions.AddRangeAsync(transactions);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Supplier Distribution #{DistNo} saved and JV #{VoucherNo} posted for Rs {Amount}",
                    distMaster.DistributionNo, voucher.VoucherNo, totalTransfer);

                TempData["SuccessMessage"] = $"Supplier Distribution '{distMaster.DistributionNo}' saved successfully! Rs {totalTransfer:N0} deducted from '{parentSupplier.Name}' and credited to {validItems.Count} child supplier(s) (Voucher: {voucher.VoucherNo}).";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving supplier distribution");
                TempData["ErrorMessage"] = "Failed to save distribution: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 5. POST: DELETE SUPPLIER DISTRIBUTION & CANCEL JOURNAL VOUCHER
        // =========================================================================
        [HttpPost]
        [Route("DeleteDistribution")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDistribution(int id)
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid Distribution ID.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                int companyId = GetCurrentCompanyId();
                var dist = await _context.SupplierDistributionMasters
                    .Include(m => m.Details)
                    .Include(m => m.ParentSupplier)
                    .FirstOrDefaultAsync(m => m.Id == id && (m.CompanyId == null || m.CompanyId == companyId));

                if (dist == null)
                {
                    TempData["ErrorMessage"] = "Distribution record not found.";
                    return RedirectToAction(nameof(Index));
                }

                string distNo = dist.DistributionNo;

                // Delete or reverse associated voucher
                if (dist.VoucherId.HasValue && dist.VoucherId.Value > 0)
                {
                    var voucher = await _context.Vouchers
                        .Include(v => v.AccountTransactions)
                        .FirstOrDefaultAsync(v => v.VoucherId == dist.VoucherId.Value);

                    if (voucher != null)
                    {
                        if (voucher.AccountTransactions != null && voucher.AccountTransactions.Any())
                        {
                            _context.AccountTransactions.RemoveRange(voucher.AccountTransactions);
                        }
                        _context.Vouchers.Remove(voucher);
                    }
                }

                _context.SupplierDistributionMasters.Remove(dist);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Supplier Distribution #{DistNo} deleted and accounting reversed", distNo);
                TempData["SuccessMessage"] = $"Supplier Distribution '{distNo}' and its accounting entries were deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting supplier distribution ID {Id}", id);
                TempData["ErrorMessage"] = "Failed to delete distribution: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
