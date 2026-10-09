namespace ERP_Core.Models
{
    public class RoleModulesPermission
    {
        public int Id { get; set; }
        public int RoleId { get; set; }
        public int ModuleId { get; set; }
        public Role? Role { get; set; }
        public Modules? Module { get; set; }
    }
}
