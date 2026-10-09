using ERP_Core.Data;
using ERP_Core.DataAccessLayer;
using ERP_Core.Middleware;
using ERP_Core.Models;
using ERP_Core.Repositories;
using ERP_Core.Repositories.IRepositories;
using ERP_Core.Services;
using ERP_Core.Services.IServices;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;
using System.Collections.ObjectModel;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

#region Data Protection Configuration

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(@"C:\keys"))
    .SetApplicationName("Webdesk_Core_PM");

#endregion

#region Serilog Logging Configuration

// File logger for general logs
var logger = new LoggerConfiguration()
    .WriteTo.File("Logs/log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

logger.Information("Application startup");

// SQL column configuration
var columnOptions = new ColumnOptions
    {
        AdditionalColumns = new Collection<SqlColumn>
    {
        new SqlColumn("UserId", SqlDbType.NVarChar, allowNull: true),
        new SqlColumn("EntityName", SqlDbType.NVarChar, allowNull: true),
        new SqlColumn("ActionType", SqlDbType.NVarChar, allowNull: true),
        new SqlColumn("CompanyId", SqlDbType.Int, allowNull: true),
        new SqlColumn("FyId", SqlDbType.Int, allowNull: true)
    }
    };

// Serilog full configuration
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentUserName()
    .Enrich.WithThreadId()
    .WriteTo.Console()
    .WriteTo.Logger(lc => lc
        .Filter.ByExcluding(logEvent =>
            logEvent.Properties.TryGetValue("SourceContext", out var context) &&
            context.ToString().Contains("Microsoft.EntityFrameworkCore")
        )
        .Filter.ByIncludingOnly(logEvent =>
            logEvent.Properties.ContainsKey("UserId") &&
            logEvent.Properties.ContainsKey("EntityName") &&
            logEvent.Properties.ContainsKey("ActionType") &&
            logEvent.Properties.ContainsKey("CompanyId") &&
            logEvent.Properties.ContainsKey("FyId")
        )
        .WriteTo.MSSqlServer(
            connectionString: builder.Configuration.GetConnectionString("DefaultConnection") ?? "",
            sinkOptions: new MSSqlServerSinkOptions
            {
                TableName = "Logs",
                AutoCreateSqlTable = true
            },
            columnOptions: columnOptions
        )
    )
    .CreateLogger();

builder.Host.UseSerilog();

#endregion

#region  Middleware & MVC Configuration

builder.Services.AddControllersWithViews();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.ValueCountLimit = 10000;
    options.KeyLengthLimit = 2048;
    options.ValueLengthLimit = 1024 * 1024;
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.MaxAge = TimeSpan.FromHours(8);
});

#endregion

#region  Authentication & Authorization

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "CustomScheme";
})
.AddCookie("CustomScheme", options =>
{
    options.LoginPath = "/login/index";
    options.AccessDeniedPath = "/Home/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Name = "MyAuthCookie";
});

builder.Services.AddAuthorization();

#endregion

#region Database Configuration

builder.Services.AddDbContext<ERPDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.UseCompatibilityLevel(120)));

#endregion

#region  Identity Configuration

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Password.RequiredUniqueChars = 1;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ERPDbContext>()
.AddDefaultTokenProviders();

#endregion

#region  Dependency Injection: Core Services & Repositories

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<UserManager<ApplicationUser>>();
builder.Services.AddScoped<FormPermissionService>();
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<LoginService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IVendorService, VendorService>();
builder.Services.AddScoped<ModulesDAL>();
builder.Services.AddScoped<CompanyDAL>();

#endregion

#region App Pipeline Configuration

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
else
{
    app.UseDeveloperExceptionPage();
}
app.UseStatusCodePagesWithReExecute("/Home/HandleError/{0}");
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<WorkspaceContextMiddleware>();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

#endregion


app.Run();
