using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using BankMaster = ERP_Core.Models.BankMaster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP_Core.Controllers.TodayBusinessSummary
{
    [Authorize]
    [Route("TodayBusinessSummary")]
    [Route("BusinessSummary")]
    public class TodayBusinessSummaryController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<TodayBusinessSummaryController> _logger;

        public TodayBusinessSummaryController(ERPDbContext context, ILogger<TodayBusinessSummaryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string? filter = "today")
        {
            ViewData["Title"] = "Today's Business Summary";
            ViewData["SubTitle"] = "آج کا مکمل کاروباری خلاصہ — Wasooli, Adaigi, Kharche, Commission, aur Baqaya Balances";

            int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;

            var (from, to, dateLabel, resolvedFilter) = ResolveDateRange(filter, fromDate, toDate);

            var model = new TodayBusinessSummaryViewModel
            {
                FilterMode = resolvedFilter,
                FromDate = from.ToString("yyyy-MM-dd"),
                ToDate = to.ToString("yyyy-MM-dd"),
                DateDisplayLabel = dateLabel
            };

            try
            {
                // 1. Fetch Company Information for Report Header & Branding
                var company = await _context.CCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId);
                if (company != null)
                {
                    model.CompanyName = company.CompanyName ?? "Zahid Hussain & Sons";
                    model.CompanyAddress = company.Address;
                    model.CompanyPhone = company.Phone;
                    model.Logo = company.Logo;
                }

                var fromDateTime = from.Date;
                var toDateTime = to.Date.AddDays(1).AddTicks(-1);

                // 2. Fetch Bank Masters for Cash vs Bank identification
                var bankMasters = await _context.BankMasters.AsNoTracking()
                    .Where(b => (b.CompanyId == null || b.CompanyId == companyId) && (b.Active != false))
                    .ToListAsync();

                var bankCoaIds = new HashSet<int>(bankMasters.Where(b => b.ChartOfAccountId.HasValue).Select(b => b.ChartOfAccountId!.Value));

                // 3. Load Bills in Period for Commission, Ice, Labour, Farmer Net Payables, Gross Catch
                var periodBills = await _context.BillMasters.AsNoTracking()
                    .Include(b => b.Supplier)
                    .Include(b => b.Customer)
                    .Include(b => b.Details)
                        .ThenInclude(d => d.Customer)
                    .Where(b => (b.CompanyId == null || b.CompanyId == companyId) && (b.Active == null || b.Active == true) &&
                                b.BillDate >= fromDateTime && b.BillDate <= toDateTime)
                    .OrderByDescending(b => b.BillDate)
                    .ThenByDescending(b => b.Id)
                    .ToListAsync();

                // Compute KPI Metrics from Bills
                model.TotalBillsCount = periodBills.Count;
                model.TotalCatchGrossAmount = periodBills.Sum(b => b.TotalGrossAmount);
                model.TotalCommissionAmount = periodBills.Sum(b => b.CommissionAmount);
                model.TotalCommissionBillsCount = periodBills.Count(b => b.CommissionAmount > 0);
                model.TodayFarmerBillsPayable = periodBills.Sum(b => b.NetPayable);
                model.TodayCustomerBillsInvoiced = periodBills.Sum(b => b.TotalGrossAmount);

                model.TotalIceExpenseAmount = periodBills.Sum(b => b.IceExpense);
                model.IceBillsCount = periodBills.Count(b => b.IceExpense > 0);

                model.TotalLabourExpenseAmount = periodBills.Sum(b => b.LabourExpense);
                model.LabourBillsCount = periodBills.Count(b => b.LabourExpense > 0);

                // Martey from Period Bills
                var marteyList = new List<MarteySummaryItemDto>();
                foreach (var b in periodBills.Where(b => b.Deposit > 0))
                {
                    marteyList.Add(new MarteySummaryItemDto
                    {
                        BillId = b.Id,
                        SourceNo = b.BillNo ?? $"BL-{b.Id}",
                        Date = b.BillDate.ToString("dd-MMM-yyyy"),
                        PartyName = b.Supplier?.Name ?? "Farmer / Boat",
                        MarteyType = "Bill Deposit / Martey",
                        Amount = b.Deposit,
                        Narration = !string.IsNullOrEmpty(b.Remarks) ? b.Remarks : "Catch Bill Deposit"
                    });
                }

                // Commission Details Breakdown List
                model.CommissionDetails = periodBills.Select(b =>
                {
                    var buyerNames = b.Details.Any(d => d.Customer != null)
                        ? string.Join(", ", b.Details.Where(d => d.Customer != null).Select(d => d.Customer!.Name).Distinct())
                        : (b.Customer?.Name ?? "General Buyer");

                    return new CommissionBillSummaryDto
                    {
                        BillId = b.Id,
                        BillNo = b.BillNo ?? $"BL-{b.Id}",
                        BillDate = b.BillDate.ToString("dd-MMM-yyyy"),
                        SupplierName = b.Supplier?.Name ?? "Walk-in Farmer/Boat",
                        BuyerNames = buyerNames,
                        LotsCount = b.Details.Count,
                        GrossSaleAmount = b.TotalGrossAmount,
                        CommissionPercent = b.CommissionPercent,
                        CommissionAmount = b.CommissionAmount,
                        NetPayable = b.NetPayable,
                        Status = b.Status ?? "Due"
                    };
                }).ToList();

                // Ice & Labour Breakdown List
                model.IceLabourBreakdown = periodBills.Select(b =>
                {
                    var buyerNames = b.Details.Any(d => d.Customer != null)
                        ? string.Join(", ", b.Details.Where(d => d.Customer != null).Select(d => d.Customer!.Name).Distinct())
                        : (b.Customer?.Name ?? "General Buyer");

                    return new IceLabourBillBreakdownDto
                    {
                        BillId = b.Id,
                        BillNo = b.BillNo ?? $"BL-{b.Id}",
                        BillDate = b.BillDate.ToString("dd-MMM-yyyy"),
                        SupplierName = b.Supplier?.Name ?? "Walk-in Boat",
                        BuyerNames = buyerNames,
                        LotsCount = b.Details.Count,
                        GrossCatchAmount = b.TotalGrossAmount,
                        IceExpense = b.IceExpense,
                        LabourExpense = b.LabourExpense,
                        OtherExpense = b.OtherExpense,
                        TotalExpense = b.TotalExpense,
                        CommissionAmount = b.CommissionAmount,
                        NetPayable = b.NetPayable,
                        Remarks = b.Remarks
                    };
                }).ToList();

                // 4. Load All Vouchers & Transactions in the Period for Receives, Pays, Expense Vouchers & Martey
                var periodVouchers = await _context.Vouchers.AsNoTracking()
                    .Include(v => v.Vendor)
                    .Include(v => v.AccountTransactions)
                        .ThenInclude(t => t.ChartOfAccount)
                    .Where(v => (v.CompanyId == null || v.CompanyId == companyId) &&
                                (v.DeleteStatus == null || v.DeleteStatus == 0) &&
                                v.VoucherDate >= fromDateTime && v.VoucherDate <= toDateTime)
                    .OrderByDescending(v => v.VoucherDate)
                    .ThenByDescending(v => v.VoucherId)
                    .ToListAsync();

                // Check for Martey in Standalone Vouchers & Transactions
                foreach (var v in periodVouchers)
                {
                    // Skip bill-generated vouchers as their deposit is already captured from periodBills
                    bool isBillVoucher = v.InvoiceId.HasValue ||
                                         !string.IsNullOrEmpty(v.InvoiceNo) ||
                                         (v.Narration != null && v.Narration.StartsWith("Brokerage Bill", StringComparison.OrdinalIgnoreCase)) ||
                                         (v.VoucherType != null && v.VoucherType.Equals("Bill", StringComparison.OrdinalIgnoreCase));

                    if (isBillVoucher)
                        continue;

                    var marteyTxLines = v.AccountTransactions?.Where(t =>
                        (t.Type != null && string.Equals(t.Type, "Martey", StringComparison.OrdinalIgnoreCase)) ||
                        (t.Narration != null && t.Narration.IndexOf("Martey", StringComparison.OrdinalIgnoreCase) >= 0 && !t.Narration.Contains("Martey: Rs 0", StringComparison.OrdinalIgnoreCase))
                    ).ToList();

                    bool isMarteyVoucher = (v.VoucherType != null && v.VoucherType.IndexOf("Martey", StringComparison.OrdinalIgnoreCase) >= 0);

                    if (isMarteyVoucher || (marteyTxLines != null && marteyTxLines.Any()))
                    {
                        string vNo = !string.IsNullOrEmpty(v.VoucherNo) ? v.VoucherNo : $"VC-{v.VoucherId}";
                        if (!marteyList.Any(m => m.SourceNo == vNo))
                        {
                            decimal marteyAmount = 0;
                            if (marteyTxLines != null && marteyTxLines.Any())
                            {
                                marteyAmount = marteyTxLines.Sum(t => Math.Max(t.Debit, t.Credit));
                            }
                            else
                            {
                                marteyAmount = v.Amount;
                            }

                            if (marteyAmount > 0)
                            {
                                string party = v.Vendor?.Name ?? marteyTxLines?.FirstOrDefault(t => !IsCashOrBankName(t.AccountName ?? t.ChartofAccountName))?.AccountName ?? v.AccountTransactions?.FirstOrDefault(t => !IsCashOrBankName(t.AccountName ?? t.ChartofAccountName))?.AccountName ?? "Martey Party";
                                marteyList.Add(new MarteySummaryItemDto
                                {
                                    BillId = v.InvoiceId,
                                    SourceNo = vNo,
                                    Date = v.VoucherDate.ToString("dd-MMM-yyyy"),
                                    PartyName = party,
                                    MarteyType = "Voucher Martey",
                                    Amount = marteyAmount,
                                    Narration = v.Narration ?? "Martey Transaction"
                                });
                            }
                        }
                    }
                }

                model.MarteyDetails = marteyList.OrderByDescending(m => m.Amount).ToList();
                model.TotalMarteyAmount = marteyList.Sum(m => m.Amount);
                model.MarteyCount = marteyList.Count;

                // -----------------------------------------------------------------
                // A. RECEIVES ACCOUNT-WISE (CR, CRV, BR, BRV, Cash/Bank Receipts)
                // -----------------------------------------------------------------
                var receiveVouchers = periodVouchers.Where(v =>
                    IsReceiptVoucher(v)
                ).ToList();

                var allReceivesList = new List<TodayVoucherDetailDto>();
                foreach (var v in receiveVouchers)
                {
                    var debitTx = v.AccountTransactions.FirstOrDefault(t => t.Debit > 0);
                    var creditTx = v.AccountTransactions.FirstOrDefault(t => t.Credit > 0);

                    string fromPartyAccount = creditTx?.ChartofAccountName
                        ?? creditTx?.AccountName
                        ?? creditTx?.ChartOfAccount?.AccountName
                        ?? (v.Vendor?.Name ?? "Customer / Party");

                    string receivedInBankOrCash = debitTx?.ChartofAccountName
                        ?? debitTx?.AccountName
                        ?? debitTx?.ChartOfAccount?.AccountName
                        ?? ((v.VoucherType == "BR" || v.VoucherType == "BRV") ? "Bank Account" : "Cash in Hand");

                    bool isCash = IsCashAccount(debitTx, bankMasters);
                    if (isCash) model.TotalCashReceived += v.Amount;
                    else model.TotalBankReceived += v.Amount;

                    allReceivesList.Add(new TodayVoucherDetailDto
                    {
                        VoucherId = v.VoucherId,
                        VoucherNo = !string.IsNullOrWhiteSpace(v.VoucherNo) ? v.VoucherNo : $"CRV-{v.VoucherId}",
                        VoucherType = (v.VoucherType == "BR" || v.VoucherType == "BRV") ? "Bank Receipt (BRV)" : "Cash Receipt (CRV)",
                        Date = v.VoucherDate.ToString("dd-MMM-yyyy"),
                        AccountName = fromPartyAccount,
                        CounterpartyOrBank = receivedInBankOrCash,
                        Amount = v.Amount,
                        Narration = v.Narration ?? creditTx?.Narration ?? debitTx?.Narration,
                        ChequeNo = debitTx?.ChequeNo ?? creditTx?.ChequeNo,
                        InvoiceNo = v.InvoiceNo
                    });
                }

                model.AllReceivesVouchers = allReceivesList;
                model.TotalReceivesAmount = allReceivesList.Sum(r => r.Amount);
                model.TotalReceivesCount = allReceivesList.Count;

                // Group Receives by Account
                model.ReceivesAccountWise = allReceivesList
                    .GroupBy(r => r.AccountName.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => new AccountWiseSummaryDto
                    {
                        AccountName = g.Key,
                        AccountCategory = "Customer / Wasooli",
                        TotalAmount = g.Sum(x => x.Amount),
                        TransactionCount = g.Count(),
                        Transactions = g.OrderByDescending(x => x.VoucherId).ToList()
                    })
                    .OrderByDescending(g => g.TotalAmount)
                    .ToList();

                // -----------------------------------------------------------------
                // B. PAYS ACCOUNT-WISE (CP, CPV, BP, BPV, Cash/Bank Payments)
                // -----------------------------------------------------------------
                var paymentVouchers = periodVouchers.Where(v =>
                    IsPaymentVoucher(v)
                ).ToList();

                var allPaysList = new List<TodayVoucherDetailDto>();
                foreach (var v in paymentVouchers)
                {
                    var debitTx = v.AccountTransactions.FirstOrDefault(t => t.Debit > 0);
                    var creditTx = v.AccountTransactions.FirstOrDefault(t => t.Credit > 0);

                    string toPartyAccount = debitTx?.ChartofAccountName
                        ?? debitTx?.AccountName
                        ?? debitTx?.ChartOfAccount?.AccountName
                        ?? (v.Vendor?.Name ?? "Supplier / Account");

                    string paidFromBankOrCash = creditTx?.ChartofAccountName
                        ?? creditTx?.AccountName
                        ?? creditTx?.ChartOfAccount?.AccountName
                        ?? ((v.VoucherType == "BP" || v.VoucherType == "BPV") ? "Bank Account" : "Cash in Hand");

                    bool isCash = IsCashAccount(creditTx, bankMasters);
                    if (isCash) model.TotalCashPaid += v.Amount;
                    else model.TotalBankPaid += v.Amount;

                    allPaysList.Add(new TodayVoucherDetailDto
                    {
                        VoucherId = v.VoucherId,
                        VoucherNo = !string.IsNullOrWhiteSpace(v.VoucherNo) ? v.VoucherNo : $"CPV-{v.VoucherId}",
                        VoucherType = (v.VoucherType == "BP" || v.VoucherType == "BPV") ? "Bank Payment (BPV)" : "Cash Payment (CPV)",
                        Date = v.VoucherDate.ToString("dd-MMM-yyyy"),
                        AccountName = toPartyAccount,
                        CounterpartyOrBank = paidFromBankOrCash,
                        Amount = v.Amount,
                        Narration = v.Narration ?? debitTx?.Narration ?? creditTx?.Narration,
                        ChequeNo = creditTx?.ChequeNo ?? debitTx?.ChequeNo,
                        InvoiceNo = v.InvoiceNo
                    });
                }

                model.AllPaysVouchers = allPaysList;
                model.TotalPaysAmount = allPaysList.Sum(p => p.Amount);
                model.TotalPaysCount = allPaysList.Count;

                // Group Pays by Account
                model.PaysAccountWise = allPaysList
                    .GroupBy(p => p.AccountName.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => new AccountWiseSummaryDto
                    {
                        AccountName = g.Key,
                        AccountCategory = "Supplier / Adaigi",
                        TotalAmount = g.Sum(x => x.Amount),
                        TransactionCount = g.Count(),
                        Transactions = g.OrderByDescending(x => x.VoucherId).ToList()
                    })
                    .OrderByDescending(g => g.TotalAmount)
                    .ToList();

                // -----------------------------------------------------------------
                // C. EXPENSES FROM EXPENSE VOUCHERS
                // -----------------------------------------------------------------
                var expenseVouchers = periodVouchers.Where(v =>
                    IsExpenseVoucher(v)
                ).ToList();

                var allExpensesList = new List<TodayExpenseVoucherDetailDto>();
                foreach (var v in expenseVouchers)
                {
                    var debitTx = v.AccountTransactions.FirstOrDefault(t => t.Debit > 0 && IsExpenseAccount(t));
                    var fallbackDebit = debitTx ?? v.AccountTransactions.FirstOrDefault(t => t.Debit > 0);
                    var creditTx = v.AccountTransactions.FirstOrDefault(t => t.Credit > 0);

                    string expHead = fallbackDebit?.ChartofAccountName
                        ?? fallbackDebit?.AccountName
                        ?? fallbackDebit?.ChartOfAccount?.AccountName
                        ?? "General Mandi Expense";

                    string paidVia = creditTx?.ChartofAccountName
                        ?? creditTx?.AccountName
                        ?? (v.VoucherType == "BP" ? "Bank" : "Cash");

                    allExpensesList.Add(new TodayExpenseVoucherDetailDto
                    {
                        VoucherId = v.VoucherId,
                        VoucherNo = !string.IsNullOrWhiteSpace(v.VoucherNo) ? v.VoucherNo : $"EV-{v.VoucherId}",
                        Date = v.VoucherDate.ToString("dd-MMM-yyyy"),
                        ExpenseHead = expHead,
                        PaidVia = paidVia,
                        Amount = v.Amount,
                        Narration = v.Narration ?? fallbackDebit?.Narration,
                        ChequeNo = creditTx?.ChequeNo ?? fallbackDebit?.ChequeNo,
                        CreatedBy = v.CreatedBy ?? v.Username ?? "Accountant"
                    });
                }

                model.AllExpenseVouchers = allExpensesList;
                model.TotalExpenseVouchersAmount = allExpensesList.Sum(e => e.Amount);
                model.TotalExpenseVouchersCount = allExpensesList.Count;

                // Group Expense Vouchers by Expense Head
                model.ExpensesFromVouchers = allExpensesList
                    .GroupBy(e => e.ExpenseHead.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => new ExpenseHeadSummaryDto
                    {
                        ExpenseHead = g.Key,
                        TotalAmount = g.Sum(x => x.Amount),
                        VoucherCount = g.Count(),
                        Vouchers = g.OrderByDescending(x => x.VoucherId).ToList()
                    })
                    .OrderByDescending(g => g.TotalAmount)
                    .ToList();

                // -----------------------------------------------------------------
                // D. TOTAL FARMER PAYABLE AMOUNT (Based on Today's Bills Only)
                // -----------------------------------------------------------------
                LoadFarmerPayablesSummary(periodBills, model);

                // -----------------------------------------------------------------
                // E. TOTAL SUPPLIER / BUYER RECEIVABLE AMOUNT (Based on Today's Bills Only)
                // -----------------------------------------------------------------
                LoadPartyReceivablesSummary(periodBills, model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Today's Business Summary: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Error generating business summary: " + ex.Message;
            }

            return View(model);
        }

        // =========================================================================
        // HELPER CALCULATION METHODS (Today's Bills Scoped)
        // =========================================================================

        private static void LoadFarmerPayablesSummary(List<BillMaster> periodBills, TodayBusinessSummaryViewModel model)
        {
            var farmerList = periodBills
                .Where(b => b.SupplierId.HasValue || b.Supplier != null)
                .GroupBy(b => b.SupplierId ?? (b.Supplier != null ? b.Supplier.Id : 0))
                .Select(g =>
                {
                    var first = g.First();
                    var supName = first.Supplier?.Name ?? "Walk-in Farmer / Boat";
                    var supVillage = first.Supplier?.City ?? first.Supplier?.Address ?? "-";
                    var supPhone = first.Supplier?.Phone ?? "-";
                    var supCode = first.Supplier?.ChartOfAccount?.AccountCode ?? "-";
                    decimal billsAmt = g.Sum(b => b.TotalGrossAmount);
                    decimal commAmt = g.Sum(b => b.CommissionAmount);
                    decimal expAmt = g.Sum(b => b.TotalExpense);
                    decimal netPayable = g.Sum(b => b.NetPayable);

                    return new FarmerPayableSummaryDto
                    {
                        SupplierId = g.Key,
                        SupplierName = supName,
                        AccountCode = supCode,
                        VillageOrJetty = supVillage,
                        Phone = supPhone,
                        OpeningBalance = 0,
                        PeriodBillsAmount = billsAmt,
                        PeriodPaidAmount = commAmt + expAmt,
                        CurrentPayableBalance = netPayable,
                        Status = "Today's Bill"
                    };
                })
                .OrderByDescending(f => f.CurrentPayableBalance)
                .ToList();

            model.FarmerPayables = farmerList;
            model.TotalFarmerPayableAmount = farmerList.Sum(f => f.CurrentPayableBalance);
            model.ActiveFarmersCount = farmerList.Count;
            model.TodayFarmerBillsPayable = model.TotalFarmerPayableAmount;
        }

        private static void LoadPartyReceivablesSummary(List<BillMaster> periodBills, TodayBusinessSummaryViewModel model)
        {
            var allBillItems = periodBills
                .SelectMany(b => (b.Details != null && b.Details.Any())
                    ? b.Details.Select(d => new
                    {
                        BillId = b.Id,
                        BillNo = b.BillNo,
                        CustomerId = d.CustomerId ?? b.CustomerId ?? 0,
                        Customer = d.Customer ?? b.Customer,
                        Amount = d.Amount > 0 ? d.Amount : (b.Details.Count > 0 ? (b.TotalGrossAmount / b.Details.Count) : b.TotalGrossAmount)
                    })
                    : new[]
                    {
                        new
                        {
                            BillId = b.Id,
                            BillNo = b.BillNo,
                            CustomerId = b.CustomerId ?? (b.Customer != null ? b.Customer.Id : 0),
                            Customer = b.Customer,
                            Amount = b.TotalGrossAmount
                        }
                    })
                .ToList();

            var buyerList = allBillItems
                .GroupBy(x => x.CustomerId)
                .Select(g =>
                {
                    var first = g.First();
                    var custName = first.Customer?.Name ?? (g.Key == 0 ? "Walk-in Buyer" : "Customer / Buyer");
                    var custCity = first.Customer?.City ?? "-";
                    var custPhone = first.Customer?.Phone ?? "-";
                    var custCode = first.Customer?.ChartOfAccount?.AccountCode ?? "-";
                    decimal billedAmt = g.Sum(x => x.Amount);

                    return new PartyReceivableSummaryDto
                    {
                        PartyId = g.Key,
                        PartyName = custName,
                        PartyType = "Customer / Buyer",
                        AccountCode = custCode,
                        CityOrVillage = custCity,
                        Phone = custPhone,
                        OpeningBalance = 0,
                        PeriodInvoicedAmount = billedAmt,
                        PeriodReceivedAmount = 0,
                        CurrentReceivableBalance = billedAmt,
                        Status = "Today's Bill"
                    };
                })
                .OrderByDescending(b => b.CurrentReceivableBalance)
                .ToList();

            model.SupplierReceivables = buyerList;
            model.TotalSupplierReceivableAmount = buyerList.Sum(b => b.CurrentReceivableBalance);
            model.ActiveReceivablePartiesCount = buyerList.Count;
            model.TodayCustomerBillsInvoiced = model.TotalSupplierReceivableAmount;
        }

        private static bool IsReceiptVoucher(Voucher v)
        {
            string vt = (v.VoucherType ?? "").ToUpperInvariant();
            string vn = (v.VoucherNo ?? "").ToUpperInvariant();

            if (vt == "CR" || vt == "CRV" || vt == "BR" || vt == "BRV" ||
                vn.StartsWith("CR-") || vn.StartsWith("CRV-") || vn.StartsWith("BR-") || vn.StartsWith("BRV-") ||
                vt.Contains("RECEIPT"))
            {
                return true;
            }

            // In receipt voucher: Debit is Cash/Bank, Credit is Party/Customer
            if (v.AccountTransactions != null && v.AccountTransactions.Any())
            {
                bool hasBankOrCashDebit = v.AccountTransactions.Any(t => t.Debit > 0 && IsCashOrBankName(t.AccountName ?? t.ChartofAccountName));
                bool hasPartyCredit = v.AccountTransactions.Any(t => t.Credit > 0 && !IsCashOrBankName(t.AccountName ?? t.ChartofAccountName));
                if (hasBankOrCashDebit && hasPartyCredit) return true;
            }

            return false;
        }

        private static bool IsPaymentVoucher(Voucher v)
        {
            string vt = (v.VoucherType ?? "").ToUpperInvariant();
            string vn = (v.VoucherNo ?? "").ToUpperInvariant();

            if (vt == "CP" || vt == "CPV" || vt == "BP" || vt == "BPV" ||
                vn.StartsWith("CP-") || vn.StartsWith("CPV-") || vn.StartsWith("BP-") || vn.StartsWith("BPV-") ||
                vt.Contains("PAYMENT"))
            {
                return true;
            }

            // In payment voucher: Credit is Cash/Bank, Debit is Party/Supplier
            if (v.AccountTransactions != null && v.AccountTransactions.Any())
            {
                bool hasBankOrCashCredit = v.AccountTransactions.Any(t => t.Credit > 0 && IsCashOrBankName(t.AccountName ?? t.ChartofAccountName));
                bool hasPartyDebit = v.AccountTransactions.Any(t => t.Debit > 0 && !IsCashOrBankName(t.AccountName ?? t.ChartofAccountName) && !IsExpenseName(t.AccountName ?? t.ChartofAccountName));
                if (hasBankOrCashCredit && hasPartyDebit && !IsExpenseVoucher(v)) return true;
            }

            return false;
        }

        private static bool IsExpenseVoucher(Voucher v)
        {
            string vt = (v.VoucherType ?? "").ToUpperInvariant();
            string vn = (v.VoucherNo ?? "").ToUpperInvariant();

            if (vt == "EV" || vn.StartsWith("EV-") || vn.StartsWith("EXP-") || vt.Contains("EXPENSE"))
            {
                return true;
            }

            if (v.InvoiceId == null && string.IsNullOrEmpty(v.InvoiceNo) && v.AccountTransactions != null && v.AccountTransactions.Any())
            {
                bool hasExpenseDebit = v.AccountTransactions.Any(t => t.Debit > 0 && IsExpenseAccount(t));
                if (hasExpenseDebit && !vn.StartsWith("CR-") && !vn.StartsWith("BR-")) return true;
            }

            return false;
        }

        private static bool IsExpenseAccount(AccountTransaction t)
        {
            if (t.ChartOfAccount != null)
            {
                if (t.ChartOfAccount.AccountCode != null && (t.ChartOfAccount.AccountCode.StartsWith("05") || t.ChartOfAccount.AccountCode.StartsWith("5")))
                    return true;
                if (t.ChartOfAccount.AccountName != null && (t.ChartOfAccount.AccountName.Contains("Expense") || t.ChartOfAccount.AccountName.Contains("Kharch")))
                    return true;
            }

            string name = (t.ChartofAccountName ?? t.AccountName ?? "").ToLowerInvariant();
            return name.Contains("expense") || name.Contains("kharch") || name.Contains("tea") || name.Contains("petrol") || name.Contains("food") || name.Contains("stationery") || name.Contains("rent") || name.Contains("salary") || name.Contains("electricity");
        }

        private static bool IsExpenseName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var n = name.ToLowerInvariant();
            return n.Contains("expense") || n.Contains("kharch");
        }

        private static bool IsCashAccount(AccountTransaction? tx, List<ERP_Core.Models.BankMaster> bankMasters)
        {
            if (tx == null) return true;
            if (tx.ChartofAccountId.HasValue)
            {
                var bm = bankMasters.FirstOrDefault(b => b.ChartOfAccountId == tx.ChartofAccountId.Value);
                if (bm != null) return bm.IsCash;
            }
            string name = (tx.ChartofAccountName ?? tx.AccountName ?? "").ToLowerInvariant();
            return name.Contains("cash") || !name.Contains("bank");
        }

        private static bool IsCashOrBankName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var n = name.ToLowerInvariant();
            return n.Contains("cash") || n.Contains("bank") || n.Contains("mbl") || n.Contains("hbl") || n.Contains("ubl") || n.Contains("meezan") || n.Contains("habib") || n.Contains("mcb");
        }

        private (DateTime From, DateTime To, string DateLabel, string ResolvedFilter) ResolveDateRange(string? filter, DateTime? fromDate, DateTime? toDate)
        {
            var now = DateTime.Today;
            try
            {
                var maxBillDate = _context.BillMasters.Max(b => (DateTime?)b.BillDate);
                if (maxBillDate.HasValue && maxBillDate.Value.Date > now)
                {
                    now = maxBillDate.Value.Date;
                }
            }
            catch { }

            if (string.Equals(filter, "custom_date", StringComparison.OrdinalIgnoreCase) ||
                (string.IsNullOrWhiteSpace(filter) && fromDate.HasValue && toDate.HasValue))
            {
                var f = fromDate ?? now.AddDays(-7);
                var t = toDate ?? now;
                if (f > t) (f, t) = (t, f);
                return (f.Date, t.Date, $"{f:dd-MMM-yyyy} to {t:dd-MMM-yyyy}", "custom_date");
            }

            return (filter?.ToLowerInvariant()) switch
            {
                "today" => (now, now, $"Today ({now:dd-MMM-yyyy})", "today"),
                "yesterday" => (now.AddDays(-1), now.AddDays(-1), $"Yesterday ({now.AddDays(-1):dd-MMM-yyyy})", "yesterday"),
                "past_3_days" => (now.AddDays(-3), now, $"Past 3 Days ({now.AddDays(-3):dd-MMM} to {now:dd-MMM-yyyy})", "past_3_days"),
                "past_week" => (now.AddDays(-7), now, $"Past Week ({now.AddDays(-7):dd-MMM} to {now:dd-MMM-yyyy})", "past_week"),
                "month" => (new DateTime(now.Year, now.Month, 1), now, $"This Month ({now:MMMM yyyy})", "month"),
                _ => (now, now, $"Today ({now:dd-MMM-yyyy})", "today")
            };
        }
    }
}
