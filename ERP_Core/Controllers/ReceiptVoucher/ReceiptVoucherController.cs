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

namespace ERP_Core.Controllers.ReceiptVoucher
{
    [Authorize]
    [Route("ReceiptVoucher")]
    public class ReceiptVoucherController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ReceiptVoucherController> _logger;

        public ReceiptVoucherController(
            ERPDbContext context,
            IUnitOfWork unitOfWork,
            ILogger<ReceiptVoucherController> logger)
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
            int? receiptAccountId,
            string? tab = "entry")
        {
            ViewData["Title"] = "Receipt Voucher";
            ViewData["SubTitle"] = "Cash ya Bank ke zariye wasooli darj karein — baqaida double-entry voucher.";

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

            // 2. Fetch Relevant Accounts (Customer Receivables, Debtors, Revenues & Income only)
            var receiptAccounts = await GetReceiptRelevantAccountsAsync(companyId);

            // 3. Default Voucher Type & Next Number (Cash Receipt: CR / CRV)
            string defaultVoucherType = "CR";
            string nextVoucherNo = await _unitOfWork.GenerateVoucherNoAsync("CR", fyId, companyId);

            // Default filter dates (matching SalesReport, ExpenseVoucher & PaymentVoucher: From 1st of current month to Today)
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

            // 4. Base query for Receipt Vouchers
            var query = _context.Vouchers
                .AsNoTracking()
                .Include(v => v.AccountTransactions)
                    .ThenInclude(t => t.ChartOfAccount)
                .Where(v => (v.CompanyId == null || v.CompanyId == companyId) && (v.DeleteStatus == 0 || v.DeleteStatus == null) &&
                            (v.VoucherType == "CR" || v.VoucherType == "CRV" || v.VoucherType == "BR" || v.VoucherType == "BRV" ||
                             (v.VoucherNo != null && (v.VoucherNo.StartsWith("CRV-") || v.VoucherNo.StartsWith("BRV-") || v.VoucherNo.StartsWith("CR-") || v.VoucherNo.StartsWith("BR-")))));

            bool isFilteringReport = string.Equals(tab, "report", StringComparison.OrdinalIgnoreCase) &&
                                     (!string.IsNullOrWhiteSpace(fromDate) || !string.IsNullOrWhiteSpace(toDate) || (receiptAccountId.HasValue && receiptAccountId.Value > 0));

            if (isFilteringReport)
            {
                if (parsedFrom.HasValue) query = query.Where(v => v.VoucherDate >= parsedFrom.Value);
                if (parsedTo.HasValue) query = query.Where(v => v.VoucherDate <= parsedTo.Value);
                if (receiptAccountId.HasValue && receiptAccountId.Value > 0)
                {
                    query = query.Where(v => v.AccountTransactions.Any(t => t.Credit > 0 && t.ChartofAccountId == receiptAccountId.Value));
                }
            }

            var dbVouchers = await query
                .OrderByDescending(v => v.VoucherDate)
                .ThenByDescending(v => v.VoucherId)
                .Take(isFilteringReport ? 500 : 250)
                .ToListAsync();

            var voucherDtos = new List<ReceiptVoucherDto>();

            foreach (var v in dbVouchers)
            {
                // In a Receipt Voucher:
                // DEBIT is Cash/Bank (Received In)
                // CREDIT is Customer/Revenue (From Account)
                var debitTx = v.AccountTransactions.FirstOrDefault(t => t.Debit > 0);
                var creditTx = v.AccountTransactions.FirstOrDefault(t => t.Credit > 0);

                string fromAccount = creditTx?.ChartofAccountName
                    ?? creditTx?.AccountName
                    ?? creditTx?.ChartOfAccount?.AccountName
                    ?? "Customer / Party";

                string receivedVia = debitTx?.ChartofAccountName
                    ?? debitTx?.AccountName
                    ?? debitTx?.ChartOfAccount?.AccountName
                    ?? ((v.VoucherType == "BR" || v.VoucherType == "BRV") ? "Bank" : "Cash");

                int? fromAccountId = creditTx?.ChartofAccountId;
                if (!fromAccountId.HasValue || fromAccountId.Value == 0)
                {
                    fromAccountId = receiptAccounts.FirstOrDefault(a =>
                        string.Equals(a.AccountName, fromAccount, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(creditTx?.Code) && string.Equals(a.AccountCode, creditTx.Code, StringComparison.OrdinalIgnoreCase)))?.Id;
                }

                int? bankMasterId = bankMasters.FirstOrDefault(b =>
                    (debitTx?.ChartofAccountId != null && b.ChartOfAccountId == debitTx.ChartofAccountId) ||
                    (!string.IsNullOrEmpty(debitTx?.ChartofAccountName) && string.Equals(b.BankName, debitTx.ChartofAccountName, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(receivedVia) && string.Equals(b.BankName, receivedVia, StringComparison.OrdinalIgnoreCase)))?.Id;

                if (!bankMasterId.HasValue || bankMasterId.Value == 0)
                {
                    bankMasterId = bankMasters.FirstOrDefault(b => (v.VoucherType == "BR" || v.VoucherType == "BRV") ? b.IsBank : b.IsCash)?.Id;
                }

                bool isBankReceipt = false;
                var matchedBankMaster = bankMasters.FirstOrDefault(b => b.Id == bankMasterId);
                if (matchedBankMaster != null)
                {
                    isBankReceipt = matchedBankMaster.IsBank;
                }
                else if (v.VoucherType == "BR" || v.VoucherType == "BRV" || (!string.IsNullOrEmpty(receivedVia) && receivedVia.Contains("Bank", StringComparison.OrdinalIgnoreCase)))
                {
                    isBankReceipt = true;
                }

                string receiptType = debitTx?.Type ?? creditTx?.Type ?? "General Receipt";
                if (string.IsNullOrWhiteSpace(receiptType))
                {
                    receiptType = "General Receipt";
                }

                voucherDtos.Add(new ReceiptVoucherDto
                {
                    VoucherId = v.VoucherId,
                    VoucherNo = v.VoucherNo,
                    Date = v.VoucherDate.ToString("dd-MMM-yyyy"),
                    RawDate = v.VoucherDate.ToString("yyyy-MM-dd"),
                    FromAccountId = fromAccountId,
                    FromAccount = fromAccount,
                    Description = v.Narration ?? "",
                    Amount = v.Amount,
                    BankMasterId = bankMasterId,
                    ReceivedVia = receivedVia,
                    ChequeNo = debitTx?.ChequeNo ?? creditTx?.ChequeNo,
                    IsBankReceipt = isBankReceipt,
                    VoucherType = isBankReceipt ? "BR" : "CR",
                    ReceiptType = receiptType
                });
            }

            var model = new ReceiptVoucherViewModel
            {
                NextVoucherNo = nextVoucherNo,
                DefaultVoucherType = defaultVoucherType,
                ReceiptAccounts = receiptAccounts,
                BankMasters = bankMasters,
                Vouchers = voucherDtos,
                FromDate = defaultFromDate,
                ToDate = defaultToDate,
                SelectedReceiptAccountId = receiptAccountId,
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
            string vType = (bm != null && bm.IsBank) ? "BR" : "CR";
            string nextCode = await _unitOfWork.GenerateVoucherNoAsync(vType, fyId, companyId);
            return Json(new { success = true, voucherNo = nextCode, voucherType = vType });
        }

        [HttpPost]
        [Route("SaveVoucher")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveVoucher(
            int? voucherId,
            int fromAccountId,
            decimal amount,
            int bankMasterId,
            string description,
            DateTime? date,
            string? chequeNo,
            string? receiptType)
        {
            if (amount <= 0)
            {
                TempData["ErrorMessage"] = "Amount must be greater than zero.";
                return RedirectToAction(nameof(Index));
            }

            if (fromAccountId <= 0)
            {
                TempData["ErrorMessage"] = "Please select a valid Party / Revenue / Customer Account.";
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

                // 1. Fetch From Account (Customer / Revenue COA)
                var fromCoa = await _context.ChartOfAccounts.FindAsync(fromAccountId);
                if (fromCoa == null)
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
                string voucherType = bankMaster.IsBank ? "BR" : "CR";
                string entryTypeName = bankMaster.IsBank ? "Bank Receipt Voucher" : "Cash Receipt Voucher";

                // 4. Resolve Debit COA (Bank / Cash Linked Account)
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

                string resolvedReceiptType = !string.IsNullOrWhiteSpace(receiptType) ? receiptType.Trim() : "General Receipt";
                string cleanDesc = description.Trim();
                string fullNarration = !string.IsNullOrWhiteSpace(resolvedReceiptType) &&
                                       !string.Equals(resolvedReceiptType, "General Receipt", StringComparison.OrdinalIgnoreCase) &&
                                       !cleanDesc.StartsWith($"[{resolvedReceiptType}]", StringComparison.OrdinalIgnoreCase)
                    ? $"[{resolvedReceiptType}] {cleanDesc}"
                    : cleanDesc;

                voucher.Narration = fullNarration;

                // Double-Entry Balanced Transactions:
                // 1. DEBIT: Cash / Bank (Received In)
                var debitTx = new AccountTransaction
                {
                    VoucherId = voucher.VoucherId,
                    VoucherNo = voucher.VoucherNo,
                    ChartofAccountId = bankCoaId,
                    ChartofAccountName = bankCoa?.AccountName ?? bankMaster.BankName,
                    Code = bankCoa?.AccountCode ?? "",
                    AccountName = bankCoa?.AccountName ?? bankMaster.BankName,
                    Debit = amount,
                    Credit = 0,
                    Amount = amount,
                    Narration = fullNarration,
                    EntryTypeName = entryTypeName,
                    Type = resolvedReceiptType,
                    ChequeNo = chequeNo?.Trim(),
                    ExtraDate = voucherDate,
                    CompanyId = companyId,
                    UserId = userId
                };

                // 2. CREDIT: From Account (Customer / Revenue / Party)
                var creditTx = new AccountTransaction
                {
                    VoucherId = voucher.VoucherId,
                    VoucherNo = voucher.VoucherNo,
                    ChartofAccountId = fromCoa.Id,
                    ChartofAccountName = fromCoa.AccountName,
                    Code = fromCoa.AccountCode ?? "",
                    AccountName = fromCoa.AccountName,
                    Debit = 0,
                    Credit = amount,
                    Amount = amount,
                    Narration = fullNarration,
                    EntryTypeName = entryTypeName,
                    Type = resolvedReceiptType,
                    ChequeNo = chequeNo?.Trim(),
                    ExtraDate = voucherDate,
                    CompanyId = companyId,
                    UserId = userId
                };

                await _context.AccountTransactions.AddRangeAsync(debitTx, creditTx);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Receipt Voucher {VoucherNo} (Type: {VoucherType}) {Action} for Rs {Amount:N0}.",
                    voucher.VoucherNo, voucher.VoucherType, isEdit ? "updated" : "created", amount);

                TempData["SuccessMessage"] = isEdit
                    ? $"Receipt Voucher '{voucher.VoucherNo}' updated successfully."
                    : $"Receipt Voucher '{voucher.VoucherNo}' for Rs {amount:N0} (Received via {bankMaster.BankName}) saved successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Receipt Voucher: {Message}", ex.Message);
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
                    TempData["ErrorMessage"] = "Receipt Voucher not found.";
                    return RedirectToAction(nameof(Index));
                }

                string vNo = voucher.VoucherNo;

                if (voucher.AccountTransactions != null && voucher.AccountTransactions.Any())
                {
                    _context.AccountTransactions.RemoveRange(voucher.AccountTransactions);
                }

                _context.Vouchers.Remove(voucher);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Receipt Voucher '{VoucherNo}' (ID {VoucherId}) deleted successfully.", vNo, id);
                TempData["SuccessMessage"] = $"Receipt Voucher '{vNo}' deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Receipt Voucher ID {Id}: {Message}", id, ex.Message);
                TempData["ErrorMessage"] = $"Failed to delete voucher: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // HELPER: ALL TRANSABLE CHART OF ACCOUNTS EXCEPT EXPENSES FOR RECEIPT VOUCHER
        // =========================================================================
        private async Task<List<CoaLevel3DropdownDto>> GetReceiptRelevantAccountsAsync(int companyId)
        {
            var transableAccounts = await _context.ChartOfAccounts
                .AsNoTracking()
                .Include(a => a.ParentAccount)
                .Include(a => a.NatureOfAccount)
                .Where(a => a.Transable &&
                            (a.Active == true || a.Active == null) &&
                            (a.CompanyId == null || a.CompanyId == companyId) &&
                            !(a.AccountCode != null && a.AccountCode.StartsWith("5")) &&
                            !(a.NatureOfAccount != null && a.NatureOfAccount.AccountName != null && a.NatureOfAccount.AccountName.Contains("Expense")) &&
                            !(a.AccountName.Contains("Expense") || a.AccountName.Contains("خرچہ")))
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
                _logger.LogWarning(ex, "Failed to ensure default BankMasters in ReceiptVoucherController.");
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
