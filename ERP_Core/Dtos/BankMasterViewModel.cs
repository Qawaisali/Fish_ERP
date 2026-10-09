using System.Collections.Generic;
using ERP_Core.Models;

namespace ERP_Core.Dtos
{
    public class BankMasterListDto
    {
        public int Id { get; set; }
        public string BankName { get; set; } = string.Empty;
        public int ParentBankId { get; set; } = 0;
        public string ParentBankName { get; set; } = "None";
        public string AccountName { get; set; } = string.Empty;
        public string AccountCode { get; set; } = string.Empty;
        public string AccountNo { get; set; } = string.Empty;
        public string IbanNo { get; set; } = string.Empty;
        public string SwiftCode { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        public bool IsBank { get; set; } = true;
        public bool IsCash { get; set; } = false;
        public int? ChartOfAccountId { get; set; }
    }

    public class BankTreeNodeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string AccountNo { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string Type { get; set; } = "Bank";
        public bool Active { get; set; } = true;
        public List<BankTreeNodeDto> Children { get; set; } = new();
    }

    public class CoaAccountDropdownDto
    {
        public int Id { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string DisplayText => string.IsNullOrWhiteSpace(AccountCode) ? AccountName : $"{AccountCode} - {AccountName}";
    }

    public class ParentBankDropdownDto
    {
        public int Id { get; set; }
        public string BankName { get; set; } = string.Empty;
    }

    public class BankMasterViewModel
    {
        public List<BankMasterListDto> Banks { get; set; } = new();
        public List<BankTreeNodeDto> TreeNodes { get; set; } = new();
        public List<CoaAccountDropdownDto> AvailableAccounts { get; set; } = new();
        public List<ParentBankDropdownDto> AvailableParentBanks { get; set; } = new();
        public BankMaster Form { get; set; } = new();
    }
}
