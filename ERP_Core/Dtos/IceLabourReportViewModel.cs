using System;
using System.Collections.Generic;

namespace ERP_Core.Dtos
{
    public class IceLabourReportViewModel
    {
        // 4 Filters
        public string FromDate { get; set; } = string.Empty;
        public string ToDate { get; set; } = string.Empty;
        public int? SelectedSupplierId { get; set; }
        public string ExpenseType { get; set; } = "All"; // "All", "Ice", "Labour"

        // Selected Supplier Metadata
        public string? SelectedSupplierName { get; set; }
        public string? SelectedSupplierVillage { get; set; }
        public string? SelectedSupplierPhone { get; set; }
        public string? SelectedSupplierAccountCode { get; set; }

        // Company Details for Report Header & Printing
        public string? CompanyName { get; set; } = "Zahid Hussain & Sons";
        public string? CompanyAddress { get; set; }
        public string? Logo { get; set; }
        public string? Phone { get; set; }

        // KPI Summary Stat Totals
        public int TotalBillsCount { get; set; }
        public decimal TotalGrossCatch { get; set; }
        public decimal TotalIceExpense { get; set; }     // LoadingExpense
        public decimal TotalLabourExpense { get; set; }  // LabourExpense
        public decimal TotalCombinedExpense => TotalIceExpense + TotalLabourExpense;
        public decimal TotalOtherExpense { get; set; }
        public decimal TotalNetPayable { get; set; }

        // Dropdown List
        public List<PartyDropdownDto> SuppliersList { get; set; } = new();

        // Line Item Records for Table
        public List<IceLabourReportItemDto> Items { get; set; } = new();
    }

    public class IceLabourReportItemDto
    {
        public int BillId { get; set; }
        public string BillNo { get; set; } = string.Empty;
        public DateTime BillDate { get; set; }
        public int? SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? SupplierCity { get; set; }
        public string? BuyerNames { get; set; }
        public int LotsCount { get; set; }
        public decimal TotalGrossCatch { get; set; }
        public decimal IceExpense { get; set; }      // LoadingExpense
        public decimal LabourExpense { get; set; }   // LabourExpense
        public decimal CombinedExpense => IceExpense + LabourExpense;
        public decimal OtherExpense { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal NetPayable { get; set; }
        public string Status { get; set; } = "Due";
        public string? Remarks { get; set; }
    }
}
