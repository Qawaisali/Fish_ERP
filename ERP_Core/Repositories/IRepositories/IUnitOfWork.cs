using System.Data.Common;
using ERP_Core.DataAccessLayer;
using ERP_Core.Models;

namespace ERP_Core.Repositories.IRepositories
{
    public interface IUnitOfWork : IDisposable
    {
        Task<string> InitializeGenerateCode(int fyId, int companyId, string inputcode, PrefixType prefixType, bool isInitialcode);
        DbConnection CreateConnection();

        IGenericRepository<ApplicationUser> ApplicationUsers { get; }
        IGenericRepository<Company> Companies { get; }
        IGenericRepository<Role> Roles { get; }
        IGenericRepository<Department> Departments { get; }
        IGenericRepository<Designation> Designations { get; }
        IGenericRepository<NatureofAccount> NatureofAccounts { get; }
        IGenericRepository<ChartOfAccount> ChartOfAccounts { get; }
        IGenericRepository<Fish> Fishes { get; }
        IGenericRepository<ProductCategory> ProductCategories { get; }
        IGenericRepository<BillMaster> BillMasters { get; }
        IGenericRepository<BillDetail> BillDetails { get; }
        IGenericRepository<Voucher> Vouchers { get; }
        IGenericRepository<AccountTransaction> AccountTransactions { get; }
        FinancialYearRepository FinancialYearRepo { get; }

        Task<string> GenerateVoucherNoAsync(VoucherType voucherType, int fyId, int companyId);
        Task<string> GenerateVoucherNoAsync(string voucherType, int fyId, int companyId);

        Task<int> SaveAsync();
    }
}
