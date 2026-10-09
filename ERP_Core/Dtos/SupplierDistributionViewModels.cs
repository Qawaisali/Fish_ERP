using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ERP_Core.Dtos
{
    public class SupplierDistributionViewModel
    {
        public int Id { get; set; }
        public string DistributionNo { get; set; } = string.Empty;
        public string Date { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");

        [Required(ErrorMessage = "Please select a Parent Supplier.")]
        public int? SelectedParentSupplierId { get; set; }
        public string? SelectedParentSupplierName { get; set; }
        public string? SelectedParentSupplierPhone { get; set; }
        public string? SelectedParentSupplierCity { get; set; }
        public string? SelectedParentSupplierCode { get; set; }
        public decimal ParentCurrentBalance { get; set; } = 0;

        public decimal TotalDistributionAmount { get; set; } = 0;
        public string? Remarks { get; set; }
        public string? VoucherNo { get; set; }

        public List<PartyDropdownDto> ParentSuppliersList { get; set; } = new();
        public List<ChildSupplierOptionDto> AllChildSuppliers { get; set; } = new();
        public List<SupplierDistributionDetailItemDto> DistributionItems { get; set; } = new();

        public List<SupplierDistributionItemDto> RecentDistributions { get; set; } = new();

        // Filters for Index
        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
        public int? FilterParentId { get; set; }
        public decimal TotalHistoricalAmount { get; set; } = 0;
        public int TotalHistoricalCount { get; set; } = 0;
    }

    public class SupplierDistributionDetailItemDto
    {
        public int Id { get; set; }
        public int ChildSupplierId { get; set; }
        public string? ChildSupplierName { get; set; }
        public string? AccountCode { get; set; }
        public string? Phone { get; set; }
        public decimal CurrentBalance { get; set; } = 0;
        public decimal Amount { get; set; } = 0;
        public string? Remarks { get; set; }
    }

    public class SupplierDistributionItemDto
    {
        public int Id { get; set; }
        public string DistributionNo { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string DateStr => Date.ToString("dd-MMM-yyyy");
        public int ParentSupplierId { get; set; }
        public string ParentSupplierName { get; set; } = string.Empty;
        public string? ParentAccountCode { get; set; }
        public decimal TotalAmount { get; set; } = 0;
        public string? Remarks { get; set; }
        public int? VoucherId { get; set; }
        public string? VoucherNo { get; set; }
        public int ChildrenCount { get; set; } = 0;
        public List<SupplierDistributionDetailItemDto> Details { get; set; } = new();
    }

    public class ChildSupplierOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public int? ParentSupplierId { get; set; }
        public string? ParentSupplierName { get; set; }
        public decimal CurrentBalance { get; set; } = 0;
    }

    public class ParentSupplierDetailsDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public decimal CurrentBalance { get; set; } = 0;
        public List<ChildSupplierOptionDto> LinkedChildren { get; set; } = new();
    }
}
