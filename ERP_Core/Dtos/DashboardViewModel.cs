namespace ERP_Core.Dtos
{
    public class DashboardViewModel
    {
        public string UserName { get; set; } = "Employee";
        public bool IsAdmin { get; set; }
        public int EmpId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string FinancialYearName { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public int FinancialYearId { get; set; }

        public int TotalUsersCount { get; set; }
        public int TotalRolesCount { get; set; }
        public int TotalModulesCount { get; set; }
        public int TotalCompaniesCount { get; set; }

        public List<SidebarLinkDto> AccessibleModules { get; set; } = new();
        public string CurrentMonthShort { get; set; } = DateTime.Now.ToString("MMM");
    }
}
