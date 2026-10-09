using System.ComponentModel.DataAnnotations;

namespace ERP_Core.Dtos
{
    public class AdminVerifyDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; } = string.Empty;
    }

    public class AdminWorkspaceDto
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a company")]
        public int CompanyId { get; set; }

        [Required(ErrorMessage = "Please select a financial year")]
        public int FinancialYearId { get; set; }

        public bool RememberMe { get; set; }
        public string? ReturnUrl { get; set; }
    }

    public class EmployeeLoginDto
    {
        [Required(ErrorMessage = "Please select an employee")]
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
        public string? ReturnUrl { get; set; }
    }

    public class EmployeeItemDto
    {
        public string Id { get; set; } = string.Empty;
        public int EmpId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Designation { get; set; }
        public string DisplayText => string.IsNullOrEmpty(Designation) ? Name : $"{Name} — {Designation}";
    }

    public class CompanyItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class FinancialYearItemDto
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string DisplayText => $"{Description} · {StartDate:dd-MM-yyyy} to {EndDate:dd-MM-yyyy}";
    }

    public class LoginViewModel
    {
        public List<EmployeeItemDto> Employees { get; set; } = new();
        public List<CompanyItemDto> Companies { get; set; } = new();
        public List<FinancialYearItemDto> FinancialYears { get; set; } = new();
        public string? ReturnUrl { get; set; }
    }
}
