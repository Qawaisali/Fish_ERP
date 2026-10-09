using System.Collections.Generic;
using System.Threading.Tasks;
using ERP_Core.Models;

namespace ERP_Core.Services.IServices
{
    public interface IVendorService
    {
        Task<Vendor> CreateVendorAsync(Vendor vendor, string? createdBy = null);
        Task<Vendor> UpdateVendorAsync(Vendor vendor, string? modifiedBy = null);
        Task<bool> DeleteVendorAsync(int id);
        Task<string> GenerateNextAccountCodeAsync(int parentAccountId);
        Task<List<Vendor>> GetVendorsAsync(VendorType? type = null, int? companyId = null, string? search = null, bool includeInactive = false);
        Task<Vendor?> GetByIdAsync(int id);
    }
}
