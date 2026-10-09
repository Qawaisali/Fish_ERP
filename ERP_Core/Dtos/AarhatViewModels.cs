using System;
using System.Collections.Generic;
using System.Linq;

namespace ERP_Core.Dtos
{
    // =========================================================================
    // 1. DASHBOARD VIEWMODELS
    // =========================================================================
    public class AarhatDashboardViewModel
    {
        public string Duration { get; set; } = "today";
        public string? SelectedFromDate { get; set; }
        public string? SelectedToDate { get; set; }

        public int TodayBillsCount { get; set; } = 0;
        public decimal TodaySaleAmount { get; set; } = 0;
        public decimal CommissionEarned { get; set; } = 0;
        public decimal TotalReceivable { get; set; } = 0;
        public int ActiveCustomersCount { get; set; } = 0;
        public decimal TotalPayable { get; set; } = 0;
        public int ActiveSuppliersCount { get; set; } = 0;

        public List<WeeklyTrendDto> WeeklyTrends { get; set; } = new();
        public List<WeeklyTrendDto> MonthlyTrends { get; set; } = new();
        public List<TopPayableDto> TopPayables { get; set; } = new();
        public List<TopPayableDto> RecentPayables { get; set; } = new();
        public int TopPayablesCount { get; set; } = 0;
        public decimal TopPayablesTotal { get; set; } = 0;

        public List<DashboardSummaryItemDto> TopReceivables { get; set; } = new();
        public List<DashboardSummaryItemDto> RecentReceivables { get; set; } = new();
        public int TopReceivablesCount { get; set; } = 0;
        public decimal TopReceivablesTotal { get; set; } = 0;

        public List<DashboardSummaryItemDto> TopAdvances { get; set; } = new();
        public List<DashboardSummaryItemDto> RecentAdvances { get; set; } = new();
        public int TopAdvancesCount { get; set; } = 0;
        public decimal TopAdvancesTotal { get; set; } = 0;

        public List<DashboardSummaryItemDto> TopMartey { get; set; } = new();
        public List<DashboardSummaryItemDto> RecentMartey { get; set; } = new();
        public int TopMarteyCount { get; set; } = 0;
        public decimal TopMarteyTotal { get; set; } = 0;

        public List<DashboardSummaryItemDto> TopLoans { get; set; } = new();
        public List<DashboardSummaryItemDto> RecentLoans { get; set; } = new();
        public int TopLoansCount { get; set; } = 0;
        public decimal TopLoansTotal { get; set; } = 0;

        public List<DashboardSummaryItemDto> TopOthers { get; set; } = new();
        public List<DashboardSummaryItemDto> RecentOthers { get; set; } = new();
        public int TopOthersCount { get; set; } = 0;
        public decimal TopOthersTotal { get; set; } = 0;

        public List<DashboardSummaryItemDto> TopCommissions { get; set; } = new();
        public List<DashboardSummaryItemDto> RecentCommissions { get; set; } = new();
        public int TopCommissionsCount { get; set; } = 0;
        public decimal TopCommissionsTotal { get; set; } = 0;

        public List<DashboardSummaryItemDto> TopExpenses { get; set; } = new();
        public List<DashboardSummaryItemDto> RecentExpenses { get; set; } = new();
        public int TopExpensesCount { get; set; } = 0;
        public decimal TopExpensesTotal { get; set; } = 0;

        public List<DashboardBankBalanceDto> BankBalances { get; set; } = new();
        public decimal TotalLiquidFunds => BankBalances.Sum(b => b.CurrentBalance);
        public decimal TotalBankFunds => BankBalances.Where(b => !b.IsCash).Sum(b => b.CurrentBalance);
        public decimal TotalCashInHand => BankBalances.Where(b => b.IsCash).Sum(b => b.CurrentBalance);

        public List<BillSummaryDto> RecentBills { get; set; } = new();
        public List<StaffDashboardSummaryDto> StaffMembers { get; set; } = new();
    }

    public class DashboardBankBalanceDto
    {
        public int BankId { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountNo { get; set; } = string.Empty;
        public string IbanNo { get; set; } = string.Empty;
        public string BranchOrLocation { get; set; } = string.Empty;
        public bool IsCash { get; set; } = false;
        public decimal CurrentBalance { get; set; }
        public decimal InflowAmount { get; set; }
        public decimal OutflowAmount { get; set; }
        public int? ChartOfAccountId { get; set; }
        public string Status { get; set; } = "Active";
    }

    public class WeeklyTrendDto
    {
        public string Day { get; set; } = string.Empty;
        public int Percent { get; set; }
        public decimal Amount { get; set; }
        public bool IsToday { get; set; }
    }

