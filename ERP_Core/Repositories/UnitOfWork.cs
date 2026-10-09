using System.Data;
using System.Data.Common;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Dapper;
using ERP_Core.Data;
using ERP_Core.DataAccessLayer;
using ERP_Core.Models;
using ERP_Core.Repositories.IRepositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ERP_Core.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<UnitOfWork> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private bool _disposed = false;

        public IGenericRepository<ApplicationUser> ApplicationUsers { get; }
        public IGenericRepository<Company> Companies { get; }
        public IGenericRepository<Role> Roles { get; }
        public IGenericRepository<Department> Departments { get; }
        public IGenericRepository<Designation> Designations { get; }
        public IGenericRepository<NatureofAccount> NatureofAccounts { get; }
        public IGenericRepository<ChartOfAccount> ChartOfAccounts { get; }
        public IGenericRepository<Fish> Fishes { get; }
        public IGenericRepository<ProductCategory> ProductCategories { get; }
        public IGenericRepository<BillMaster> BillMasters { get; }
        public IGenericRepository<BillDetail> BillDetails { get; }
        public IGenericRepository<Voucher> Vouchers { get; }
        public IGenericRepository<AccountTransaction> AccountTransactions { get; }
        public FinancialYearRepository FinancialYearRepo { get; }

        public UnitOfWork(
            ERPDbContext context,
            ILogger<UnitOfWork> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;

            ApplicationUsers = new GenericRepository<ApplicationUser>(context);
            Companies = new GenericRepository<Company>(context);
            Roles = new GenericRepository<Role>(context);
            Departments = new GenericRepository<Department>(context);
            Designations = new GenericRepository<Designation>(context);
            NatureofAccounts = new GenericRepository<NatureofAccount>(context);
            ChartOfAccounts = new GenericRepository<ChartOfAccount>(context);
            Fishes = new GenericRepository<Fish>(context);
            ProductCategories = new GenericRepository<ProductCategory>(context);
            BillMasters = new GenericRepository<BillMaster>(context);
            BillDetails = new GenericRepository<BillDetail>(context);
            Vouchers = new GenericRepository<Voucher>(context);
            AccountTransactions = new GenericRepository<AccountTransaction>(context);
            FinancialYearRepo = new FinancialYearRepository(context);
        }

        public DbConnection CreateConnection()
        {
            var connectionString = _context.Database.GetConnectionString();
            return new SqlConnection(connectionString);
        }

        public async Task<string> GenerateVoucherNoAsync(VoucherType voucherType, int fyId, int companyId)
        {
            return await GenerateVoucherNoAsync(voucherType.ToString(), fyId, companyId);
        }

        public async Task<string> GenerateVoucherNoAsync(string voucherType, int fyId, int companyId)
        {
            string vType = (voucherType ?? "JV").Trim().ToUpperInvariant();
            string prefix = vType switch
            {
                "JV" => "JV-",
                "CP" or "CPV" => "CPV-",
                "CR" or "CRV" => "CRV-",
                "BP" or "BPV" => "BPV-",
                "BR" or "BRV" => "BRV-",
                _ => vType.EndsWith("-") ? vType : $"{vType}-"
            };

            string fyCode = await GetFinancialYearStartDateAsync(fyId, companyId);
            if (string.IsNullOrEmpty(fyCode))
            {
                fyCode = DateTime.Now.ToString("yy");
            }

            string companyCode = await GetCompanyCode(companyId);
            if (string.IsNullOrEmpty(companyCode))
            {
                companyCode = $"C{companyId:D2}";
            }

            string basePrefix = $"{prefix}{companyCode}-{fyCode}-";
            string defaultVoucherNo = $"{basePrefix}0001";

            try
            {
                // 1. Query highest voucher no in Voucher table matching the prefix
                var maxVoucherNo = await _context.Vouchers
                    .Where(v => (v.CompanyId == null || v.CompanyId == companyId) && v.VoucherNo != null && v.VoucherNo.StartsWith(basePrefix))
                    .OrderByDescending(v => v.VoucherNo)
                    .Select(v => v.VoucherNo)
                    .FirstOrDefaultAsync();

                // 2. If not found in Voucher table, check AccountTransaction table
                if (string.IsNullOrEmpty(maxVoucherNo))
                {
                    maxVoucherNo = await _context.AccountTransactions
                        .Where(t => (t.CompanyId == null || t.CompanyId == companyId) && t.VoucherNo != null && t.VoucherNo.StartsWith(basePrefix))
                        .OrderByDescending(t => t.VoucherNo)
                        .Select(t => t.VoucherNo)
                        .FirstOrDefaultAsync();
                }

                if (!string.IsNullOrEmpty(maxVoucherNo))
                {
                    string nextCode = GetNextCode(maxVoucherNo);
                    return string.IsNullOrEmpty(nextCode) ? defaultVoucherNo : nextCode;
                }

                return defaultVoucherNo;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error generating voucher number for prefix {BasePrefix}", basePrefix);
                return defaultVoucherNo;
            }
        }

        public async Task<string> InitializeGenerateCode(int fyId, int companyId, string inputcode, PrefixType prefixType, bool isInitialcode)
        {
            string prefix = prefixType switch
            {
                PrefixType.D => "DES-",
                PrefixType.DEP => "DEP-",
                PrefixType.ROL => "ROL-",
                PrefixType.CS => "COMP-",
                PrefixType.JV => "JV-",
                PrefixType.CP or PrefixType.CPV => "CPV-",
                PrefixType.CR or PrefixType.CRV => "CRV-",
                PrefixType.BP or PrefixType.BPV => "BPV-",
                PrefixType.BR or PrefixType.BRV => "BRV-",
                _ => $"{prefixType}-"
            };

            string fyCode = await GetFinancialYearStartDateAsync(fyId, companyId);
            string companyCode = await GetCompanyCode(companyId);
            string newPrefix = $"{prefix}{companyCode}-{fyCode}-0001";

            inputcode = string.IsNullOrEmpty(inputcode) ? newPrefix : inputcode;
            string maxCode = string.Empty;

            try
            {
                maxCode = prefixType switch
                {
                    PrefixType.D => await GetMaxCodeWithoutTenantAsync("CDesignations", "Code", "Id"),
                    PrefixType.DEP => await GetMaxCodeWithoutTenantAsync("CDepartments", "Code", "Id"),
                    PrefixType.ROL => await GetMaxCodeWithoutTenantAsync("CRoles", "Code", "Id"),
                    PrefixType.JV or PrefixType.CP or PrefixType.CR or PrefixType.BP or PrefixType.BR or PrefixType.CPV or PrefixType.CRV or PrefixType.BPV or PrefixType.BRV =>
                        await _context.Vouchers.Where(v => v.CompanyId == companyId && v.VoucherNo.StartsWith(prefix)).OrderByDescending(v => v.VoucherNo).Select(v => v.VoucherNo).FirstOrDefaultAsync() ?? "",
                    _ => ""
                };

                if (!string.IsNullOrEmpty(maxCode) && maxCode.StartsWith(prefix))
                {
                    string newCode = GetNextCode(maxCode);
                    if (isInitialcode)
                    {
                        return string.IsNullOrEmpty(newCode) ? newPrefix : newCode;
                    }
                    if (newCode != inputcode)
                    {
                        newCode = GetNextCode(inputcode, true);
                    }
                    return string.IsNullOrEmpty(newCode) ? newPrefix : newCode;
                }

                return newPrefix;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error generating code for prefix {PrefixType}", prefixType);
                return newPrefix;
            }
        }

        private static string GetNextCode(string inputCode, bool isOnlyNumCheck = false)
        {
            var match = Regex.Match(inputCode, @"^(.+?)(\d+)?$");
            if (match.Success)
            {
                string prefix = match.Groups[1].Value;
                string numberPart = match.Groups[2].Value;

                if (string.IsNullOrEmpty(numberPart))
                {
                    return $"{prefix}0001";
                }

                if (isOnlyNumCheck)
                {
                    return inputCode;
                }

                if (long.TryParse(numberPart, out long number))
                {
                    number++;
                    string newNumber = number.ToString(new string('0', numberPart.Length));
                    return $"{prefix}{newNumber}";
                }
            }

            return string.Empty;
        }

        public async Task<string> GetMaxCodeWithoutTenantAsync(string tableName, string columnName, string idColumnName)
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                var procedure = "spGetMaxCodeWithoutFYIdAndCompanyId";
                var parameters = new
                {
                    TableName = tableName,
                    ColumnName = columnName,
                    IdColumnName = idColumnName
                };

                return await connection.QueryFirstOrDefaultAsync<string>(
                    procedure,
                    parameters,
                    commandType: CommandType.StoredProcedure) ?? "";
            }
            catch
            {
                return "";
            }
        }

        public async Task<string> GetFinancialYearStartDateAsync(int fyId, int companyId)
        {
            var fy = await FinancialYearRepo.GetByIdAsync(fyId);
            return fy?.StartDate.ToString("yy") ?? "";
        }

        private async Task<string> GetCompanyCode(int companyId)
        {
            var company = await Companies.GetByIdAsync(companyId);
            return company?.Code ?? "";
        }

        public async Task<int> SaveAsync()
        {
            var modifiedEntries = _context.ChangeTracker
                .Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
                .Select(e => new
                {
                    Entry = e,
                    EntityType = e.Entity.GetType().Name,
                    EntityIdProp = e.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey()),
                    State = e.State
                })
                .ToList();

            var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "System";
            int.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirst("CompanyId")?.Value, out int companyId);
            int.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirst("FinancialYearId")?.Value, out int fyId);

            var result = await _context.SaveChangesAsync();

            foreach (var item in modifiedEntries)
            {
                var entityId = item.EntityIdProp?.CurrentValue?.ToString() ?? "N/A";
                var actionType = item.State switch
                {
                    EntityState.Added => "Added",
                    EntityState.Modified => "Modified",
                    EntityState.Deleted => "Deleted",
                    _ => "Unknown"
                };

                _logger.LogInformation("{Action} on {EntityName} (ID: {EntityId}) by User: {UserId}, Company: {CompanyId}, FY: {FyId}",
                    actionType, item.EntityType, entityId, userId, companyId, fyId);
            }

            return result;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _context?.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
