using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using ERP_Core.Services.IServices;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ERP_Core.Services
{
    public class LoginService : ILoginService
    {
        private readonly ERPDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;

        public LoginService(ERPDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            _dbContext = dbContext;
            _userManager = userManager;
        }

        public async Task<List<EmployeeItemDto>> GetEmployeesAsync()
        {
            return await _dbContext.CApplicationUsers
                .Where(u => u.Active)
                .OrderBy(u => u.Name)
                .Select(u => new EmployeeItemDto
                {
                    Id = u.EmpId > 0 ? u.EmpId.ToString() : u.Id,
                    Name = u.Name ?? u.UserName ?? "User",
                    Designation = null
                })
                .ToListAsync();
        }

        public async Task<List<CompanyItemDto>> GetCompaniesAsync()
        {
            return await _dbContext.CCompanies
                .OrderBy(c => c.CompanyName)
                .Select(c => new CompanyItemDto
                {
                    Id = c.CompanyId,
                    Name = c.CompanyName ?? "Company"
                })
                .ToListAsync();
        }

        public async Task<List<FinancialYearItemDto>> GetFinancialYearsByCompanyIdAsync(int companyId)
        {
            return await _dbContext.CFinancialYears
                .Where(f => f.CompanyID == companyId)
                .OrderByDescending(f => f.StartDate)
                .Select(f => new FinancialYearItemDto
                {
                    Id = f.Id,
                    CompanyId = f.CompanyID,
                    Description = f.Description ?? "FY",
                    StartDate = f.StartDate,
                    EndDate = f.EndDate
                })
                .ToListAsync();
        }

        public async Task<List<FinancialYearItemDto>> GetAllFinancialYearsAsync()
        {
            return await _dbContext.CFinancialYears
                .OrderByDescending(f => f.StartDate)
                .Select(f => new FinancialYearItemDto
                {
                    Id = f.Id,
                    CompanyId = f.CompanyID,
                    Description = f.Description ?? "FY",
                    StartDate = f.StartDate,
                    EndDate = f.EndDate
                })
                .ToListAsync();
        }

        public async Task<ApplicationUser?> VerifyAdminCredentialsAsync(string emailOrUsername, string password)
        {
            if (string.IsNullOrWhiteSpace(emailOrUsername) || string.IsNullOrWhiteSpace(password))
            {
                return null;
            }

            var clean = emailOrUsername.Trim();

            // Try find by email
            var user = await _userManager.FindByEmailAsync(clean);

            // Try find by username
            if (user == null)
            {
                user = await _userManager.FindByNameAsync(clean);
            }

            // Try find in DbContext directly
            if (user == null)
            {
                int.TryParse(clean, out int empId);
                user = await _dbContext.CApplicationUsers.FirstOrDefaultAsync(u =>
                    u.Email == clean ||
                    u.UserName == clean ||
                    u.Name == clean ||
                    (empId > 0 && u.EmpId == empId));
            }

            if (user != null && user.Active)
            {
                var isPasswordValid = await _userManager.CheckPasswordAsync(user, password);
                if (isPasswordValid)
                {
                    return user;
                }
            }

            return null;
        }

        public async Task<ApplicationUser?> VerifyEmployeeCredentialsAsync(string userIdOrUsername, string password)
        {
            if (string.IsNullOrWhiteSpace(userIdOrUsername) || string.IsNullOrWhiteSpace(password))
            {
                return null;
            }

            var clean = userIdOrUsername.Trim();
            ApplicationUser? user = null;

            if (int.TryParse(clean, out int empId))
            {
                user = await _dbContext.CApplicationUsers.FirstOrDefaultAsync(u => u.EmpId == empId);
            }

            if (user == null)
            {
                user = await _userManager.FindByNameAsync(clean);
            }

            if (user == null)
            {
                user = await _userManager.FindByEmailAsync(clean);
            }

            if (user == null)
            {
                user = await _dbContext.CApplicationUsers.FirstOrDefaultAsync(u =>
                    u.Id == clean || u.Name == clean);
            }

            if (user != null && user.Active)
            {
                var isPasswordValid = await _userManager.CheckPasswordAsync(user, password);
                if (isPasswordValid)
                {
                    return user;
                }
            }

            return null;
        }
    }
}
