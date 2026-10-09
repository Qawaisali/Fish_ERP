namespace ERP_Core.Models
{
    public class Department : BaseEntity
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int? CompanyId { get; set; }
    }
}
