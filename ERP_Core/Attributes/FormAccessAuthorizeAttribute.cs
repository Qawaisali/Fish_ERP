using ERP_Core.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace ERP_Core.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class FormAccessAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _formName;

        public FormAccessAuthorizeAttribute(string formName)
        {
            _formName = formName;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var httpContext = context.HttpContext;
            var user = httpContext.User;

            if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
            {
                HandleUnauthorized(context);
                return;
            }

            // 👑 1. Admins have global access across all forms/modules
            if (user.IsInRole("Admin") || user.HasClaim(ClaimTypes.Role, "Admin") || user.HasClaim(ClaimTypes.Role, "Owner"))
            {
                return;
            }

            // 2. Any logged-in user can access the main Dashboard
            if (string.Equals(_formName, "Dashboard", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var dbContext = httpContext.RequestServices.GetService<ERPDbContext>();
            var cache = httpContext.RequestServices.GetService<IMemoryCache>();

            if (dbContext == null)
            {
                context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
                return;
            }

            // Retrieve User Identifiers from Claims or Session
            var empIdClaim = user.FindFirstValue("EmpId") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
            var identityIdClaim = user.FindFirstValue("IdentityId");
            var roleIdClaim = user.FindFirstValue("RoleId") ?? httpContext.Session.GetInt32("RoleId")?.ToString();

            int.TryParse(empIdClaim, out int empId);
            int.TryParse(roleIdClaim, out int roleId);

            try
            {
                // If RoleId was not found in claims, look up from the User table
                if (roleId <= 0)
                {
                    var dbUser = await dbContext.CApplicationUsers
                        .Where(u => (empId > 0 && u.EmpId == empId) || (!string.IsNullOrEmpty(identityIdClaim) && u.Id == identityIdClaim))
                        .Select(u => new { u.RoleId })
                        .FirstOrDefaultAsync();

                    if (dbUser != null)
                    {
                        roleId = dbUser.RoleId;
                        httpContext.Session.SetInt32("RoleId", roleId);
                    }
                }

                // If user has no valid role assigned
                if (roleId <= 0)
                {
                    HandleForbidden(context);
                    return;
                }

                // Fetch permitted module names & URLs with MemoryCache (5 min cache)
                string cacheKey = $"RolePerms_{roleId}";
                List<PermittedModuleEntry>? permittedModules = null;

                if (cache != null && !cache.TryGetValue(cacheKey, out permittedModules))
                {
                    permittedModules = await (
                        from rf in dbContext.CRoleModulesPermissions
                        join f in dbContext.CModules on rf.ModuleId equals f.Id
                        where rf.RoleId == roleId && f.Active != false
                        select new PermittedModuleEntry
                        {
                            Name = f.Name ?? "",
                            Url = f.Url ?? ""
                        }
                    ).ToListAsync();

                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(5));

                    cache.Set(cacheKey, permittedModules, cacheEntryOptions);
                }
                else if (cache == null)
                {
                    permittedModules = await (
                        from rf in dbContext.CRoleModulesPermissions
                        join f in dbContext.CModules on rf.ModuleId equals f.Id
                        where rf.RoleId == roleId && f.Active != false
                        select new PermittedModuleEntry
                        {
                            Name = f.Name ?? "",
                            Url = f.Url ?? ""
                        }
                    ).ToListAsync();
                }

                if (permittedModules == null)
                {
                    permittedModules = new List<PermittedModuleEntry>();
                }

                var normalizedFormName = _formName.Trim().TrimStart('/');

                // Check permission in memory
                bool hasPermission = permittedModules.Any(m =>
                {
                    var nameMatch = string.Equals(m.Name.Trim(), normalizedFormName, StringComparison.OrdinalIgnoreCase);
                    var cleanUrl = (m.Url ?? "").Trim().TrimStart('/');
                    var urlMatch = string.Equals(cleanUrl, normalizedFormName, StringComparison.OrdinalIgnoreCase);
                    return nameMatch || urlMatch;
                });

                if (!hasPermission)
                {
                    HandleForbidden(context);
                }
            }
            catch
            {
                HandleForbidden(context);
            }
        }

        private static void HandleUnauthorized(AuthorizationFilterContext context)
        {
            if (IsAjaxRequest(context.HttpContext.Request))
            {
                context.Result = new JsonResult(new { success = false, message = "Session expired. Please log in." })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }
            else
            {
                context.Result = new RedirectToActionResult("Index", "Login", null);
            }
        }

        private static void HandleForbidden(AuthorizationFilterContext context)
        {
            if (IsAjaxRequest(context.HttpContext.Request))
            {
                context.Result = new JsonResult(new { success = false, message = "Access Denied. You do not have permission for this module." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
            else
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Home", null);
            }
        }

        private static bool IsAjaxRequest(HttpRequest request)
        {
            return string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
                || (request.Headers["Accept"].ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase));
        }

        private class PermittedModuleEntry
        {
            public string Name { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
        }
    }
}
