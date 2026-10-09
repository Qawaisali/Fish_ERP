using System.ComponentModel.DataAnnotations;

namespace ERP_Core.Dtos
{
    public class RegisterUserDto
    {
        [Display(Name = "Employee ID")]
        public int? EmpId { get; set; }

        [Required(ErrorMessage = "Full Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        public string UserName { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Invalid phone number")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "Company is required")]
        public int CompanyId { get; set; }

        [Required(ErrorMessage = "Role is required")]
        public int RoleId { get; set; }

        public int DesignationId { get; set; } = 1;
        public int DepartmenId { get; set; } = 1;

        public bool Active { get; set; } = true;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm Password is required")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class RegisterViewModel
    {
        public RegisterUserDto Form { get; set; } = new();
        public List<CompanyItemDto> Companies { get; set; } = new();
        public List<RoleItemDto> Roles { get; set; } = new();
        public List<DesignationItemDto> Designations { get; set; } = new();
        public List<DepartmentItemDto> Departments { get; set; } = new();
    }

    public class RoleItemDto
    {
        public int Id { get; set; }
        public string RoleName { get; set; } = string.Empty;
    }

    public class DesignationItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class DepartmentItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
