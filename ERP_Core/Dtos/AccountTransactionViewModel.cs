using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ERP_Core.Dtos
{
    public class AccountSummaryItemDto
    {
        public int ChartOfAccountId { get; set; }
        public int? VendorId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string? AccountCode { get; set; }
        public string City { get; set; } = "-";
        public string Phone { get; set; } = "-";
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal NetBalance { get; set; }
        public int VoucherCount { get; set; }
        public DateTime? LastTxDate { get; set; }
        public string LastTxFormatted => LastTxDate?.ToString("dd-MMM-yyyy") ?? "-";
        public string? LatestVoucherNo { get; set; }
    }

    public class AccountTransactionViewModel
    {
        public string SelectedType { get; set; } = "All";
        public string FromDate { get; set; } = string.Empty;
        public string ToDate { get; set; } = string.Empty;
        public string? SearchQuery { get; set; }
        public int? SelectedAccountId { get; set; }

        public int TotalAccounts { get; set; }
        public int TotalVouchers { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal NetAmount { get; set; }

        public string CompanyName { get; set; } = "Zahid Hussain & Sons";
        public string CompanyAddress { get; set; } = "Fish Commission Agents · Sukkur Fish Harbour";
        public string CompanyPhone { get; set; } = "0300-3110890 | 0334-3942837";

        public List<AccountSummaryItemDto> AccountsSummary { get; set; } = new();
        public List<SelectListItem> AvailableTypes { get; set; } = new();
        public List<SelectListItem> AvailableAccounts { get; set; } = new();
    }
}
