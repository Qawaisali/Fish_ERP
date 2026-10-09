namespace ERP_Core.Models
{
    public class Modules : BaseEntity
    {
        public int Id { get; set; }
        public int isSubModule { get; set; }
        public int isParentModule { get; set; }
        public int ParentModuleId { get; set; }
        public int Order { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}
