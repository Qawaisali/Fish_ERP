using ERP_Core.Data;
using ERP_Core.Models;
using ERP_Core.Repositories.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace ERP_Core.Repositories
{
    public class FinancialYearRepository : IFinancialYearRepository
    {
        protected readonly ERPDbContext _context;

        public FinancialYearRepository(ERPDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<FinancialYear>> GetAllFinancialYears()
        {
            return await _context.CFinancialYears.ToListAsync();
        }

        public async Task<FinancialYear?> GetByIdAsync(int id)
        {
            return await _context.CFinancialYears.FirstOrDefaultAsync(fy => fy.Id == id);
        }
    }
}
