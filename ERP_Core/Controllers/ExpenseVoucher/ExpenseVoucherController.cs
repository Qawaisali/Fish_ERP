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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP_Core.Controllers.ExpenseVoucher
{
    [Authorize]
    [FormAccessAuthorize("Expense Voucher")]
    [Route("ExpenseVoucher")]
    public class ExpenseVoucherController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ExpenseVoucherController> _logger;

        public ExpenseVoucherController(
            ERPDbContext context,
            IUnitOfWork unitOfWork,
            ILogger<ExpenseVoucherController> logger)
        {
            _context = context;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(
            string? fromDate,
            string? toDate,
            int? expenseAccountId,
            string? tab = "entry")
        {
            ViewData["Title"] = "Expense Voucher";
            ViewData["SubTitle"] = "Rozana ke aam kharche darj karein — bill se alag.";

            int companyId = GetCurrentCompanyId();
            int fyId = GetCurrentFinancialYearId();

            // 1. Ensure default BankMaster records exist if empty
            await EnsureDefaultBankMastersAsync(companyId);

            // 2. Fetch Bank Masters
            var bankMasters = await _context.BankMasters
                .AsNoTracking()
                .Include(b => b.ChartOfAccount)
                .Where(b => b.Active != false && (b.CompanyId == null || b.CompanyId == companyId))
                .OrderBy(b => b.IsCash ? 0 : 1)
                .ThenBy(b => b.BankName)
                .Select(b => new BankMasterDropdownDto
                {
                    Id = b.Id,
                    BankName = b.BankName,
                    AccountNo = b.AccountNo,
                    ChartOfAccountId = b.ChartOfAccountId,
                    IsCash = b.IsCash,
                    IsBank = b.IsBank
                })
                .ToListAsync();

            // 3. Fetch Expense Accounts from Chart of Accounts where Transable is true
            var expenseAccounts = await GetExpenseAccountsAsync(companyId);

            // 4. Use JV (Journal Voucher) as the voucher type for Expense Vouchers
            string defaultVoucherType = "JV";
            string nextVoucherNo = await _unitOfWork.GenerateVoucherNoAsync("JV", fyId, companyId);

            // 5. Set of all Expense Account IDs for filtering
            var expenseAccountIds = new HashSet<int>(expenseAccounts.Select(a => a.Id));

            // Default filter dates (matching SalesReport: From 1st of current month to Today)
            string defaultFromDate = string.IsNullOrWhiteSpace(fromDate)
                ? DateTime.Now.ToString("yyyy-MM-01")
                : fromDate.Trim();

            string defaultToDate = string.IsNullOrWhiteSpace(toDate)
                ? DateTime.Now.ToString("yyyy-MM-dd")
                : toDate.Trim();

            DateTime? parsedFrom = null;
            DateTime? parsedTo = null;
            if (DateTime.TryParse(defaultFromDate, out var fDate)) parsedFrom = fDate.Date;
            if (DateTime.TryParse(defaultToDate, out var tDate)) parsedTo = tDate.Date.AddDays(1).AddTicks(-1);

            // 6. Base query for Daily Expense Vouchers (exclude bills, invoices, party receipts)
            var query = _context.Vouchers
                .AsNoTracking()
                .Include(v => v.AccountTransactions)
                    .ThenInclude(t => t.ChartOfAccount)
                        .ThenInclude(c => c.NatureOfAccount)
                .Where(v => (v.CompanyId == null || v.CompanyId == companyId) &&
                            (v.DeleteStatus == 0 || v.DeleteStatus == null) &&
                            v.InvoiceId == null &&
                            (v.InvoiceNo == null || v.InvoiceNo == "") &&
                            v.VendorId == null &&
                            (v.VoucherNo == null || !v.VoucherNo.StartsWith("CRV-")) &&
                            v.AccountTransactions.Any(t => t.Debit > 0 &&
                                (t.ChartofAccountId.HasValue && expenseAccountIds.Contains(t.ChartofAccountId.Value) ||
                                 (t.ChartOfAccount != null && (t.ChartOfAccount.AccountCode.StartsWith("05") || t.ChartOfAccount.AccountCode.StartsWith("5") ||
                                                               (t.ChartOfAccount.AccountName != null && (t.ChartOfAccount.AccountName.Contains("Expense") || t.ChartOfAccount.AccountName.Contains("Kharch"))))))));

            bool isFilteringReport = string.Equals(tab, "report", StringComparison.OrdinalIgnoreCase) &&
                                     (!string.IsNullOrWhiteSpace(fromDate) || !string.IsNullOrWhiteSpace(toDate) || (expenseAccountId.HasValue && expenseAccountId.Value > 0));

            if (isFilteringReport)
            {
                if (parsedFrom.HasValue) query = query.Where(v => v.VoucherDate >= parsedFrom.Value);
                if (parsedTo.HasValue) query = query.Where(v => v.VoucherDate <= parsedTo.Value);
                if (expenseAccountId.HasValue && expenseAccountId.Value > 0)
                {
                    query = query.Where(v => v.AccountTransactions.Any(t => t.Debit > 0 && t.ChartofAccountId == expenseAccountId.Value));
                }
            }

            var dbVouchers = await query
                .OrderByDescending(v => v.VoucherDate)
                .ThenByDescending(v => v.VoucherId)
                .Take(isFilteringReport ? 500 : 250)
                .ToListAsync();

            var voucherDtos = new List<ExpenseVoucherDto>();

            foreach (var v in dbVouchers)
            {
                var debitTx = v.AccountTransactions.FirstOrDefault(t => t.Debit > 0);
                var creditTx = v.AccountTransactions.FirstOrDefault(t => t.Credit > 0);

                string expenseHead = debitTx?.ChartofAccountName
                    ?? debitTx?.AccountName
                    ?? debitTx?.ChartOfAccount?.AccountName
                    ?? "Expense";

                string paidVia = creditTx?.ChartofAccountName
                    ?? creditTx?.AccountName
                    ?? creditTx?.ChartOfAccount?.AccountName
                    ?? (v.VoucherType == "BP" ? "Bank" : "Cash");

                int? expenseAccId = debitTx?.ChartofAccountId ?? debitTx?.ChartOfAccount?.Id;
                if (!expenseAccId.HasValue && !string.IsNullOrEmpty(expenseHead))
                {
                    expenseAccId = expenseAccounts.FirstOrDefault(a =>
                        string.Equals(a.AccountName, expenseHead, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(debitTx?.Code) && a.AccountCode == debitTx.Code))?.Id;
                }

                int? bankMasterId = bankMasters.FirstOrDefault(b => b.ChartOfAccountId == creditTx?.ChartofAccountId || b.BankName == creditTx?.ChartofAccountName)?.Id;
                if (!bankMasterId.HasValue && creditTx != null)
                {
                    bankMasterId = bankMasters.FirstOrDefault(b => b.ChartOfAccountId == creditTx.ChartOfAccount?.Id || b.BankName == creditTx.AccountName)?.Id;
                }
                if (!bankMasterId.HasValue)
                {
                    bankMasterId = bankMasters.FirstOrDefault(b => v.VoucherType == "BP" ? b.IsBank : b.IsCash)?.Id;
                }

                var matchedBankMaster = bankMasters.FirstOrDefault(b => b.Id == bankMasterId);
                bool isBankPayment = false;
                if (matchedBankMaster != null)
                {
                    isBankPayment = matchedBankMaster.IsBank;
                }
                else if (creditTx?.ChartOfAccount?.NatureOfAccount?.AccountName != null &&
                         creditTx.ChartOfAccount.NatureOfAccount.AccountName.Contains("Bank", StringComparison.OrdinalIgnoreCase))
                {
                    isBankPayment = true;
                }
                else if (!string.IsNullOrEmpty(paidVia) && paidVia.Contains("Bank", StringComparison.OrdinalIgnoreCase))
                {
                    isBankPayment = true;
                }
                else if (v.VoucherType == "BP")
                {
                    isBankPayment = true;
                }

                voucherDtos.Add(new ExpenseVoucherDto
                {
                    VoucherId = v.VoucherId,
                    VoucherNo = v.VoucherNo,
                    Date = v.VoucherDate.ToString("dd-MMM-yyyy"),
                    RawDate = v.VoucherDate.ToString("yyyy-MM-dd"),
                    ExpenseAccountId = expenseAccId,
                    ExpenseAccount = expenseHead,
                    Description = v.Narration ?? "",
                    Amount = v.Amount,
                    BankMasterId = bankMasterId,
                    PaidVia = paidVia,
                    IsBankPayment = isBankPayment,
                    VoucherType = isBankPayment ? "BP" : "CP"
                });
            }

            var model = new ExpenseVoucherViewModel
            {
                NextVoucherNo = nextVoucherNo,
                DefaultVoucherType = defaultVoucherType,
                ExpenseAccounts = expenseAccounts,
                BankMasters = bankMasters,
                Vouchers = voucherDtos,
                FromDate = defaultFromDate,
                ToDate = defaultToDate,
                SelectedExpenseAccountId = expenseAccountId,
                ActiveTab = string.Equals(tab, "report", StringComparison.OrdinalIgnoreCase) ? "report" : "entry"
            };

            return View(model);
        }

        [HttpGet]
        [Route("GetNextVoucherCode")]
        public async Task<IActionResult> GetNextVoucherCode(int bankMasterId)
        {
            int companyId = GetCurrentCompanyId();
            int fyId = GetCurrentFinancialYearId();

            string nextCode = await _unitOfWork.GenerateVoucherNoAsync("JV", fyId, companyId);
            return Json(new { success = true, voucherNo = nextCode, voucherType = "JV" });
        }

        [HttpPost]
        [Route("SaveVoucher")]
        [Route("AddVoucher")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveVoucher(
            int? voucherId,
            int expenseAccountId,
            decimal amount,
            int bankMasterId,
            string description,
            DateTime? date,
            string? chequeNo)
        {
            if (amount <= 0)
            {
                TempData["ErrorMessage"] = "Amount must be greater than zero.";
                return RedirectToAction(nameof(Index));
            }

            if (expenseAccountId <= 0)
            {
                TempData["ErrorMessage"] = "Please select a valid Expense Account from Chart of Accounts.";
                return RedirectToAction(nameof(Index));
            }

            if (bankMasterId <= 0)
            {
                TempData["ErrorMessage"] = "Please select a valid Bank / Cash channel from Bank Master.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                TempData["ErrorMessage"] = "Description is required.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                int companyId = GetCurrentCompanyId();
                int fyId = GetCurrentFinancialYearId();
                int? userId = GetCurrentUserId();
                string userName = User.Identity?.Name ?? "System";

                DateTime voucherDate = date ?? DateTime.Now;

                // 1. Fetch Expense Account (Level 3 COA)
                var expenseCoa = await _context.ChartOfAccounts.FindAsync(expenseAccountId);
                if (expenseCoa == null)
                {
                    TempData["ErrorMessage"] = "Selected Expense Account was not found.";
                    return RedirectToAction(nameof(Index));
                }

                // 2. Fetch Bank Master
                var bankMaster = await _context.BankMasters
                    .Include(b => b.ChartOfAccount)
                    .FirstOrDefaultAsync(b => b.Id == bankMasterId);

                if (bankMaster == null)
                {
                    TempData["ErrorMessage"] = "Selected Bank Master record was not found.";
                    return RedirectToAction(nameof(Index));
                }

                // 3. Direct Voucher Type String: "JV" (Journal Voucher for Expense)
                string voucherType = "JV";
                string entryTypeName = "Journal Voucher";

                // 4. Resolve Credit COA (Bank Master's Linked Account)
                int? bankCoaId = bankMaster.ChartOfAccountId;
                var bankCoa = bankMaster.ChartOfAccount;
                if (!bankCoaId.HasValue || bankCoa == null)
                {
                    bankCoa = bankMaster.IsBank
                        ? await _context.ChartOfAccounts.FirstOrDefaultAsync(c => c.AccountCode == "1002" || c.AccountName.Contains("Bank"))
                        : await _context.ChartOfAccounts.FirstOrDefaultAsync(c => c.AccountCode == "1001" || c.AccountName.Contains("Cash"));
                    bankCoaId = bankCoa?.Id;
                }

                Voucher? voucher = null;
                bool isEdit = voucherId.HasValue && voucherId.Value > 0;

                if (isEdit)
                {
                    voucher = await _context.Vouchers
                        .Include(v => v.AccountTransactions)
                        .FirstOrDefaultAsync(v => v.VoucherId == voucherId!.Value && (v.CompanyId == null || v.CompanyId == companyId));

                    if (voucher == null)
                    {
                        TempData["ErrorMessage"] = "Voucher to edit was not found.";
                        return RedirectToAction(nameof(Index));
                    }

                    voucher.VoucherDate = voucherDate;
                    voucher.Amount = amount;
                    voucher.Narration = description.Trim();
                    voucher.VoucherType = "JV";
                    voucher.ModifiedBy = userName;
                    voucher.ModifiedOn = DateTime.Now;

                    // Remove existing transactions to re-post fresh balanced ones
                    if (voucher.AccountTransactions != null && voucher.AccountTransactions.Any())
                    {
                        _context.AccountTransactions.RemoveRange(voucher.AccountTransactions);
                    }
                }
                else
                {
                    // Generate new sequential voucher number from DB using "JV"
                    string voucherNo = await _unitOfWork.GenerateVoucherNoAsync("JV", fyId, companyId);

                    voucher = new Voucher
                    {
                        VoucherNo = voucherNo,
                        VoucherDate = voucherDate,
                        VoucherType = "JV",
                        Amount = amount,
                        Narration = description.Trim(),
                        CompanyId = companyId,
                        FinancialYearID = fyId,
                        UserId = userId,
                        Username = userName,
                        ApprovalStatus = "Approved",
                        DeleteStatus = 0,
                        CreatedBy = userName,
                        CreatedOn = DateTime.Now
                    };

                    await _context.Vouchers.AddAsync(voucher);
                    await _context.SaveChangesAsync(); // Generates VoucherId
                }

                // Double-Entry Accounting: Exactly Two Balanced Transactions
                // 1. DEBIT: Selected Expense COA

                var debitTx = new AccountTransaction
                {
                    VoucherId = voucher.VoucherId,
                    VoucherNo = voucher.VoucherNo,
                    ChartofAccountId = expenseCoa.Id,
                    ChartofAccountName = expenseCoa.AccountName,
                    Code = expenseCoa.AccountCode,
                    AccountName = expenseCoa.AccountName,
                    Debit = amount,
                    Credit = 0,
                    Amount = amount,
                    Narration = description.Trim(),
                    EntryTypeName = entryTypeName,
                    ChequeNo = chequeNo?.Trim(),
                    ExtraDate = voucherDate,
                    CompanyId = companyId,
                    UserId = userId,
                    Type = "Expense"
                };

                // 2. CREDIT: Bank Master COA
                var creditTx = new AccountTransaction
                {
                    VoucherId = voucher.VoucherId,
                    VoucherNo = voucher.VoucherNo,
                    ChartofAccountId = bankCoaId,
                    ChartofAccountName = bankCoa?.AccountName ?? bankMaster.BankName,
                    Code = bankCoa?.AccountCode ?? "",
                    AccountName = bankCoa?.AccountName ?? bankMaster.BankName,
                    Debit = 0,
                    Credit = amount,
                    Amount = amount,
                    Narration = description.Trim(),
                    EntryTypeName = entryTypeName,
                    ChequeNo = chequeNo?.Trim(),
                    ExtraDate = voucherDate,
                    CompanyId = companyId,
                    UserId = userId
                };

                await _context.AccountTransactions.AddRangeAsync(debitTx, creditTx);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Expense Voucher {VoucherNo} (Type: {VoucherType}) {Action} for Rs {Amount:N0}.",
                    voucher.VoucherNo, voucher.VoucherType, isEdit ? "updated" : "created", amount);

                TempData["SuccessMessage"] = isEdit
                    ? $"Expense Voucher '{voucher.VoucherNo}' updated successfully."
                    : $"Expense Voucher '{voucher.VoucherNo}' for Rs {amount:N0} (Paid via {bankMaster.BankName}) saved successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Expense Voucher: {Message}", ex.Message);
                TempData["ErrorMessage"] = $"Failed to save voucher: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Route("DeleteVoucher/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteVoucher(int id)
        {
            try
            {
                int companyId = GetCurrentCompanyId();
                var voucher = await _context.Vouchers
                    .Include(v => v.AccountTransactions)
                    .FirstOrDefaultAsync(v => v.VoucherId == id && (v.CompanyId == null || v.CompanyId == companyId) && v.InvoiceId == null && (v.InvoiceNo == null || v.InvoiceNo == "") && v.VendorId == null);

                if (voucher == null)
                {
                    TempData["ErrorMessage"] = "Expense Voucher not found or cannot be deleted here.";
                    return RedirectToAction(nameof(Index));
                }

                string vNo = voucher.VoucherNo;

                if (voucher.AccountTransactions != null && voucher.AccountTransactions.Any())
                {
                    _context.AccountTransactions.RemoveRange(voucher.AccountTransactions);
                }

                _context.Vouchers.Remove(voucher);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Expense Voucher '{VoucherNo}' (ID {VoucherId}) deleted successfully.", vNo, id);
                TempData["SuccessMessage"] = $"Expense Voucher '{vNo}' deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Expense Voucher ID {Id}: {Message}", id, ex.Message);
                TempData["ErrorMessage"] = $"Failed to delete voucher: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // HELPER: EXPENSE CHART OF ACCOUNTS QUERY (TRANSABLE ONLY)
        // =========================================================================
        private async Task<List<CoaLevel3DropdownDto>> GetExpenseAccountsAsync(int companyId)
        {
            var allAccounts = await _context.ChartOfAccounts
                .AsNoTracking()
                .Include(a => a.NatureOfAccount)
                .Where(a => a.Active != false && (a.CompanyId == null || a.CompanyId == companyId))
                .ToListAsync();

            if (!allAccounts.Any())
            {
                return new List<CoaLevel3DropdownDto>();
            }

            var accountMap = allAccounts.ToDictionary(a => a.Id);

            // Helper to determine if account belongs to Expenses (Head 5000 / Nature 'Expenses')
            bool IsExpenseAccount(ChartOfAccount a)
            {
                // 1. Check NatureOfAccount
                if (a.NatureOfAccount?.AccountName != null &&
                    a.NatureOfAccount.AccountName.Contains("Expense", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // 2. Check AccountCode or AccountName directly
                if ((a.AccountCode != null && (a.AccountCode.StartsWith("05") || a.AccountCode.StartsWith("5"))) ||
                    (a.AccountName != null && (a.AccountName.Contains("Expense", StringComparison.OrdinalIgnoreCase) ||
                                               a.AccountName.Contains("Kharch", StringComparison.OrdinalIgnoreCase))))
                {
                    return true;
                }

                // 3. Check Ancestors in tree
                int? currentParent = a.ParentAccountId;
                int safeguard = 0;
                while (currentParent.HasValue && accountMap.TryGetValue(currentParent.Value, out var parent) && safeguard++ < 10)
                {
                    if (parent.NatureOfAccount?.AccountName != null &&
                        parent.NatureOfAccount.AccountName.Contains("Expense", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if ((parent.AccountCode != null && (parent.AccountCode.StartsWith("05") || parent.AccountCode.StartsWith("5"))) ||
                        (parent.AccountName != null && (parent.AccountName.Contains("Expense", StringComparison.OrdinalIgnoreCase) ||
                                                       parent.AccountName.Contains("Kharch", StringComparison.OrdinalIgnoreCase))))
                    {
                        return true;
                    }

                    currentParent = parent.ParentAccountId;
                }

                return false;
            }

            // CRITICAL REQUIREMENT: Show ONLY Expense accounts where Transable is true
            var expenseAccounts = allAccounts
                .Where(a => a.Transable && IsExpenseAccount(a))
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

            // Resiliency fallback: If no accounts explicitly matched "Expense" keyword/code but there are transable accounts, return all transable accounts
            if (!expenseAccounts.Any())
            {
                expenseAccounts = allAccounts
                    .Where(a => a.Transable)
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
            }

            return expenseAccounts;
        }

        // =========================================================================
        // HELPER: ENSURE DEFAULT BANK / CASH RECORDS EXIST
        // =========================================================================
        private async Task EnsureDefaultBankMastersAsync(int companyId)
        {
            try
            {
                bool anyBanks = await _context.BankMasters.AnyAsync(b => b.CompanyId == null || b.CompanyId == companyId);
                if (!anyBanks)
                {
                    var cashCoa = await _context.ChartOfAccounts.FirstOrDefaultAsync(c => c.AccountCode == "1001" || c.AccountName.Contains("Cash"));
                    var bankCoa = await _context.ChartOfAccounts.FirstOrDefaultAsync(c => c.AccountCode == "1002" || c.AccountName.Contains("Bank"));

                    var defaultCash = new ERP_Core.Models.BankMaster
                    {
                        BankName = "Cash in Hand",
                        AccountName = "Cash Ledger",
                        IsCash = true,
                        IsBank = false,
                        ChartOfAccountId = cashCoa?.Id,
                        CompanyId = companyId,
                        Active = true,
                        CreatedBy = "System",
                        CreatedOn = DateTime.Now
                    };

                    await _context.BankMasters.AddAsync(defaultCash);

                    if (bankCoa != null)
                    {
                        var defaultBank = new ERP_Core.Models.BankMaster
                        {
                            BankName = "Meezan Bank - Main",
                            AccountName = "Business Account",
                            AccountNo = "0102-0103987",
                            IsCash = false,
                            IsBank = true,
                            ChartOfAccountId = bankCoa.Id,
                            CompanyId = companyId,
                            Active = true,
                            CreatedBy = "System",
                            CreatedOn = DateTime.Now
                        };
                        await _context.BankMasters.AddAsync(defaultBank);
                    }

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Non-critical issue ensuring default BankMasters: {Message}", ex.Message);
            }
        }

        // =========================================================================
        // TENANT CONTEXT HELPERS
        // =========================================================================
        private int GetCurrentCompanyId()
        {
            int? sessionCompany = HttpContext.Session.GetInt32("CompanyId");
            if (sessionCompany.HasValue && sessionCompany.Value > 0)
                return sessionCompany.Value;

            var claim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId");
            if (claim != null && int.TryParse(claim.Value, out int cid) && cid > 0)
                return cid;

            int dbCompany = _context.CCompanies.Select(c => c.CompanyId).FirstOrDefault();
            return dbCompany > 0 ? dbCompany : 1;
        }

        private int GetCurrentFinancialYearId()
        {
            int? sessionFy = HttpContext.Session.GetInt32("FinancialYearId");
            if (sessionFy.HasValue && sessionFy.Value > 0)
                return sessionFy.Value;

            var claim = User.Claims.FirstOrDefault(c => c.Type == "FinancialYearId");
            if (claim != null && int.TryParse(claim.Value, out int fyId) && fyId > 0)
                return fyId;

            int activeFy = _context.CFinancialYears.Where(f => f.Active != false).Select(f => f.Id).FirstOrDefault();
            return activeFy > 0 ? activeFy : 1;
        }

        private int? GetCurrentUserId()
        {
            var claim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int uid))
                return uid;
            return null;
        }
    }
}
