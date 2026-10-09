using System;
using System.Collections.Generic;
using ERP_Core.Models;

namespace ERP_Core.Dtos
{
    public class TeamRoleItemDto
    {
        public int Id { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public bool Active { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
        public int EmployeeCount { get; set; }
        public int ModuleCount { get; set; }
        public List<string> EmployeeInitials { get; set; } = new();
        public List<string> EmployeeNames { get; set; } = new();
    }

    public class TeamAccessIndexViewModel
    {
        public List<TeamRoleItemDto> Roles { get; set; } = new();
        public List<Modules> AvailableParentModules { get; set; } = new();
        public int TotalRoles { get; set; }
        public int TotalEmployees { get; set; }
        public int TotalModules { get; set; }
        public int ActiveRolesCount { get; set; }
    }

    public class EmployeeRoleDto
    {
        public string Id { get; set; } = string.Empty;
        public int EmpId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string DesignationName { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public bool Active { get; set; }
        public DateTime? CreatedOn { get; set; }
    }

    public class ModulePermissionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int Order { get; set; }
        public int isParentModule { get; set; }
        public int isSubModule { get; set; }
        public int ParentModuleId { get; set; }
        public string ParentModuleName { get; set; } = string.Empty;
        public bool IsGranted { get; set; }
    }

    public class ParentModuleGroupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsGranted { get; set; }
        public List<ModulePermissionDto> SubModules { get; set; } = new();
    }

    public class RoleDetailsViewModel
    {
        public TeamRoleItemDto Role { get; set; } = new();
        public List<EmployeeRoleDto> Employees { get; set; } = new();
        public List<ParentModuleGroupDto> ModuleGroups { get; set; } = new();
        public List<EmployeeRoleDto> AvailableEmployees { get; set; } = new();
        public List<Modules> AvailableParentModules { get; set; } = new();
        public int TotalSystemModules { get; set; }
        public int GrantedModulesCount { get; set; }
    }
}
