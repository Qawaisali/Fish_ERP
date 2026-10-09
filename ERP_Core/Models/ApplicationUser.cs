using Microsoft.AspNetCore.Identity;

namespace ERP_Core.Models
{
    public class ApplicationUser : IdentityUser
    {
        public int EmpId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public int RoleId { get; set; }
        public bool Active { get; set; }
        public int DesignationId { get; set; }
        public int DepartmenId { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public string? ModifiedBy { get; set; }
    }
}
