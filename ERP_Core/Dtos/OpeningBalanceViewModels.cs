using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ERP_Core.Dtos
{
    // =========================================================================
    // OPENING BALANCE VIEW MODEL & DTOS
    // =========================================================================
    public class OpeningBalanceViewModel
    {
        public int? Id { get; set; }

        [Display(Name = "OB Code")]
        public string OBCode { get; set; } = string.Empty;

        [Display(Name = "OB Date")]
        public DateTime OBDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Please select a Financial Year")]
        [Display(Name = "Financial Year")]
        public int FinancialYearId { get; set; }

        public int CompanyId { get; set; }

        public string FiscalPeriodText { get; set; } = string.Empty;

        // Dynamic Line Items
        public List<OpeningBalanceDetailDto> Details { get; set; } = new();

        // Dropdowns data
        public List<FinancialYearDropdownDto> AvailableFinancialYears { get; set; } = new();
        public List<CoaLevel3DropdownDto> AvailableCoaAccounts { get; set; } = new();

        // Saved Vouchers History
        public List<OpeningBalanceSummaryDto> RecentVouchers { get; set; } = new();

        // Summary Stats
        public int TotalVouchers { get; set; }
        public decimal TotalDebitSum { get; set; }
        public decimal TotalCreditSum { get; set; }
    }

    public class OpeningBalanceDetailDto
    {
        public int? Id { get; set; }

        public int COAId { get; set; }

        public string? COACode { get; set; }
        public string? COAName { get; set; }
        public string? ParentAccountName { get; set; }

        public string OBDtlTranType { get; set; } = "DEB"; // "DEB" or "CRE"

        public decimal? OBDtlOpenBlnc { get; set; }
    }

    public class FinancialYearDropdownDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool CurrentYear { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string PeriodText => $"{StartDate:dd MMM yyyy} to {EndDate:dd MMM yyyy}";
        public string DisplayText => string.IsNullOrWhiteSpace(Description)
            ? Code
            : $"{Code} · {Description}" + (CurrentYear ? " (Active Current)" : "");
    }

    public class CoaLevel3DropdownDto
    {
        public int Id { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public int? ParentAccountId { get; set; }
        public string? ParentAccountName { get; set; }
        public string? ParentAccountCode { get; set; }
        public string DisplayText => string.IsNullOrWhiteSpace(AccountCode)
            ? AccountName
            : $"{AccountCode} — {AccountName}" + (!string.IsNullOrEmpty(ParentAccountName) ? $" [{ParentAccountName}]" : "");
    }

    public class OpeningBalanceSummaryDto
    {
        public int Id { get; set; }
        public string OBCode { get; set; } = string.Empty;
        public DateTime OBDate { get; set; }
        public int FinancialYearId { get; set; }
        public string FinancialYearCode { get; set; } = string.Empty;
        public string FiscalPeriod { get; set; } = string.Empty;
        public int TotalEntries { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal Difference => Math.Abs(TotalDebit - TotalCredit);
        public bool IsBalanced => TotalDebit == TotalCredit;
        public bool? Active { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
    }
}
