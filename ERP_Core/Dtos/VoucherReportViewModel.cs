using System;
using System.Collections.Generic;

namespace ERP_Core.Dtos
{
    public class VoucherReportViewModel
    {
        // -------------------------------------------------------------------------
        // 5 Primary Filter Inputs
        // -------------------------------------------------------------------------

        // 1. From Date (Default: Today)
        public string FromDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");

        // 2. To Date (Default: Today)
        public string ToDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");

        // 3. Voucher Type (BP, CP, BR, CR, JV, or empty for All)
        public string? SelectedVoucherType { get; set; }

        // 4. Voucher No (Dependent dropdown - filtered by type, or empty for All)
        public string? SelectedVoucherNo { get; set; }

        // 5. Chart of Account Option (Specific transable account or null for All)
        public int? SelectedAccountId { get; set; }

        // Selected Account Details for display
        public string? SelectedAccountName { get; set; }
        public string? SelectedAccountCode { get; set; }

        // -------------------------------------------------------------------------
        // Dropdown Lists
        // -------------------------------------------------------------------------

        public List<VoucherTypeOptionDto> VoucherTypesList { get; set; } = new()
        {
            new VoucherTypeOptionDto { Code = "", Name = "-- All Voucher Types (BP, CP, BR, CR, JV) --" },
            new VoucherTypeOptionDto { Code = "BP", Name = "BP — Bank Payment Voucher" },
            new VoucherTypeOptionDto { Code = "CP", Name = "CP — Cash Payment Voucher" },
            new VoucherTypeOptionDto { Code = "BR", Name = "BR — Bank Receipt Voucher" },
            new VoucherTypeOptionDto { Code = "CR", Name = "CR — Cash Receipt Voucher" },
            new VoucherTypeOptionDto { Code = "JV", Name = "JV — Journal Voucher" }
        };

        public List<VoucherLookupDto> VouchersList { get; set; } = new();

        public List<AccountDropdownDto> AccountsList { get; set; } = new();

        // -------------------------------------------------------------------------
        // Summary KPIs
        // -------------------------------------------------------------------------

        public int TotalVouchersCount { get; set; } = 0;
        public decimal TotalDebitAmount { get; set; } = 0;
        public decimal TotalCreditAmount { get; set; } = 0;
        public int TotalLinesCount { get; set; } = 0;
        public bool IsBalanced => Math.Abs(TotalDebitAmount - TotalCreditAmount) < 0.01m;

        // Company Details for Printout (Exact match to Zahid Hussain & Sons Mandi header)
        public string CompanyName { get; set; } = "Zahid Hussain & Sons";
        public string CompanyAddress { get; set; } = "Fish Commission Agents · Sukkur Fish Harbour";
        public string CompanyPhone { get; set; } = "Zahid Hussain (0300-3110890) | Yaseen (0334-3942837)";
        public string? Logo { get; set; }

        // -------------------------------------------------------------------------
        // Report Transaction Lines & Groupings
        // -------------------------------------------------------------------------

        public List<VoucherReportItemDto> Items { get; set; } = new();

        // Grouped by Voucher: 1 row block per voucher with multiple entries inside
        public List<VoucherReportGroupDto> VoucherGroups { get; set; } = new();

        // Helper date display properties
        public string FromDateFormatted => DateTime.TryParse(FromDate, out var d) ? d.ToString("dd-MMM-yyyy") : FromDate;
        public string ToDateFormatted => DateTime.TryParse(ToDate, out var d) ? d.ToString("dd-MMM-yyyy") : ToDate;
    }

    public class VoucherTypeOptionDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class VoucherLookupDto
    {
        public int VoucherId { get; set; }
        public string VoucherNo { get; set; } = string.Empty;
        public string VoucherType { get; set; } = string.Empty;
        public DateTime VoucherDate { get; set; }
        public decimal Amount { get; set; }
        public string DisplayText => $"{VoucherNo} [{VoucherType}] — {VoucherDate:dd-MMM-yyyy} (Rs. {Amount:N0})";
    }

    public class VoucherReportGroupDto
    {
        public int VoucherId { get; set; }
        public string VoucherNo { get; set; } = string.Empty;
        public DateTime VoucherDate { get; set; }
        public string VoucherDateFormatted => VoucherDate.ToString("dd-MMM-yyyy");
        public string VoucherType { get; set; } = "Voucher";
        public decimal VoucherAmount { get; set; }
        public string? AmountInWords { get; set; }
        public string? VoucherNarration { get; set; }
        public string? ApprovalStatus { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? CreatedOn { get; set; }
        public List<VoucherReportItemDto> Lines { get; set; } = new();
    }

    public class VoucherReportItemDto
    {
        public int VoucherId { get; set; }
        public string VoucherNo { get; set; } = string.Empty;
        public DateTime VoucherDate { get; set; }
        public string VoucherDateFormatted => VoucherDate.ToString("dd-MMM-yyyy");
        public string VoucherType { get; set; } = "Voucher";
        public decimal VoucherAmount { get; set; }
        public string? AmountInWords { get; set; }
        public string? VoucherNarration { get; set; }
        public string? ApprovalStatus { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? CreatedOn { get; set; }

        public int? AccountTransactionId { get; set; }
        public int? ChartofAccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string Narration { get; set; } = string.Empty;
        public string? ChequeNo { get; set; }
        public DateTime? ChequeDate { get; set; }
        public string? ChequeDateFormatted => ChequeDate.HasValue ? ChequeDate.Value.ToString("dd-MMM-yyyy") : null;
        public string? InvoiceNo { get; set; }
        public bool IsFilteredAccount { get; set; }
    }
}
