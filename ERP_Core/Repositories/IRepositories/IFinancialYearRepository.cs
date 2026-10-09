using ERP_Core.Models;

namespace ERP_Core.Repositories.IRepositories
{
    public interface IFinancialYearRepository
    {
        Task<IEnumerable<FinancialYear>> GetAllFinancialYears();
        Task<FinancialYear?> GetByIdAsync(int id);
    }
}
