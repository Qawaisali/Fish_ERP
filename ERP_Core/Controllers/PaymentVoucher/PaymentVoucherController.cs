using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ERP_Core.Attributes;
using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using BankMaster = ERP_Core.Models.BankMaster;
using ERP_Core.Repositories.IRepositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP_Core.Controllers.PaymentVoucher
{
    [Authorize]
    [Route("PaymentVoucher")]
    public class PaymentVoucherController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<PaymentVoucherController> _logger;

        public PaymentVoucherController(
            ERPDbContext context,
            IUnitOfWork unitOfWork,
            ILogger<PaymentVoucherController> logger)
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
            int? paymentAccountId,
            string? tab = "entry")
        {
            ViewData["Title"] = "Payment Voucher";
            ViewData["SubTitle"] = "Cash ya Bank ke zariye adaigi darj karein — baqaida double-entry voucher.";

            int companyId = GetCurrentCompanyId();
            int fyId = GetCurrentFinancialYearId();

            await EnsureDefaultBankMastersAsync(companyId);

            // 1. Fetch Bank Masters (Cash in Hand & Bank Accounts)
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

            // 2. Fetch Relevant Accounts (Supplier Payables, Liabilities & Operational Expenses)
            var paymentAccounts = await GetPaymentRelevantAccountsAsync(companyId);

            // 3. Default Voucher Type & Next Number (Cash Payment: CP / CPV)
            string defaultVoucherType = "CP";
            string nextVoucherNo = await _unitOfWork.GenerateVoucherNoAsync("CP", fyId, companyId);

            // Default filter dates (matching SalesReport & ExpenseVoucher: From 1st of current month to Today)
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

            // 4. Base query for Payment Vouchers
            var query = _context.Vouchers
                .AsNoTracking()
                .Include(v => v.AccountTransactions)
                    .ThenInclude(t => t.ChartOfAccount)
                .Where(v => (v.CompanyId == null || v.CompanyId == companyId) && (v.DeleteStatus == 0 || v.DeleteStatus == null) &&
                            (v.VoucherType == "CP" || v.VoucherType == "CPV" || v.VoucherType == "BP" || v.VoucherType == "BPV" ||
                             (v.VoucherNo != null && (v.VoucherNo.StartsWith("CPV-") || v.VoucherNo.StartsWith("BPV-") || v.VoucherNo.StartsWith("CP-") || v.VoucherNo.StartsWith("BP-")))));

            bool isFilteringReport = string.Equals(tab, "report", StringComparison.OrdinalIgnoreCase) &&
                                     (!string.IsNullOrWhiteSpace(fromDate) || !string.IsNullOrWhiteSpace(toDate) || (paymentAccountId.HasValue && paymentAccountId.Value > 0));

            if (isFilteringReport)
            {
                if (parsedFrom.HasValue) query = query.Where(v => v.VoucherDate >= parsedFrom.Value);
                if (parsedTo.HasValue) query = query.Where(v => v.VoucherDate <= parsedTo.Value);
                if (paymentAccountId.HasValue && paymentAccountId.Value > 0)
                {
                    query = query.Where(v => v.AccountTransactions.Any(t => t.Debit > 0 && t.ChartofAccountId == paymentAccountId.Value));
                }
            }

            var dbVouchers = await query
                .OrderByDescending(v => v.VoucherDate)
                .ThenByDescending(v => v.VoucherId)
                .Take(isFilteringReport ? 500 : 250)
                .ToListAsync();

            var voucherDtos = new List<PaymentVoucherDto>();

            foreach (var v in dbVouchers)
            {
                // In a Payment Voucher:
                // DEBIT is Party / Liability / Expense (Paid To)
                // CREDIT is Cash / Bank (Paid Via)
                var debitTx = v.AccountTransactions.FirstOrDefault(t => t.Debit > 0);
                var creditTx = v.AccountTransactions.FirstOrDefault(t => t.Credit > 0);

                string toAccount = debitTx?.ChartofAccountName
                    ?? debitTx?.AccountName
                    ?? debitTx?.ChartOfAccount?.AccountName
                    ?? "Supplier / Account";

                string paidVia = creditTx?.ChartofAccountName
                    ?? creditTx?.AccountName
                    ?? creditTx?.ChartOfAccount?.AccountName
                    ?? ((v.VoucherType == "BP" || v.VoucherType == "BPV") ? "Bank" : "Cash");

                int? toAccountId = debitTx?.ChartofAccountId;
                if (!toAccountId.HasValue || toAccountId.Value == 0)
                {
                    toAccountId = paymentAccounts.FirstOrDefault(a =>
                        string.Equals(a.AccountName, toAccount, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(debitTx?.Code) && string.Equals(a.AccountCode, debitTx.Code, StringComparison.OrdinalIgnoreCase)))?.Id;
                }

                int? bankMasterId = bankMasters.FirstOrDefault(b =>
                    (creditTx?.ChartofAccountId != null && b.ChartOfAccountId == creditTx.ChartofAccountId) ||
                    (!string.IsNullOrEmpty(creditTx?.ChartofAccountName) && string.Equals(b.BankName, creditTx.ChartofAccountName, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(paidVia) && string.Equals(b.BankName, paidVia, StringComparison.OrdinalIgnoreCase)))?.Id;

                if (!bankMasterId.HasValue || bankMasterId.Value == 0)
                {
                    bankMasterId = bankMasters.FirstOrDefault(b => (v.VoucherType == "BP" || v.VoucherType == "BPV") ? b.IsBank : b.IsCash)?.Id;
                }

                bool isBankPayment = false;
                var matchedBankMaster = bankMasters.FirstOrDefault(b => b.Id == bankMasterId);
                if (matchedBankMaster != null)
                {
                    isBankPayment = matchedBankMaster.IsBank;
                }
                else if (v.VoucherType == "BP" || v.VoucherType == "BPV" || (!string.IsNullOrEmpty(paidVia) && paidVia.Contains("Bank", StringComparison.OrdinalIgnoreCase)))
                {
                    isBankPayment = true;
                }

                string paymentType = debitTx?.Type ?? creditTx?.Type ?? "General Payment";
                if (string.IsNullOrWhiteSpace(paymentType))
                {
                    paymentType = "General Payment";
                }

                voucherDtos.Add(new PaymentVoucherDto
                {
                    VoucherId = v.VoucherId,
                    VoucherNo = v.VoucherNo,
                    Date = v.VoucherDate.ToString("dd-MMM-yyyy"),
                    RawDate = v.VoucherDate.ToString("yyyy-MM-dd"),
                    ToAccountId = toAccountId,
                    ToAccount = toAccount,
                    Description = v.Narration ?? "",
                    Amount = v.Amount,
                    BankMasterId = bankMasterId,
                    PaidVia = paidVia,
                    ChequeNo = creditTx?.ChequeNo ?? debitTx?.ChequeNo,
                    IsBankPayment = isBankPayment,
                    VoucherType = isBankPayment ? "BP" : "CP",
                    PaymentType = paymentType
                });
            }

            var model = new PaymentVoucherViewModel
            {
                NextVoucherNo = nextVoucherNo,
                DefaultVoucherType = defaultVoucherType,
                PaymentAccounts = paymentAccounts,
                BankMasters = bankMasters,
                Vouchers = voucherDtos,
                FromDate = defaultFromDate,
                ToDate = defaultToDate,
                SelectedPaymentAccountId = paymentAccountId,
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

            var bm = await _context.BankMasters.FindAsync(bankMasterId);
            string vType = (bm != null && bm.IsBank) ? "BP" : "CP";
            string nextCode = await _unitOfWork.GenerateVoucherNoAsync(vType, fyId, companyId);
            return Json(new { success = true, voucherNo = nextCode, voucherType = vType });
        }

        [HttpPost]
        [Route("SaveVoucher")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveVoucher(
            int? voucherId,
            int toAccountId,
            decimal amount,
            int bankMasterId,
            string description,
            DateTime? date,
            string? chequeNo,
            string? paymentType)
        {
            if (amount <= 0)
            {
                TempData["ErrorMessage"] = "Amount must be greater than zero.";
                return RedirectToAction(nameof(Index));
            }

            if (toAccountId <= 0)
            {
                TempData["ErrorMessage"] = "Please select a valid Party / Chart of Account.";
                return RedirectToAction(nameof(Index));
            }

            if (bankMasterId <= 0)
            {
                TempData["ErrorMessage"] = "Please select a valid Cash or Bank channel.";
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

                // 1. Fetch To Account (Supplier / Liability / Expense COA)
                var toCoa = await _context.ChartOfAccounts.FindAsync(toAccountId);
                if (toCoa == null)
                {
                    TempData["ErrorMessage"] = "Selected Party/Account was not found.";
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

                // 3. Resolve Voucher Type & Codes
                string voucherType = bankMaster.IsBank ? "BP" : "CP";
                string entryTypeName = bankMaster.IsBank ? "Bank Payment Voucher" : "Cash Payment Voucher";

                // 4. Resolve Credit COA (Bank / Cash Linked Account)
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
                    voucher.VoucherType = voucherType;
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
                    string voucherNo = await _unitOfWork.GenerateVoucherNoAsync(voucherType, fyId, companyId);

                    voucher = new Voucher
                    {
                        VoucherNo = voucherNo,
                        VoucherDate = voucherDate,
                        VoucherType = voucherType,
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
                    await _context.SaveChangesAsync();
                }

                string resolvedPaymentType = !string.IsNullOrWhiteSpace(paymentType) ? paymentType.Trim() : "General Payment";
                string cleanDesc = description.Trim();
                string fullNarration = !string.IsNullOrWhiteSpace(resolvedPaymentType) &&
                                       !string.Equals(resolvedPaymentType, "General Payment", StringComparison.OrdinalIgnoreCase) &&
                                       !cleanDesc.StartsWith($"[{resolvedPaymentType}]", StringComparison.OrdinalIgnoreCase)
                    ? $"[{resolvedPaymentType}] {cleanDesc}"
                    : cleanDesc;

                voucher.Narration = fullNarration;

                // Double-Entry Balanced Transactions:
                // 1. DEBIT: To Account (Supplier / Liability / Expense / Party)
                var debitTx = new AccountTransaction
                {
                    VoucherId = voucher.VoucherId,
                    VoucherNo = voucher.VoucherNo,
                    ChartofAccountId = toCoa.Id,
                    ChartofAccountName = toCoa.AccountName,
                    Code = toCoa.AccountCode ?? "",
                    AccountName = toCoa.AccountName,
                    Debit = amount,
                    Credit = 0,
                    Amount = amount,
                    Narration = fullNarration,
                    EntryTypeName = entryTypeName,
                    Type = resolvedPaymentType,
                    ChequeNo = chequeNo?.Trim(),
                    ExtraDate = voucherDate,
                    CompanyId = companyId,
                    UserId = userId
                };

                // 2. CREDIT: Cash / Bank (Paid Via)
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
                    Narration = fullNarration,
                    EntryTypeName = entryTypeName,
                    Type = resolvedPaymentType,
                    ChequeNo = chequeNo?.Trim(),
                    ExtraDate = voucherDate,
                    CompanyId = companyId,
                    UserId = userId
                };

                await _context.AccountTransactions.AddRangeAsync(debitTx, creditTx);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Payment Voucher {VoucherNo} (Type: {VoucherType}) {Action} for Rs {Amount:N0}.",
                    voucher.VoucherNo, voucher.VoucherType, isEdit ? "updated" : "created", amount);

                TempData["SuccessMessage"] = isEdit
                    ? $"Payment Voucher '{voucher.VoucherNo}' updated successfully."
                    : $"Payment Voucher '{voucher.VoucherNo}' for Rs {amount:N0} (Paid via {bankMaster.BankName}) saved successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Payment Voucher: {Message}", ex.Message);
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
                    .FirstOrDefaultAsync(v => v.VoucherId == id && (v.CompanyId == null || v.CompanyId == companyId));

                if (voucher == null)
                {
                    TempData["ErrorMessage"] = "Payment Voucher not found.";
                    return RedirectToAction(nameof(Index));
                }

                string vNo = voucher.VoucherNo;

                if (voucher.AccountTransactions != null && voucher.AccountTransactions.Any())
                {
                    _context.AccountTransactions.RemoveRange(voucher.AccountTransactions);
                }

                _context.Vouchers.Remove(voucher);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Payment Voucher '{VoucherNo}' (ID {VoucherId}) deleted successfully.", vNo, id);
                TempData["SuccessMessage"] = $"Payment Voucher '{vNo}' deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Payment Voucher ID {Id}: {Message}", id, ex.Message);
                TempData["ErrorMessage"] = $"Failed to delete voucher: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // HELPER: ALL TRANSABLE CHART OF ACCOUNTS FOR PAYMENT VOUCHER
        // =========================================================================
        private async Task<List<CoaLevel3DropdownDto>> GetPaymentRelevantAccountsAsync(int companyId)
        {
            var transableAccounts = await _context.ChartOfAccounts
                .AsNoTracking()
                .Include(a => a.ParentAccount)
                .Where(a => a.Transable && (a.Active == true || a.Active == null) && (a.CompanyId == null || a.CompanyId == companyId))
                .OrderBy(a => a.AccountCode)
                .ThenBy(a => a.AccountName)
                .Select(a => new CoaLevel3DropdownDto
                {
                    Id = a.Id,
                    AccountCode = a.AccountCode ?? string.Empty,
                    AccountName = a.AccountName,
                    ParentAccountId = a.ParentAccountId,
                    ParentAccountName = a.ParentAccount != null ? a.ParentAccount.AccountName : null,
                    ParentAccountCode = a.ParentAccount != null ? a.ParentAccount.AccountCode : null
                })
                .ToListAsync();

            return transableAccounts;
        }

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

                    var defaultBank = new ERP_Core.Models.BankMaster
                    {
                        BankName = "Habib Bank Ltd (Main Branch)",
                        AccountName = "Mandi Operations",
                        AccountNo = "PK12HABB00001234567801",
                        IsCash = false,
                        IsBank = true,
                        ChartOfAccountId = bankCoa?.Id,
                        CompanyId = companyId,
                        Active = true,
                        CreatedBy = "System",
                        CreatedOn = DateTime.Now
                    };

                    await _context.BankMasters.AddRangeAsync(defaultCash, defaultBank);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to ensure default BankMasters in PaymentVoucherController.");
            }
        }

        private int GetCurrentCompanyId()
        {
            var cidStr = User.FindFirstValue("CompanyId") ?? HttpContext.Session.GetString("CompanyId");
            return int.TryParse(cidStr, out int cid) && cid > 0 ? cid : 1;
        }

        private int GetCurrentFinancialYearId()
        {
            var fyStr = User.FindFirstValue("FinancialYearId") ?? HttpContext.Session.GetString("FinancialYearId");
            return int.TryParse(fyStr, out int fyId) && fyId > 0 ? fyId : 1;
        }

        private int? GetCurrentUserId()
        {
            var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("UserId");
            return int.TryParse(uidStr, out int uid) ? uid : null;
        }
    }
}