    public class TopPayableDto
    {
        public string SupplierName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class DashboardSummaryItemDto
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class StaffDashboardSummaryDto
    {
        public int StaffId { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int BillsCreatedCount { get; set; }
        public int BillsModifiedCount { get; set; }
        public int VouchersCreatedCount { get; set; }
        public int VouchersModifiedCount { get; set; }
        public int TotalBillsHandled => BillsCreatedCount + BillsModifiedCount;
        public int TotalVouchersHandled => VouchersCreatedCount + VouchersModifiedCount;
        public int TotalHandled => TotalBillsHandled + TotalVouchersHandled;
        public decimal TotalSalesVolume { get; set; }
        public decimal TotalCommission { get; set; }
        public decimal TotalVouchersVolume { get; set; }
        public string LastActivity { get; set; } = string.Empty;
        public List<StaffBillActivityDto> BillsWorkedOn { get; set; } = new();
        public List<StaffVoucherActivityDto> ManualVouchers { get; set; } = new();
    }

    public class StaffBillActivityDto
    {
        public int BillId { get; set; }
        public string BillNo { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public int LotsCount { get; set; }
        public decimal TotalSale { get; set; }
        public decimal Commission { get; set; }
        public decimal NetPayable { get; set; }
        public string ActivityType { get; set; } = "Created"; // "Created" or "Modified"
        public string Timestamp { get; set; } = string.Empty;
        public string Status { get; set; } = "Paid";
        public List<StaffVoucherActivityDto> LinkedVouchers { get; set; } = new();
    }

    public class StaffVoucherActivityDto
    {
        public int VoucherId { get; set; }
        public string VoucherNo { get; set; } = string.Empty;
        public string VoucherType { get; set; } = string.Empty; // "Journal Voucher (JV)", "Cash Payment (CPV)", etc.
        public string Date { get; set; } = string.Empty;
        public string PartyOrAccount { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Narration { get; set; } = string.Empty;
        public string? InvoiceNo { get; set; }
        public string ActivityType { get; set; } = "Created"; // "Created" or "Modified"
        public string Timestamp { get; set; } = string.Empty;
        public string Status { get; set; } = "Approved";
    }

    public class BillSummaryDto
    {
        public int Id { get; set; }
        public string BillNo { get; set; } = string.Empty;
        public string? VoucherNo { get; set; }
        public string? ExpenseVoucherNo { get; set; }
        public string Date { get; set; } = string.Empty;
        public int? SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public int LotsCount { get; set; }
        public decimal TotalSale { get; set; }
        public decimal Commission { get; set; }
        public decimal CommissionPercent { get; set; }
        public decimal LabourExpense { get; set; }
        public decimal IceExpense { get; set; }
        public decimal LoadingExpense { get => IceExpense; set => IceExpense = value; }
        public decimal FarmerIceExpense { get; set; } = 0;
        public decimal FreightExpense { get; set; } = 0;
        public decimal OtherExpense { get; set; }
        public decimal Expenses { get; set; }
        public decimal Deposit { get; set; } = 0;
        public decimal NetPayable { get; set; }
        public decimal PaidToSupplier { get; set; } = 0;
        public decimal RemainingPayable => Math.Round(Math.Max(0, NetPayable - PaidToSupplier), 0);
        public bool IsSupplierPaid => (PaidToSupplier >= Math.Round(NetPayable, 0) && NetPayable > 0) || Status == "Paid";
        public string Status { get; set; } = "Due"; // Paid, Due, Partial
        public string Remarks { get; set; } = string.Empty;
        public List<BillLotItemDto> Lots { get; set; } = new();
    }

    // =========================================================================
    // 2. COMPANY MASTER VIEWMODEL
    // =========================================================================
    public class CompanyMasterViewModel
    {
        public List<ERP_Core.Models.Company> Companies { get; set; } = new();
        public List<ERP_Core.Models.Company> ParentCompanies { get; set; } = new();
        public ERP_Core.Models.Company Form { get; set; } = new();
        public int TotalCompanies { get; set; }
        public int ActiveCompanies { get; set; }
        public int InactiveCompanies { get; set; }
        public int BranchCompanies { get; set; }

        // Legacy/compatibility properties
        public string CompanyName { get; set; } = string.Empty;
        public string BusinessType { get; set; } = string.Empty;
        public string MandiLicenseNo { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string WhatsApp { get; set; } = string.Empty;
        public string Ntn { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal DefaultCommissionPercent { get; set; } = 6.0m;
        public string BankName { get; set; } = string.Empty;
        public string AccountTitle { get; set; } = string.Empty;
        public string Iban { get; set; } = string.Empty;
    }

    // =========================================================================
    // 3. USER RIGHTS & STAFF VIEWMODEL
    // =========================================================================
    public class UserRightsViewModel
    {
        public List<StaffUserDto> Users { get; set; } = new();
    }

    public class StaffUserDto
    {
        public int Id { get; set; }
        public string IdentityId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "Staff"; // Owner, Manager, Staff
        public string Status { get; set; } = "Active";
    }

    // =========================================================================
    // 4. CUSTOMER MASTER VIEWMODEL
    // =========================================================================
    public class CustomerMasterViewModel
    {
        public List<CustomerDto> Customers { get; set; } = new();
    }

    public class CustomerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public int TotalBills { get; set; }
        public decimal ReceivableBalance { get; set; }
        public decimal OpeningBalance { get; set; }
        public string Cnic { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public string AccountCode { get; set; } = string.Empty;
    }

    // =========================================================================
    // 5. SUPPLIER (BOAT) MASTER VIEWMODEL
    // =========================================================================
    public class SupplierMasterViewModel
    {
        public List<SupplierDto> Suppliers { get; set; } = new();
        public List<PartyDropdownDto> ParentSuppliersList { get; set; } = new();
    }

    public class SupplierDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string VillageOrJetty { get; set; } = string.Empty;
        public int TotalLots { get; set; }
        public decimal PayableBalance { get; set; }
        public decimal OpeningBalance { get; set; }
        public string AccountNo { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public string AccountCode { get; set; } = string.Empty;
        public int? ParentSupplierId { get; set; }
        public string? ParentSupplierName { get; set; }
    }

    // =========================================================================
    // 6. FISH MASTER VIEWMODEL & DTOS
    // =========================================================================
    public class FishMasterViewModel
    {
        public List<FishDto> Fishes { get; set; } = new();
        public List<FishTypeDto> FishTypes => Fishes.Select(f => new FishTypeDto
        {
            Id = f.Id,
            Code = f.Code,
            Name = f.Name,
            Type = f.Type,
            Category = f.CategoryName ?? f.Type,
            Unit = f.Unit,
            Status = f.Status
        }).ToList();
        public ERP_Core.Models.Fish Form { get; set; } = new();
        public int TotalFish { get; set; }
        public int ActiveFish { get; set; }
        public int InactiveFish { get; set; }
        public int UniqueTypesCount { get; set; }
        public List<string> AvailableTypes { get; set; } = new() { "Sea Fish", "River Fish", "Freshwater", "Shrimp / Prawn", "Crab / Lobster", "Shellfish", "Other" };
        public List<string> AvailableUnits { get; set; } = new() { "Kg", "Maund", "Crate", "Piece", "Box" };
        public List<ProductCategoryDropdownDto> AvailableCategories { get; set; } = new();
    }

    public class FishDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Sea Fish";
        public string Unit { get; set; } = "Kg";
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryCode { get; set; }
        public bool? Active { get; set; } = true;
        public string Status => (Active == true || Active == null) ? "Active" : "Inactive";
        public DateTime? CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class FishTypeDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Sea Fish";
        public string Category { get; set; } = "Sea Fish"; // Backward compatibility
        public string Unit { get; set; } = "Kg"; // Kg, Maund, Crate
        public string Status { get; set; } = "Active";
    }

    // =========================================================================
    // 7. DAILY PURCHASE (LOT ENTRY) VIEWMODEL
    // =========================================================================
    public class DailyPurchaseViewModel
    {
        public List<PurchaseLotDto> Lots { get; set; } = new();
        public List<string> AvailableSuppliers { get; set; } = new();
        public List<string> AvailableFish { get; set; } = new();
    }

    public class PurchaseLotDto
    {
        public string LotNo { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string FishName { get; set; } = string.Empty;
        public decimal WeightKg { get; set; }
        public int Crates { get; set; }
        public string VehicleBoatNo { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string Status { get; set; } = "In Stock"; // In Stock, Sold
    }

    // =========================================================================
    // 8. NEW BILL & 3-BOX CALCULATION VIEWMODEL
    // =========================================================================
    public class NewBillViewModel
    {
        public int Id { get; set; }
        public string BillNo { get; set; } = "BL-0001";
        public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

        public int? SelectedSupplierId { get; set; }
        public string SelectedSupplier { get; set; } = string.Empty;

        public decimal CommissionPercent { get; set; } = 6.0m;
        public decimal CommissionAmount { get; set; } = 0;
        public decimal LabourExpense { get; set; } = 0;
        public decimal IceExpense { get; set; } = 0;
        public decimal LoadingExpense { get => IceExpense; set => IceExpense = value; }
        public decimal FarmerIceExpense { get; set; } = 0;
        public decimal FreightExpense { get; set; } = 0;
        public decimal OtherExpense { get; set; } = 0;
        public decimal TotalExpense { get; set; } = 0;
        public decimal Deposit { get; set; } = 0;
        public decimal TotalGrossAmount { get; set; } = 0;
        public decimal NetPayable { get; set; } = 0;
        public string Status { get; set; } = "Due"; // Due, Paid, Partial
        public string? Remarks { get; set; }

        public List<BillLotItemDto> Lots { get; set; } = new();
        public List<BillSummaryDto> RecentBills { get; set; } = new();

        public List<VendorDropdownDto> CustomersList { get; set; } = new();
        public List<VendorDropdownDto> SuppliersList { get; set; } = new();
        public List<FishDropdownDto> FishList { get; set; } = new();
        public List<BankMasterDropdownDto> CashAndBankAccounts { get; set; } = new();
    }

    public class BillPaymentRequestDto
    {
        public int BillId { get; set; }
        public string BillNo { get; set; } = string.Empty;
        public int? LotId { get; set; }
        public string? LotNo { get; set; }
        public string PartyType { get; set; } = "Supplier"; // "Supplier" or "Customer"
        public int PartyId { get; set; }
        public string PartyName { get; set; } = string.Empty;
        public string PaymentMode { get; set; } = "Cash"; // "Cash" or "Bank"
        public int PaymentAccountId { get; set; }
        public string? PaymentAccountName { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.Today;
        public string? ChequeNo { get; set; }
        public string? Narration { get; set; }
    }

    public class BillLotItemDto
    {
        public int Id { get; set; }
        public string LotNo { get; set; } = string.Empty;
        public int? FishId { get; set; }
        public string FishName { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int? Crates { get; set; }

        /// <summary>
        /// Maund-Kg-Pao formatted weight: 000-00-0 (e.g. 002-10-2 = 2*40 + 10 + 0.5 = 90.5 Kg)
        /// 3rd position: 0=0g, 1=250g, 2=500g, 3=750g
        /// </summary>
        public string WeightStr { get; set; } = "000-00-0";

        public decimal WeightKg { get; set; }
        public decimal RatePerKg { get; set; }
        public decimal Amount { get; set; }
        public decimal ReceivedAmount { get; set; } = 0;
        public decimal RemainingAmount => Math.Max(0, Amount - ReceivedAmount);
        public bool IsPaid => (ReceivedAmount >= Amount && Amount > 0);

        public static decimal ParseMaundToKg(string? weightStr)
        {
            if (string.IsNullOrWhiteSpace(weightStr)) return 0;
            var parts = weightStr.Trim().Split(new[] { '-', ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3)
            {
                decimal.TryParse(parts[0], out decimal maund);
                decimal.TryParse(parts[1], out decimal kg);
                decimal.TryParse(parts[2], out decimal paoRaw);

                decimal paoKg = 0;
                if (paoRaw == 1 || paoRaw == 25 || paoRaw == 250) paoKg = 0.25m;
                else if (paoRaw == 2 || paoRaw == 50 || paoRaw == 500) paoKg = 0.50m;
                else if (paoRaw == 3 || paoRaw == 75 || paoRaw == 750) paoKg = 0.75m;
                else if (paoRaw > 0 && paoRaw < 1) paoKg = paoRaw;
                else if (paoRaw >= 100) paoKg = paoRaw / 1000m;

                return (maund * 40m) + kg + paoKg;
            }
            else if (parts.Length == 2)
            {
                decimal.TryParse(parts[0], out decimal maund);
                decimal.TryParse(parts[1], out decimal kg);
                return (maund * 40m) + kg;
            }
            else if (decimal.TryParse(weightStr, out decimal directKg))
            {
                return directKg;
            }

            return 0;
        }

        public static string FormatKgToMaund(decimal weightKg)
        {
            if (weightKg <= 0) return "000-00-0";
            int maund = (int)(weightKg / 40m);
            decimal rem = weightKg - (maund * 40m);
            int kg = (int)rem;
            decimal paoRem = rem - kg;

            int pao = 0;
            if (paoRem >= 0.70m) pao = 3;
            else if (paoRem >= 0.45m) pao = 2;
            else if (paoRem >= 0.20m) pao = 1;

            return $"{maund:D3}-{kg:D2}-{pao}";
        }
    }

    public class FishDropdownDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Unit { get; set; } = "Kg";
        public string DisplayName => !string.IsNullOrEmpty(Code) ? $"[{Code}] {Name}" : Name;
    }

    public class VendorDropdownDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public string DisplayName => !string.IsNullOrEmpty(Code)
            ? (!string.IsNullOrEmpty(City) ? $"[{Code}] {Name} ({City})" : $"[{Code}] {Name}")
            : (!string.IsNullOrEmpty(City) ? $"{Name} ({City})" : Name);
    }

    // =========================================================================
    // 9. EXPENSE VOUCHER VIEWMODEL
    // =========================================================================
    public class ExpenseVoucherViewModel
    {
        public string NextVoucherNo { get; set; } = string.Empty;
        public string DefaultVoucherType { get; set; } = "CP";
        public List<ExpenseVoucherDto> Vouchers { get; set; } = new();
        public List<CoaLevel3DropdownDto> ExpenseAccounts { get; set; } = new();
        public List<BankMasterDropdownDto> BankMasters { get; set; } = new();

        // Filters for Expense Report (matching Sales Report)
        public string FromDate { get; set; } = string.Empty;
        public string ToDate { get; set; } = string.Empty;
        public int? SelectedExpenseAccountId { get; set; }
        public string ActiveTab { get; set; } = "entry";
    }

    public class ExpenseVoucherDto
    {
        public int VoucherId { get; set; }
        public string VoucherNo { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string RawDate { get; set; } = string.Empty;
        public int? ExpenseAccountId { get; set; }
        public string ExpenseAccount { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int? BankMasterId { get; set; }
        public string PaidVia { get; set; } = "Cash"; // Cash, Bank, etc.
        public string? ChequeNo { get; set; }
        public bool IsBankPayment { get; set; }
        public string VoucherType { get; set; }

        // Compatibility fallback for existing prints/reports
        public string Category => !string.IsNullOrEmpty(ExpenseAccount) ? ExpenseAccount : "Expense";
    }

    public class BankMasterDropdownDto
    {
        public int Id { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string? AccountNo { get; set; }
        public int? ChartOfAccountId { get; set; }
        public bool IsCash { get; set; }
        public bool IsBank { get; set; }
        public string DisplayText => IsCash
            ? $"{BankName} (Cash)"
            : (!string.IsNullOrWhiteSpace(AccountNo) ? $"{BankName} — {AccountNo}" : $"{BankName} (Bank)");
    }

    // =========================================================================
    // 9B. RECEIPT VOUCHER VIEWMODEL
    // =========================================================================
    public class ReceiptVoucherViewModel
    {
        public string NextVoucherNo { get; set; } = string.Empty;
        public string DefaultVoucherType { get; set; } = "CR";
        public List<ReceiptVoucherDto> Vouchers { get; set; } = new();
        public List<CoaLevel3DropdownDto> ReceiptAccounts { get; set; } = new();
        public List<BankMasterDropdownDto> BankMasters { get; set; } = new();

        // Filters for Receipt Report (matching Expense & Payment Reports)
        public string FromDate { get; set; } = string.Empty;
        public string ToDate { get; set; } = string.Empty;
        public int? SelectedReceiptAccountId { get; set; }
        public string ActiveTab { get; set; } = "entry";
    }

    public class ReceiptVoucherDto
    {
        public int VoucherId { get; set; }
        public string VoucherNo { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string RawDate { get; set; } = string.Empty;
        public int? FromAccountId { get; set; }
        public string FromAccount { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int? BankMasterId { get; set; }
        public string ReceivedVia { get; set; } = "Cash"; // Cash, Bank, etc.
        public string? ChequeNo { get; set; }
        public bool IsBankReceipt { get; set; }
        public string VoucherType { get; set; } = "CR"; // CR (Cash Receipt) or BR (Bank Receipt)
        public string? ReceiptType { get; set; } = "General Receipt"; // Martey, Advance, Loan, Revenue, etc.
    }

    // =========================================================================
    // 9C. PAYMENT VOUCHER VIEWMODEL
    // =========================================================================
    public class PaymentVoucherViewModel
    {
        public string NextVoucherNo { get; set; } = string.Empty;
        public string DefaultVoucherType { get; set; } = "CP";
        public List<PaymentVoucherDto> Vouchers { get; set; } = new();
        public List<CoaLevel3DropdownDto> PaymentAccounts { get; set; } = new();
        public List<BankMasterDropdownDto> BankMasters { get; set; } = new();

        // Filters for Payment Report (matching Expense & Sales Report)
        public string FromDate { get; set; } = string.Empty;
        public string ToDate { get; set; } = string.Empty;
        public int? SelectedPaymentAccountId { get; set; }
        public string ActiveTab { get; set; } = "entry";
    }

    public class PaymentVoucherDto
    {
        public int VoucherId { get; set; }
        public string VoucherNo { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string RawDate { get; set; } = string.Empty;
        public int? ToAccountId { get; set; }
        public string ToAccount { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int? BankMasterId { get; set; }
        public string PaidVia { get; set; } = "Cash"; // Cash, Bank, etc.
        public string? ChequeNo { get; set; }
        public bool IsBankPayment { get; set; }
        public string VoucherType { get; set; } = "CP"; // CP (Cash Payment) or BP (Bank Payment)
        public string? PaymentType { get; set; } = "General Payment"; // Martey, Advance, Loan, Expense, etc.
    }

    // =========================================================================
    // 10. SUPPLIER PAYABLE & CUSTOMER RECEIVABLE VIEWMODELS
    // =========================================================================
    public class PartyDropdownDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? CityOrVillage { get; set; }
        public string? Phone { get; set; }
        public int? ParentSupplierId { get; set; }
        public string? ParentSupplierName { get; set; }
        public bool IsChild => ParentSupplierId.HasValue && ParentSupplierId.Value > 0;
        public string DisplayText
        {
            get
            {
                if (IsChild)
                {
                    string parentInfo = !string.IsNullOrEmpty(ParentSupplierName) ? $" (Child of {ParentSupplierName})" : " (Sub-Account)";
                    return !string.IsNullOrEmpty(Code)
                        ? $"↳ [{Code}] {Name}{parentInfo}"
                        : $"↳ {Name}{parentInfo}";
                }
                return !string.IsNullOrEmpty(Code)
                    ? (!string.IsNullOrEmpty(CityOrVillage) ? $"[{Code}] {Name} ({CityOrVillage})" : $"[{Code}] {Name}")
                    : (!string.IsNullOrEmpty(CityOrVillage) ? $"{Name} ({CityOrVillage})" : Name);
            }
        }
        public string DisplayName => DisplayText;
    }

    public class PartyStatementItemDto
    {
        public DateTime Date { get; set; }
        public string DateFormatted => Date.ToString("dd-MMM-yyyy");
        public string RefNo { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal Balance { get; set; }
        public decimal OutstandingBalance { get; set; }
        public string? VoucherCode { get; set; }
        public string? InvoiceNo { get; set; }
        public string? ChequeNo { get; set; }
        public DateTime? ChequeDate { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string? Type { get; set; }
    }

    public class CustomerReceivableViewModel
    {
        public string FromDate { get; set; } = string.Empty;
        public string ToDate { get; set; } = string.Empty;
        public int? SelectedCustomerId { get; set; }
        public string? SelectedCustomerName { get; set; }
        public string? SelectedCustomerPhone { get; set; }
        public string? SelectedCustomerCity { get; set; }
        public string? SelectedCustomerAccountCode { get; set; }

        public decimal OpeningBalance { get; set; }
        public decimal TotalBilled { get; set; }
        public decimal TotalReceived { get; set; }
        public decimal TotalReceivableSum { get; set; }
        public decimal OutstandingBalance { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? Logo { get; set; }
        public int TotalCustomersCount { get; set; }

        public List<PartyDropdownDto> CustomersList { get; set; } = new();
        public List<CustomerReceivableItemDto> Receivables { get; set; } = new();
        public List<PartyStatementItemDto> StatementTransactions { get; set; } = new();
        public bool IsSingleCustomerView => SelectedCustomerId.HasValue && SelectedCustomerId.Value > 0;
    }

    public class CustomerReceivableItemDto
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? AccountCode { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string LastBillDate { get; set; } = string.Empty;
        public decimal OpeningBalance { get; set; }
        public decimal TotalPurchased { get; set; } // Total Billed / Gross Sales
        public decimal TotalReceived { get; set; }  // Total Payments Collected
        public decimal ReceivableBalance { get; set; } // Net Outstanding
        public string Status { get; set; } = "Due"; // Due, Partial, Cleared
    }

    public class SupplierPayableViewModel
    {
        public string FromDate { get; set; } = string.Empty;
        public string ToDate { get; set; } = string.Empty;
        public int? SelectedSupplierId { get; set; }
        public string? SelectedType { get; set; }
        public List<string> AvailableTypes { get; set; } = new() { "All", "Advance", "Martey", "Bill / Catch", "Farmer Expense", "Loan", "Labour Expense", "Ice Expense", "General Payment", "General Receipt" };
        public string? SelectedSupplierName { get; set; }
        public string? SelectedSupplierPhone { get; set; }
        public string? SelectedSupplierVillage { get; set; }
        public string? SelectedSupplierAccountCode { get; set; }

        public decimal OpeningBalance { get; set; }
        public decimal TotalOpeningBalance { get; set; }
        public decimal TotalPurchased { get; set; } // Gross Catch / Purchases
        public decimal TotalPaid { get; set; }      // Total Payments Made
        public decimal TotalPayableSum { get; set; } // Net Outstanding
        public decimal OutstandingBalance { get; set; }
        public decimal TotalAdvance { get; set; }
        public decimal TotalMartey { get; set; }
        public decimal TotalBillAmount { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? Logo { get; set; }
        public int TotalSuppliersCount { get; set; }

        public List<PartyDropdownDto> SuppliersList { get; set; } = new();
        public List<SupplierPayableItemDto> Payables { get; set; } = new();
        public List<PartyStatementItemDto> StatementTransactions { get; set; } = new();
        public bool IsSingleSupplierView => SelectedSupplierId.HasValue && SelectedSupplierId.Value > 0;
    }

    public class SupplierPayableItemDto
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? AccountCode { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string VillageOrJetty { get; set; } = string.Empty;
        public string LastBillDate { get; set; } = string.Empty;
        public decimal OpeningBalance { get; set; }
        public decimal TotalSold { get; set; }      // Gross Catch Supplied
        public decimal TotalPaid { get; set; }      // Total Paid
        public decimal PayableBalance { get; set; } // Net Outstanding
        public string Status { get; set; } = "Due"; // Due, Partial, Cleared
        public int? ParentSupplierId { get; set; }
        public string? ParentSupplierName { get; set; }
        public List<SupplierPayableItemDto> Children { get; set; } = new();
        public bool HasChildren => Children != null && Children.Count > 0;
        public bool IsChild => ParentSupplierId.HasValue && ParentSupplierId.Value > 0;
    }

    // =========================================================================
    // 11. ACCOUNT LEDGER VIEWMODEL
    // =========================================================================
    public class AccountDropdownDto
    {
        public int Id { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string? NatureName { get; set; }
        public string DisplayText => string.IsNullOrWhiteSpace(AccountCode) ? AccountName : $"{AccountCode} - {AccountName}";
    }

    public class AccountLedgerViewModel
    {
        public int? SelectedAccountId { get; set; }
        public string? SelectedAccountCode { get; set; }
        public string? SelectedAccountName { get; set; }
        public string? AccountNature { get; set; }
        public string FromDate { get; set; } = string.Empty;
        public string ToDate { get; set; } = string.Empty;

        public bool IsAccountSelected => SelectedAccountId.HasValue && SelectedAccountId.Value > 0;

        public decimal OpeningBalance { get; set; } = 0;
        public decimal TotalDebit { get; set; } = 0;
        public decimal TotalCredit { get; set; } = 0;
        public decimal ClosingBalance { get; set; } = 0;
        public decimal OutstandingBalance { get; set; } = 0;

        // Company Details
        public string CompanyName { get; set; } = "Fish Commission Agents";
        public string CompanyAddress { get; set; } = "Sukkur Fish Harbour";
        public string? Logo { get; set; }

        public List<AccountLedgerRowViewModel> Transactions { get; set; } = new();
        public List<AccountDropdownDto> AccountsList { get; set; } = new();

        // Backward compatibility
        public string SelectedAccount { get; set; } = string.Empty;
    }

    public class AccountLedgerRowViewModel
    {
        public int AccountTransactionId { get; set; }
        public DateTime? Date { get; set; }
        public string DateFormatted => Date.HasValue ? Date.Value.ToString("dd-MMM-yyyy") : "-";
        public string? VoucherNo { get; set; }
        public int? VoucherId { get; set; }
        public string? InvoiceNo { get; set; }
        public string VoucherType { get; set; } = "Voucher";
        public string Description { get; set; } = string.Empty;
        public string? ChequeNo { get; set; }
        public DateTime? ChequeDate { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal Balance { get; set; }
        public string BalanceType => Balance > 0 ? "Dr" : (Balance < 0 ? "Cr" : "Nil");
        public string RefNo => !string.IsNullOrWhiteSpace(VoucherNo) ? VoucherNo : (!string.IsNullOrWhiteSpace(InvoiceNo) ? InvoiceNo : "-");
    }

    public class LedgerItemDto
    {
        public string Date { get; set; } = string.Empty;
        public string RefNo { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal Balance { get; set; }
    }

    // =========================================================================
    // 12. FINANCIAL STATEMENTS VIEWMODELS (P&L & Balance Sheet)
    // =========================================================================
    public class ProfitLossViewModel
    {
        public string PeriodFrom { get; set; } = "2026-08-01";
        public string PeriodTo { get; set; } = "2026-08-31";
        public decimal CommissionEarned { get; set; } = 984600;
        public decimal ExpenseRecovery { get; set; } = 112300;
        public decimal TotalIncome => CommissionEarned + ExpenseRecovery;

        public decimal LabourExpense { get; set; } = 68400;
        public decimal TransportExpense { get; set; } = 51200;
        public decimal IcePackingExpense { get; set; } = 22900;
        public decimal RentUtilitiesExpense { get; set; } = 35000;
        public decimal TotalExpenses => LabourExpense + TransportExpense + IcePackingExpense + RentUtilitiesExpense;

        public decimal NetProfit => TotalIncome - TotalExpenses;
    }

    public class BalanceSheetViewModel
    {
        public string AsOnDate { get; set; } = "2026-08-31";
        // Assets
        public decimal CashInHand { get; set; } = 340000;
        public decimal BankBalance { get; set; } = 680500;
        public decimal CustomerReceivables { get; set; } = 1280000;
        public decimal StockUnsoldLots { get; set; } = 115000;
        public decimal TotalAssets => CashInHand + BankBalance + CustomerReceivables + StockUnsoldLots;

        // Liabilities & Equity
        public decimal SupplierPayables { get; set; } = 940000;
        public decimal OutstandingExpenses { get; set; } = 24600;
        public decimal OwnersEquity { get; set; } = 1450900;
        public decimal TotalLiabilitiesEquity => SupplierPayables + OutstandingExpenses + OwnersEquity;
    }

    // =========================================================================
    // 13. CHART OF ACCOUNTS VIEWMODEL & DTOS
    // =========================================================================
    public class ChartOfAccountsViewModel
    {
        public List<ChartOfAccountListDto> Accounts { get; set; } = new();
        public List<ChartOfAccountDropdownDto> AvailableParentAccounts { get; set; } = new();
        public List<NatureOfAccountDropdownDto> AvailableNatureOfAccounts { get; set; } = new();
        public ERP_Core.Models.ChartOfAccount Form { get; set; } = new();
        public int TotalAccounts { get; set; }
        public int TransableAccounts { get; set; }
        public int GroupAccounts { get; set; }
        public int ActiveAccounts { get; set; }
        public List<CoaNodeDto> TreeNodes { get; set; } = new();
    }

    public class ChartOfAccountListDto
    {
        public int Id { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public int? ParentAccountId { get; set; }
        public string? ParentAccountName { get; set; }
        public string? ParentAccountCode { get; set; }
        public int? NatureOfAccountId { get; set; }
        public string? NatureOfAccountName { get; set; }
        public string? DebtorOrCreditor { get; set; }
        public string Currency { get; set; } = "PKR";
        public bool Transable { get; set; } = true;
        public bool? Active { get; set; } = true;
        public int? CompanyId { get; set; }
        public int? LevelNo { get; set; }
        public int SubAccountsCount { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class ChartOfAccountDropdownDto
    {
        public int Id { get; set; }
        public string? AccountCode { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public int? LevelNo { get; set; }
        public string DisplayName => string.IsNullOrWhiteSpace(AccountCode) ? AccountName : $"{AccountCode} - {AccountName}";
    }

    public class NatureOfAccountDropdownDto
    {
        public int Id { get; set; }
        public string? AccountName { get; set; }
        public string? DebtorOrCreditor { get; set; }
        public string DisplayName => !string.IsNullOrEmpty(DebtorOrCreditor) ? $"{AccountName} ({DebtorOrCreditor})" : (AccountName ?? $"Nature #{Id}");
    }

    public class CoaNodeDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int? LevelNo { get; set; }
        public string? NatureName { get; set; }
        public string Currency { get; set; } = "PKR";
        public bool Transable { get; set; }
        public bool Active { get; set; } = true;
        public decimal Balance { get; set; }
        public bool IsLeaf { get; set; }
        public List<CoaNodeDto> Children { get; set; } = new();
    }

    // =========================================================================
    // 14. SETTINGS VIEWMODEL
    // =========================================================================
    public class SettingsViewModel
    {
        public string BillPrintFormat { get; set; } = "A4"; // Thermal (3 inch), A5, A4
        public string Currency { get; set; } = "PKR (Rs)";
        public decimal DefaultCommissionPercent { get; set; } = 6.0m;
        public string FinancialYearStart { get; set; } = "2026-07-01";
        public string BackupFrequency { get; set; } = "Daily";
        public int LowStockAlertKg { get; set; } = 50;
    }

    // =========================================================================
    // 15. FINANCIAL YEAR VIEWMODEL
    // =========================================================================
    public class FinancialYearViewModel
    {
        public List<FinancialYearListDto> FinancialYears { get; set; } = new();
        public List<ERP_Core.Models.Company> AvailableCompanies { get; set; } = new();
        public ERP_Core.Models.FinancialYear Form { get; set; } = new();
        public int TotalYears { get; set; }
        public int ActiveYears { get; set; }
        public int LockedYears { get; set; }
        public string CurrentActiveYearName { get; set; } = string.Empty;
    }

    public class FinancialYearListDto
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool CurrentYear { get; set; }
        public bool LockYear { get; set; }
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public bool? Active { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
    }

    // =========================================================================
    // 16. PRODUCT CATEGORY VIEWMODEL & DTOS
    // =========================================================================
    public class ProductCategoryViewModel
    {
        public List<ProductCategoryListDto> Categories { get; set; } = new();
        public List<ChartOfAccountDropdownDto> AvailableTransableAccounts { get; set; } = new();
        public ERP_Core.Models.ProductCategory Form { get; set; } = new();
        public int TotalCategories { get; set; }
        public int ActiveCategories { get; set; }
        public int InactiveCategories { get; set; }
    }

    public class ProductCategoryListDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool? Active { get; set; } = true;
        public int? PurchaseAccountId { get; set; }
        public string? PurchaseAccountName { get; set; }
        public string? PurchaseAccountCode { get; set; }
        public int? SaleAccountId { get; set; }
        public string? SaleAccountName { get; set; }
        public string? SaleAccountCode { get; set; }
        public int? PurchaseReturnAccountId { get; set; }
        public string? PurchaseReturnAccountName { get; set; }
        public string? PurchaseReturnAccountCode { get; set; }
        public int? SaleReturnAccountId { get; set; }
        public string? SaleReturnAccountName { get; set; }
        public string? SaleReturnAccountCode { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class ProductCategoryDropdownDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName => !string.IsNullOrEmpty(Code) ? $"[{Code}] {Name}" : Name;
    }
}


