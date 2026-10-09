using System.Collections.Generic;
using ERP_Core.Models;

namespace ERP_Core.Dtos
{
    public class AccountSetupViewModel
    {
        public AccountSetup Form { get; set; } = new();
        public int SelectedCompanyId { get; set; }
        public List<Company> AvailableCompanies { get; set; } = new();
        public List<ChartOfAccountOptionDto> AvailableAccounts { get; set; } = new();
        public List<AccountSetupSummaryDto> ConfiguredSetups { get; set; } = new();
    }

    public class ChartOfAccountOptionDto
    {
        public int Id { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string Nature { get; set; } = string.Empty;
        public bool Transable { get; set; } = true;

        public string DisplayText
        {
            get
            {
                var codePrefix = !string.IsNullOrWhiteSpace(AccountCode) ? $"{AccountCode} — " : "";
                var natureSuffix = !string.IsNullOrWhiteSpace(Nature) ? $" ({Nature})" : "";
                return $"{codePrefix}{AccountName}{natureSuffix}";
            }
        }
    }

    public class AccountSetupSummaryDto
    {
        public int AccountSetupId { get; set; }
        public int? CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string SupplierAccountName { get; set; } = string.Empty;
        public string CustomerAccountName { get; set; } = string.Empty;
        public string VatInputAccountName { get; set; } = string.Empty;
        public string VatOutputAccountName { get; set; } = string.Empty;
        public string BankChargesAccountName { get; set; } = string.Empty;
    }
}
