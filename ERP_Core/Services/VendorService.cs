using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ERP_Core.Data;
using ERP_Core.Models;
using ERP_Core.Services.IServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP_Core.Services
{
    public class VendorService : IVendorService
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<VendorService> _logger;

        public VendorService(ERPDbContext context, ILogger<VendorService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // =========================================================================
        // 1. CREATE VENDOR & AUTOMATICALLY CREATE COA ACCOUNT BASED ON ACCOUNT SETUP
        // =========================================================================
        public async Task<Vendor> CreateVendorAsync(Vendor vendor, string? createdBy = null)
        {
            try
            {
                // 1. Fetch AccountSetup for company (or fallback to any configured setup)
                AccountSetup? setup = null;
                if (vendor.CompanyId.HasValue && vendor.CompanyId.Value > 0)
                {
                    setup = await _context.AccountSetups
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.CompanyId == vendor.CompanyId.Value);
                }

                if (setup == null)
                {
                    setup = await _context.AccountSetups.AsNoTracking().FirstOrDefaultAsync();
                }

                // 2. Determine parent Chart of Account ID based on VendorType
                int? parentAccountId = null;
                if (vendor.VendorType == VendorType.Supplier)
                {
                    parentAccountId = setup?.SupplierAccID;
                    if (!parentAccountId.HasValue || parentAccountId.Value <= 0)
                    {
                        var defaultSup = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.AccountCode == "0201001" || a.AccountName.ToLower().Contains("supplier payable") || a.AccountName.ToLower().Contains("fish supplier"));
                        parentAccountId = defaultSup?.Id;
                    }
                }
                else if (vendor.VendorType == VendorType.Customer)
                {
                    parentAccountId = setup?.CustomerAccID;
                    if (!parentAccountId.HasValue || parentAccountId.Value <= 0)
                    {
                        var defaultCust = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.AccountCode == "0101003" || a.AccountName.ToLower().Contains("customer receivable") || a.AccountName.ToLower().Contains("accounts receivable"));
                        parentAccountId = defaultCust?.Id;
                    }
                }

                // 3. Create sub-account in Chart of Accounts if parent head resolved
                if (parentAccountId.HasValue && parentAccountId.Value > 0)
                {
                    var parentAccount = await _context.ChartOfAccounts.FindAsync(parentAccountId.Value);
                    if (parentAccount != null)
                    {
                        string nextAccountCode = await GenerateNextAccountCodeAsync(parentAccountId.Value);

                        var newCoa = new ChartOfAccount
                        {
                            AccountName = vendor.Name.Trim(),
                            AccountCode = nextAccountCode,
                            ParentAccountId = parentAccountId.Value,
                            NatureOfAccountId = parentAccount.NatureOfAccountId,
                            Currency = string.IsNullOrWhiteSpace(parentAccount.Currency) ? "PKR" : parentAccount.Currency,
                            Transable = true,
                            Active = true,
                            LevelNo = (parentAccount.LevelNo ?? 3) + 1,
                            CompanyId = vendor.CompanyId,
                            CreatedOn = DateTime.Now,
                            CreatedBy = createdBy ?? "System"
                        };

                        await _context.ChartOfAccounts.AddAsync(newCoa);
                        await _context.SaveChangesAsync();

                        vendor.ChartOfAccountId = newCoa.Id;
                        vendor.ChartOfAccount = newCoa;
                        if (string.IsNullOrWhiteSpace(vendor.AccountNo))
                        {
                            vendor.AccountNo = nextAccountCode;
                        }
                        _logger.LogInformation("COA Account '{AccountName}' ({AccountCode}) created for Vendor under Parent ID {ParentId}",
                            newCoa.AccountName, newCoa.AccountCode, parentAccountId.Value);
                    }
                }
                else
                {
                    _logger.LogWarning("No parent posting head found in AccountSetup for {VendorType} '{VendorName}'. COA account not auto-created.",
                        vendor.VendorType, vendor.Name);
                }

                // 4. Save Vendor Record
                vendor.CreatedOn = DateTime.Now;
                vendor.CreatedBy = createdBy ?? "System";
                if (!vendor.Active.HasValue) vendor.Active = true;

                await _context.Vendors.AddAsync(vendor);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Vendor '{VendorName}' ({VendorType}) created successfully with ID {VendorId}, Code: {Code}",
                    vendor.Name, vendor.VendorType, vendor.Id, vendor.AccountNo);

                return vendor;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Vendor '{VendorName}' ({VendorType})", vendor.Name, vendor.VendorType);
                throw;
            }
        }

        // =========================================================================
        // 2. UPDATE VENDOR & SYNC LINKED CHART OF ACCOUNTS RECORD / GENERATE CODE
        // =========================================================================
        public async Task<Vendor> UpdateVendorAsync(Vendor vendor, string? modifiedBy = null)
        {
            var existing = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == vendor.Id);
            if (existing == null)
            {
                throw new InvalidOperationException($"Vendor with ID {vendor.Id} not found.");
            }

            existing.Name = vendor.Name.Trim();
            existing.Phone = vendor.Phone?.Trim();
            existing.City = vendor.City?.Trim();
            existing.Address = vendor.Address?.Trim();
            existing.Cnic = vendor.Cnic?.Trim();
            if (!string.IsNullOrWhiteSpace(vendor.AccountNo))
            {
                existing.AccountNo = vendor.AccountNo.Trim();
            }
            existing.Status = string.IsNullOrWhiteSpace(vendor.Status) ? "Active" : vendor.Status;
            existing.ParentSupplierId = vendor.ParentSupplierId;
            existing.ModifiedOn = DateTime.Now;
            existing.ModifiedBy = modifiedBy ?? "System";

            // If a ChartOfAccount is linked, update its account name and active status as well
            if (existing.ChartOfAccountId.HasValue && existing.ChartOfAccountId.Value > 0)
            {
                var coa = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.Id == existing.ChartOfAccountId.Value);
                if (coa != null)
                {
                    coa.AccountName = existing.Name;
                    coa.Active = existing.Status.Equals("Active", StringComparison.OrdinalIgnoreCase);
                    coa.ModifiedOn = DateTime.Now;
                    coa.ModifiedBy = modifiedBy ?? "System";
                    _context.ChartOfAccounts.Update(coa);
                    existing.ChartOfAccount = coa;

                    // Ensure AccountNo has the generated code if empty
                    if (string.IsNullOrWhiteSpace(existing.AccountNo) && !string.IsNullOrWhiteSpace(coa.AccountCode))
                    {
                        existing.AccountNo = coa.AccountCode;
                    }
                }
            }
            else
            {
                // Generate Account Code and create Chart of Account for vendor if not linked yet
                int? parentAccountId = null;
                AccountSetup? setup = null;
                if (existing.CompanyId.HasValue && existing.CompanyId.Value > 0)
                {
                    setup = await _context.AccountSetups.AsNoTracking().FirstOrDefaultAsync(s => s.CompanyId == existing.CompanyId.Value);
                }
                if (setup == null)
                {
                    setup = await _context.AccountSetups.AsNoTracking().FirstOrDefaultAsync();
                }

                if (existing.VendorType == VendorType.Supplier)
                {
                    parentAccountId = setup?.SupplierAccID;
                    if (!parentAccountId.HasValue || parentAccountId.Value <= 0)
                    {
                        var defaultSup = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.AccountCode == "0201001" || a.AccountName.ToLower().Contains("supplier payable") || a.AccountName.ToLower().Contains("fish supplier"));
                        parentAccountId = defaultSup?.Id;
                    }
                }
                else
                {
                    parentAccountId = setup?.CustomerAccID;
                    if (!parentAccountId.HasValue || parentAccountId.Value <= 0)
                    {
                        var defaultCust = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.AccountCode == "0101003" || a.AccountName.ToLower().Contains("customer receivable") || a.AccountName.ToLower().Contains("accounts receivable"));
                        parentAccountId = defaultCust?.Id;
                    }
                }

                if (parentAccountId.HasValue && parentAccountId.Value > 0)
                {
                    var parentAccount = await _context.ChartOfAccounts.FindAsync(parentAccountId.Value);
                    if (parentAccount != null)
                    {
                        string nextAccountCode = await GenerateNextAccountCodeAsync(parentAccountId.Value);
                        var newCoa = new ChartOfAccount
                        {
                            AccountName = existing.Name.Trim(),
                            AccountCode = nextAccountCode,
                            ParentAccountId = parentAccountId.Value,
                            NatureOfAccountId = parentAccount.NatureOfAccountId,
                            Currency = string.IsNullOrWhiteSpace(parentAccount.Currency) ? "PKR" : parentAccount.Currency,
                            Transable = true,
                            Active = true,
                            CompanyId = existing.CompanyId,
                            CreatedOn = DateTime.Now,
                            CreatedBy = modifiedBy ?? "System"
                        };

                        await _context.ChartOfAccounts.AddAsync(newCoa);
                        await _context.SaveChangesAsync();

                        existing.ChartOfAccountId = newCoa.Id;
                        existing.ChartOfAccount = newCoa;
                        if (string.IsNullOrWhiteSpace(existing.AccountNo))
                        {
                            existing.AccountNo = nextAccountCode;
                        }
                    }
                }
            }

            _context.Vendors.Update(existing);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Vendor '{Name}' (ID: {Id}, Code: {Code}) updated and code generated successfully", existing.Name, existing.Id, existing.AccountNo);
            return existing;
        }

        // =========================================================================
        // 3. SOFT DELETE VENDOR (SET ACTIVE = FALSE)
        // =========================================================================
        public async Task<bool> DeleteVendorAsync(int id)
        {
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == id);
            if (vendor == null)
            {
                return false;
            }

            vendor.Active = false;
            vendor.Status = "Inactive";
            vendor.ModifiedOn = DateTime.Now;
            _context.Vendors.Update(vendor);

            // Also set linked ChartOfAccount to active = 0 (false)
            if (vendor.ChartOfAccountId.HasValue && vendor.ChartOfAccountId.Value > 0)
            {
                var coa = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.Id == vendor.ChartOfAccountId.Value);
                if (coa != null)
                {
                    coa.Active = false;
                    coa.ModifiedOn = DateTime.Now;
                    _context.ChartOfAccounts.Update(coa);
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Vendor ID {Id} soft-deleted (Active set to 0)", id);
            return true;
        }

        // =========================================================================
        // 4. GENERATE NEXT ACCOUNT CODE HIERARCHICALLY (e.g., 020100100001, 010100300001)
        // =========================================================================
        public async Task<string> GenerateNextAccountCodeAsync(int parentAccountId)
        {
            var parent = await _context.ChartOfAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == parentAccountId);

            if (parent == null || string.IsNullOrWhiteSpace(parent.AccountCode))
            {
                return $"{parentAccountId}00001";
            }

            // Determine padding length (no hyphens):
            // Level 1 (2 digits) -> Level 2 child suffix is 2 digits (e.g. 02 -> 0201)
            // Level 2 (4 digits) -> Level 3 child suffix is 3 digits (e.g. 0201 -> 0201001)
            // Level 3 (7 digits) -> Level 4 child suffix is 5 digits (e.g. 0201001 -> 020100100001)
            int paddingLength = 5;
            if (parent.AccountCode.Length == 2)
            {
                paddingLength = 2;
            }
            else if (parent.AccountCode.Length == 4)
            {
                paddingLength = 3;
            }
            else
            {
                paddingLength = 5;
            }

            // Check existing sibling accounts
            var siblingAccounts = await _context.ChartOfAccounts
                .Where(a => a.ParentAccountId == parentAccountId && a.AccountCode != null)
                .ToListAsync();

            // Normalize any legacy test accounts with hyphens (e.g. 0201001-30, 0101003-76) to match hardcoded standard
            bool dbChanged = false;
            foreach (var sibling in siblingAccounts)
            {
                if (sibling.AccountCode!.Contains('-') && sibling.AccountCode.StartsWith(parent.AccountCode))
                {
                    var remainder = sibling.AccountCode.Substring(parent.AccountCode.Length).TrimStart('-', '.');
                    if (int.TryParse(remainder, out int parsed))
                    {
                        string cleanCode = $"{parent.AccountCode}{parsed.ToString().PadLeft(paddingLength, '0')}";
                        sibling.AccountCode = cleanCode;
                        dbChanged = true;

                        // Also sync linked vendor AccountNo if exists
                        var linkedVendor = await _context.Vendors.FirstOrDefaultAsync(v => v.ChartOfAccountId == sibling.Id || v.AccountNo == sibling.AccountCode);
                        if (linkedVendor != null)
                        {
                            linkedVendor.AccountNo = cleanCode;
                        }
                    }
                }
            }

            if (dbChanged)
            {
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not save normalized sibling codes: {Message}", ex.Message);
                }
            }

            var siblingCodes = siblingAccounts
                .Select(a => a.AccountCode!)
                .Where(c => !string.IsNullOrEmpty(c))
                .ToHashSet();

            // If standard non-hyphenated siblings exist, match their suffix length
            var standardSiblings = siblingCodes
                .Where(c => c.StartsWith(parent.AccountCode) && !c.Contains('-') && !c.Contains('.'))
                .ToList();

            if (standardSiblings.Any())
            {
                int maxSuffix = standardSiblings.Max(c => c.Length - parent.AccountCode.Length);
                if (maxSuffix > 0)
                {
                    paddingLength = maxSuffix;
                }
            }

            // Find highest index among sibling codes
            int maxSubIndex = 0;
            foreach (var code in siblingCodes)
            {
                if (code.StartsWith(parent.AccountCode))
                {
                    var remainder = code.Substring(parent.AccountCode.Length).TrimStart('-', '.');
                    if (int.TryParse(remainder, out int parsed))
                    {
                        if (parsed > maxSubIndex) maxSubIndex = parsed;
                    }
                }
            }

            int nextIndex = maxSubIndex + 1;
            string nextAccountCode;
            do
            {
                nextAccountCode = $"{parent.AccountCode}{nextIndex.ToString().PadLeft(paddingLength, '0')}";
                nextIndex++;
            } while (siblingCodes.Contains(nextAccountCode));

            return nextAccountCode;
        }

        // =========================================================================
        // 5. RETRIEVE VENDORS
        // =========================================================================
        public async Task<List<Vendor>> GetVendorsAsync(VendorType? type = null, int? companyId = null, string? search = null, bool includeInactive = false)
        {
        

            var query = _context.Vendors
                .Include(v => v.ChartOfAccount)
                .Include(v => v.Company)
                .Include(v => v.ParentSupplier)
                .AsNoTracking()
                .AsQueryable();

            if (!includeInactive)
            {
                query = query.Where(v => v.Active == true || v.Active == null);
            }

            if (type.HasValue)
            {
                query = query.Where(v => v.VendorType == type.Value);
            }

            if (companyId.HasValue && companyId.Value > 0)
            {
                query = query.Where(v => v.CompanyId == companyId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim().ToLower();
                query = query.Where(v =>
                    v.Name.ToLower().Contains(term) ||
                    (v.Phone != null && v.Phone.ToLower().Contains(term)) ||
                    (v.City != null && v.City.ToLower().Contains(term))
                );
            }

            return await query
                .OrderBy(v => v.Name)
                .ToListAsync();
        }

        // =========================================================================
        // 6. RETRIEVE VENDOR BY ID
        // =========================================================================
        public async Task<Vendor?> GetByIdAsync(int id)
        {
            return await _context.Vendors
                .Include(v => v.ChartOfAccount)
                .Include(v => v.Company)
                .Include(v => v.ParentSupplier)
                .FirstOrDefaultAsync(v => v.Id == id);
        }

    }
}
