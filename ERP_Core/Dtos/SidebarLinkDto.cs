namespace ERP_Core.Dtos
{
    public class SidebarLinkDto
    {
        public int? Id { get; set; }
        public int EmpId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int ParentModuleId { get; set; }
        public int isParentModule { get; set; }
        public int isSUbModule { get; set; }
        public int isSubModule
        {
            get => isSUbModule;
            set => isSUbModule = value;
        }
        public int Order { get; set; }
        public List<SidebarLinkDto> SubModules { get; set; } = new();
    }
}
