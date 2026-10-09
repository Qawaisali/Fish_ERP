using System.Security.Claims;

namespace ERP_Core.Middleware
{
    /// <summary>
    /// Ensures that authenticated users always have their essential ERP workspace context
    /// (CompanyId, FinancialYearId, EmpId, RoleId) populated in HttpContext.Session,
    /// even after application restarts or session expiration.
    /// </summary>
    public class WorkspaceContextMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<WorkspaceContextMiddleware> _logger;

        public WorkspaceContextMiddleware(RequestDelegate next, ILogger<WorkspaceContextMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

                // Skip static files, logout, and login endpoints
                if (!path.StartsWith("/login") && !path.StartsWith("/error") && !path.StartsWith("/lib") && !path.StartsWith("/css") && !path.StartsWith("/js") && !path.StartsWith("/images"))
                {
                    var session = context.Session;

                    // 1. Sync CompanyId
                    int? sessionCompanyId = session.GetInt32("CompanyId");
                    if (!sessionCompanyId.HasValue || sessionCompanyId.Value <= 0)
                    {
                        var claimCompanyId = context.User.FindFirst("CompanyId")?.Value;
                        if (int.TryParse(claimCompanyId, out int parsedCompanyId) && parsedCompanyId > 0)
                        {
                            session.SetInt32("CompanyId", parsedCompanyId);
                            sessionCompanyId = parsedCompanyId;
                        }
                    }

                    // 2. Sync FinancialYearId
                    int? sessionFyId = session.GetInt32("FinancialYearId");
                    if (!sessionFyId.HasValue || sessionFyId.Value <= 0)
                    {
                        var claimFyId = context.User.FindFirst("FinancialYearId")?.Value;
                        if (int.TryParse(claimFyId, out int parsedFyId) && parsedFyId > 0)
                        {
                            session.SetInt32("FinancialYearId", parsedFyId);
                            sessionFyId = parsedFyId;
                        }
                    }

                    // 3. Sync EmpId / UserId
                    string? sessionEmpId = session.GetString("EmpId");
                    if (string.IsNullOrEmpty(sessionEmpId))
                    {
                        var claimEmpId = context.User.FindFirst("EmpId")?.Value
                                      ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        if (!string.IsNullOrEmpty(claimEmpId))
                        {
                            session.SetString("EmpId", claimEmpId);
                            session.SetString("UserId", claimEmpId);
                            sessionEmpId = claimEmpId;
                        }
                    }

                    // 4. Sync RoleId
                    int? sessionRoleId = session.GetInt32("RoleId");
                    if (!sessionRoleId.HasValue || sessionRoleId.Value <= 0)
                    {
                        var claimRoleId = context.User.FindFirst("RoleId")?.Value;
                        if (int.TryParse(claimRoleId, out int parsedRoleId) && parsedRoleId > 0)
                        {
                            session.SetInt32("RoleId", parsedRoleId);
                        }
                    }
                }
            }

            await _next(context);
        }
    }
}
