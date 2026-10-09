using System;
using System.Collections.Generic;
using System.Linq;

namespace ERP_Core.Dtos
{
    public class TodayBusinessSummaryViewModel
    {
        // Date & Filter Properties
        public string FilterMode { get; set; } = "today"; // today, yesterday, past_3_days, past_week, month, custom_date
        public string FromDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
        public string ToDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
        public string DateDisplayLabel { get; set; } = "Today's Summary";

        // Company Details for Report Header / Print
        public string CompanyName { get; set; } = "Zahid Hussain & Sons";
        public string? CompanyAddress { get; set; }
        public string? CompanyPhone { get; set; }
        public string? Logo { get; set; }

        // =========================================================================
        // 8 CORE KPI SUMMARY TOTALS
        // =========================================================================
        
        // 1. Receives Account Wise Total
        public decimal TotalReceivesAmount { get; set; } = 0;
        public int TotalReceivesCount { get; set; } = 0;
        public int ReceivesAccountsCount => ReceivesAccountWise?.Count ?? 0;

        // 2. Pays Account Wise Total
        public decimal TotalPaysAmount { get; set; } = 0;
        public int TotalPaysCount { get; set; } = 0;
        public int PaysAccountsCount => PaysAccountWise?.Count ?? 0;

        // 3. Expenses from Expense Vouchers Total
        public decimal TotalExpenseVouchersAmount { get; set; } = 0;
        public int TotalExpenseVouchersCount { get; set; } = 0;
        public int ExpenseHeadsCount => ExpensesFromVouchers?.Count ?? 0;

        // 4. Commission Total
        public decimal TotalCommissionAmount { get; set; } = 0;
        public int TotalCommissionBillsCount { get; set; } = 0;

        // 5. Total Farmer Payable Amount
        public decimal TotalFarmerPayableAmount { get; set; } = 0;
        public int ActiveFarmersCount { get; set; } = 0;
        public decimal TodayFarmerBillsPayable { get; set; } = 0;

        // 6. Total Supplier / Customer Receivable Amount
        public decimal TotalSupplierReceivableAmount { get; set; } = 0;
        public int ActiveReceivablePartiesCount { get; set; } = 0;
        public decimal TodayCustomerBillsInvoiced { get; set; } = 0;

        // 7. Total Ice Expense
        public decimal TotalIceExpenseAmount { get; set; } = 0;
        public int IceBillsCount { get; set; } = 0;

        // 8. Total Labour Expense
        public decimal TotalLabourExpenseAmount { get; set; } = 0;
        public int LabourBillsCount { get; set; } = 0;

        // 9. Martey Amount (Deposit / Martey)
        public decimal TotalMarteyAmount { get; set; } = 0;
        public int MarteyCount { get; set; } = 0;

        // Auxiliary Catch & Cash Summary
        public decimal TotalCatchGrossAmount { get; set; } = 0;
        public int TotalBillsCount { get; set; } = 0;
        public decimal TotalCashReceived { get; set; } = 0;
        public decimal TotalBankReceived { get; set; } = 0;
        public decimal TotalCashPaid { get; set; } = 0;
        public decimal TotalBankPaid { get; set; } = 0;
        public decimal NetCashFlowToday => TotalReceivesAmount - TotalPaysAmount - TotalExpenseVouchersAmount;

        // =========================================================================
        // DETAILED LIST COLLECTIONS
        // =========================================================================

        // 1. Receives Account-Wise (Grouped by Account + Detailed Vouchers)
        public List<AccountWiseSummaryDto> ReceivesAccountWise { get; set; } = new();
        public List<TodayVoucherDetailDto> AllReceivesVouchers { get; set; } = new();

        // 2. Pays Account-Wise (Grouped by Account + Detailed Vouchers)
        public List<AccountWiseSummaryDto> PaysAccountWise { get; set; } = new();
        public List<TodayVoucherDetailDto> AllPaysVouchers { get; set; } = new();

        // 3. Expenses from Expense Vouchers (Grouped by Expense Head + Detailed Vouchers)
        public List<ExpenseHeadSummaryDto> ExpensesFromVouchers { get; set; } = new();
        public List<TodayExpenseVoucherDetailDto> AllExpenseVouchers { get; set; } = new();

        // 4. Commission Breakdown
        public List<CommissionBillSummaryDto> CommissionDetails { get; set; } = new();

