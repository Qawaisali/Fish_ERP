using System;
using System.Collections.Generic;

namespace ERP_Core.Dtos
{
    public class SalesReportViewModel
    {
        public string FromDate { get; set; } = string.Empty;
        public string ToDate { get; set; } = string.Empty;
        public int? SelectedSupplierId { get; set; }
        public int? SelectedCustomerId { get; set; }
        public int? SelectedPartyId
        {
            get => PartyType == "customer" ? SelectedCustomerId : SelectedSupplierId;
            set
            {
                if (PartyType == "customer") SelectedCustomerId = value;
                else SelectedSupplierId = value;
            }
        }
        public string SelectedPartyKey => PartyType == "customer" && SelectedCustomerId.HasValue && SelectedCustomerId.Value > 0
            ? $"c_{SelectedCustomerId.Value}"
            : (SelectedSupplierId.HasValue && SelectedSupplierId.Value > 0 ? $"s_{SelectedSupplierId.Value}" : "");

        public DateTime FromDateTime => DateTime.TryParse(FromDate, out var dt) ? dt : DateTime.Today;
        public DateTime ToDateTime => DateTime.TryParse(ToDate, out var dt) ? dt : DateTime.Today;
        public string FromDateFormatted => FromDateTime.ToString("dd-MMM-yyyy");
        public string ToDateFormatted => ToDateTime.ToString("dd-MMM-yyyy");
        public string? SelectedSupplierName { get; set; }
        public string? SelectedSupplierVillage { get; set; }
        public string? SelectedSupplierPhone { get; set; }
        public string? SelectedSupplierAccountCode { get; set; }
        public string? SelectedCustomerName { get; set; }
        public string? SelectedCustomerCity { get; set; }
        public string? SelectedCustomerPhone { get; set; }
        public string? SelectedCustomerAccountCode { get; set; }
        public string PartyType { get; set; } = "supplier"; // "supplier" or "customer"

        public bool IsSingleSupplierView => SelectedSupplierId.HasValue && SelectedSupplierId.Value > 0 && (string.IsNullOrEmpty(PartyType) || PartyType == "supplier");
        public bool IsSingleCustomerView => SelectedCustomerId.HasValue && SelectedCustomerId.Value > 0 && PartyType == "customer";
        public bool IsPartyReportView => IsSingleSupplierView || IsSingleCustomerView;

        // Company Details for Report Header / Print
        public string? CompanyName { get; set; } = "Zahid Hussain & Sons";
        public string? CompanyAddress { get; set; }
        public string? Logo { get; set; }
        public string? Phone { get; set; }

        // KPI Summary Totals (Matching Box 3 · Settlement)
        // 1. Total Sale (Receivable)
        public decimal TotalGrossSale { get; set; }

        // 2. Less: Commission (Aarhat Actual Sales / Revenue)
        public decimal TotalCommissionSales { get; set; }
        public decimal AverageCommissionRate => TotalGrossSale > 0 
            ? Math.Round((TotalCommissionSales / TotalGrossSale) * 100m, 2) 
            : 0;

        // 3. Less: Expenses (Labour + Ice)
        public decimal TotalLabourExpense { get; set; }
        public decimal TotalIceExpense { get; set; }
        public decimal TotalExpenses { get; set; }

        // 3b. Less: Martey / Deposit
        public decimal TotalDeposit { get; set; }

        // 4. Net Payable to Supplier
        public decimal TotalNetPayable { get; set; }

        // Additional Volume Metrics
        public int TotalBillsCount { get; set; }
        public int TotalLotsCount { get; set; }
        public decimal TotalWeightKg { get; set; }
        public int TotalCrates { get; set; }

        // Single Supplier Statement Ledger properties matching SupplierPayable
        public decimal OpeningBalance { get; set; }
        public decimal TotalPurchased { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalPayableSum { get; set; }
        public List<PartyStatementItemDto> StatementTransactions { get; set; } = new();

        // Dropdowns for filtering
        public List<PartyDropdownDto> SuppliersList { get; set; } = new();
        public List<PartyDropdownDto> CustomersList { get; set; } = new();

        // Line item records for the report grid
        public List<SalesReportItemDto> Items { get; set; } = new();
    }

    public class SalesReportItemDto
    {
        public int BillId { get; set; }
        public string BillNo { get; set; } = string.Empty;
        public DateTime BillDate { get; set; }
        public string BillDateFormatted => BillDate.ToString("dd-MMM-yyyy");

        public int? SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string SupplierCity { get; set; } = "-";
        public string SupplierVillage => SupplierCity;

        public int? CustomerId { get; set; }
        public string CustomerNames { get; set; } = string.Empty;
        public string? FishNames { get; set; }
        public string? LotNo { get; set; }
        public string? WeightStr { get; set; }
        public decimal RatePerKg { get; set; }
        public decimal CustomerAmount { get; set; }
        public int LotsCount { get; set; }
        public int TotalCrates { get; set; }
        public decimal TotalWeightKg { get; set; }

        // Box 3 Settlement Metrics
        // 1. Total Sale (Receivable from buyer)
        public decimal GrossSaleAmount { get; set; }

        // 2. Less: Commission (Our Sales)
        public decimal CommissionPercent { get; set; }
        public decimal CommissionAmount { get; set; }

        // 3. Less: Expenses (Labour + Ice)
        public decimal LabourExpense { get; set; }
        public decimal IceExpense { get; set; }
        public decimal TotalExpense { get; set; }

        // 3b. Less: Martey / Deposit
        public decimal Deposit { get; set; }

        // 4. Net Payable to Supplier
        public decimal NetPayableAmount { get; set; }

        // Accounting References
        public string? VoucherNo { get; set; }
        public string? ExpenseVoucherNo { get; set; }
        public string Status { get; set; } = "Due";
        public string? Remarks { get; set; }
    }
}
