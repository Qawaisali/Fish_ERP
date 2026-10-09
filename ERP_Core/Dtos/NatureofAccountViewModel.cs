using System.Collections.Generic;
using ERP_Core.Models;

namespace ERP_Core.Dtos
{
    public class NatureofAccountViewModel
    {
        public List<NatureofAccount> Accounts { get; set; } = new();
        public NatureofAccount Form { get; set; } = new();

        public int TotalCount { get; set; }
        public int DebtorsCount { get; set; }
        public int CreditorsCount { get; set; }
        public int UnspecifiedCount { get; set; }

        public string? SearchTerm { get; set; }
        public string? SelectedType { get; set; }
    }
}