        // 5. Farmer Payables Summary
        public List<FarmerPayableSummaryDto> FarmerPayables { get; set; } = new();

        // 6. Supplier / Customer Receivables Summary
        public List<PartyReceivableSummaryDto> SupplierReceivables { get; set; } = new();

        // 7 & 8. Ice & Labour Expenses Breakdown
        public List<IceLabourBillBreakdownDto> IceLabourBreakdown { get; set; } = new();

        // 9. Martey Breakdown Details
        public List<MarteySummaryItemDto> MarteyDetails { get; set; } = new();
    }

    public class MarteySummaryItemDto
    {
        public int? BillId { get; set; }
        public string SourceNo { get; set; } = string.Empty; // BillNo or VoucherNo
        public string Date { get; set; } = string.Empty;
        public string PartyName { get; set; } = string.Empty;
        public string MarteyType { get; set; } = "Bill Martey / Deposit"; // "Catch Bill Deposit" or "Voucher Martey"
        public decimal Amount { get; set; }
        public string? Narration { get; set; }
    }

    public class AccountWiseSummaryDto
    {
        public int? AccountId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string AccountCode { get; set; } = string.Empty;
        public string AccountCategory { get; set; } = string.Empty; // Customer, Bank, Expense, Supplier, etc.
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
        public List<TodayVoucherDetailDto> Transactions { get; set; } = new();
    }

    public class TodayVoucherDetailDto
    {
        public int VoucherId { get; set; }
        public string VoucherNo { get; set; } = string.Empty;
        public string VoucherType { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty; // Payer or Payee
        public string CounterpartyOrBank { get; set; } = string.Empty; // Cash / Bank Account used
        public decimal Amount { get; set; }
        public string? Narration { get; set; }
        public string? ChequeNo { get; set; }
        public string? InvoiceNo { get; set; }
    }

    public class ExpenseHeadSummaryDto
    {
        public int? ChartOfAccountId { get; set; }
        public string ExpenseHead { get; set; } = string.Empty;
        public string AccountCode { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int VoucherCount { get; set; }
        public List<TodayExpenseVoucherDetailDto> Vouchers { get; set; } = new();
    }

    public class TodayExpenseVoucherDetailDto
    {
        public int VoucherId { get; set; }
        public string VoucherNo { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string ExpenseHead { get; set; } = string.Empty;
        public string PaidVia { get; set; } = "Cash";
        public decimal Amount { get; set; }
        public string? Narration { get; set; }
        public string? ChequeNo { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class CommissionBillSummaryDto
    {
        public int BillId { get; set; }
        public string BillNo { get; set; } = string.Empty;
        public string BillDate { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string BuyerNames { get; set; } = string.Empty;
        public int LotsCount { get; set; }
        public decimal GrossSaleAmount { get; set; }
        public decimal CommissionPercent { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal NetPayable { get; set; }
        public string Status { get; set; } = "Due";
    }

    public class FarmerPayableSummaryDto
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? AccountCode { get; set; }
        public string VillageOrJetty { get; set; } = "-";
        public string Phone { get; set; } = "-";
        public decimal OpeningBalance { get; set; }
        public decimal PeriodBillsAmount { get; set; }
        public decimal PeriodPaidAmount { get; set; }
        public decimal CurrentPayableBalance { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime? LastTxDate { get; set; }
    }

    public class PartyReceivableSummaryDto
    {
        public int PartyId { get; set; }
        public string PartyName { get; set; } = string.Empty;
        public string PartyType { get; set; } = "Customer"; // Customer or Supplier with debit balance
        public string? AccountCode { get; set; }
        public string CityOrVillage { get; set; } = "-";
        public string Phone { get; set; } = "-";
        public decimal OpeningBalance { get; set; }
        public decimal PeriodInvoicedAmount { get; set; }
        public decimal PeriodReceivedAmount { get; set; }
        public decimal CurrentReceivableBalance { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime? LastTxDate { get; set; }
    }

    public class IceLabourBillBreakdownDto
    {
        public int BillId { get; set; }
        public string BillNo { get; set; } = string.Empty;
        public string BillDate { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string BuyerNames { get; set; } = string.Empty;
        public int LotsCount { get; set; }
        public decimal GrossCatchAmount { get; set; }
        public decimal IceExpense { get; set; }
        public decimal LabourExpense { get; set; }
        public decimal OtherExpense { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal NetPayable { get; set; }
        public string? Remarks { get; set; }
    }
}
