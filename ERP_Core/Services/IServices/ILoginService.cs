using ERP_Core.Dtos;
using ERP_Core.Models;

namespace ERP_Core.Services.IServices
{
    public interface ILoginService
    {
        Task<List<EmployeeItemDto>> GetEmployeesAsync();
        Task<List<CompanyItemDto>> GetCompaniesAsync();
        Task<List<FinancialYearItemDto>> GetFinancialYearsByCompanyIdAsync(int companyId);
        Task<List<FinancialYearItemDto>> GetAllFinancialYearsAsync();
        Task<ApplicationUser?> VerifyAdminCredentialsAsync(string email, string password);
        Task<ApplicationUser?> VerifyEmployeeCredentialsAsync(string userId, string password);
    }
}
