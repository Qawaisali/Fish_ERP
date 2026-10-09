using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ERP_Core.Attributes;
using ERP_Core.Dtos;
using ERP_Core.Models;
using ERP_Core.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ERP_Core.Controllers.SupplierMaster
{
    [Authorize]
    [FormAccessAuthorize("Supplier Master")]
    [Route("SupplierMaster")]
    public class SupplierMasterController : Controller
    {
        private readonly IVendorService _vendorService;
        private readonly ERP_Core.Data.ERPDbContext _context;
        private static bool _schemaEnsured = false;

        public SupplierMasterController(IVendorService vendorService, ERP_Core.Data.ERPDbContext context)
        {
            _vendorService = vendorService;
            _context = context;
        }


        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Supplier Master";
            ViewData["SubTitle"] = "Suppliers, kashti wale aur mallah manage karein.";


            int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;
            var vendors = await _vendorService.GetVendorsAsync(VendorType.Supplier, companyId);

            var supplierDtos = vendors.Select(v => new SupplierDto
            {
                Id = v.Id,
                Name = v.Name,
                Phone = v.Phone ?? "-",
                VillageOrJetty = v.City ?? "-",
                TotalLots = 0,
                PayableBalance = v.OpeningBalance,
                OpeningBalance = v.OpeningBalance,
                AccountNo = v.AccountNo ?? "-",
                Status = v.Status ?? "Active",
                AccountCode = v.ChartOfAccount?.AccountCode ?? "",
                ParentSupplierId = v.ParentSupplierId,
                ParentSupplierName = v.ParentSupplier?.Name
            }).ToList();

            var parentSuppliersList = vendors
                .Where(v => (v.Active == true || v.Active == null) && (v.Status == null || v.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)) && (!v.ParentSupplierId.HasValue || v.ParentSupplierId.Value <= 0))
                .Select(v => new PartyDropdownDto
                {
                    Id = v.Id,
                    Name = v.Name,
                    Code = v.ChartOfAccount?.AccountCode ?? v.AccountNo ?? "",
                    Phone = v.Phone ?? ""
                }).ToList();

            var model = new SupplierMasterViewModel
            {
                Suppliers = supplierDtos,
                ParentSuppliersList = parentSuppliersList
            };

            return View(model);
        }

        [HttpPost]
        [Route("SaveSupplier")]
        [Route("AddSupplier")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSupplier(int id, string name, string phone, string villageOrJetty, string? accountNo, int? parentSupplierId, string status = "Active")
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "Supplier Name is required.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;
                int? resolvedParentId = (parentSupplierId.HasValue && parentSupplierId.Value > 0) ? parentSupplierId.Value : (int?)null;

                if (id > 0)
                {
                    // Prevent circular self-reference
                    if (resolvedParentId == id)
                    {
                        resolvedParentId = null;
                    }

                    // UPDATE
                    var vendor = new Vendor
                    {
                        Id = id,
                        VendorType = VendorType.Supplier,
                        Name = name.Trim(),
                        Phone = phone?.Trim(),
                        City = villageOrJetty?.Trim(),
                        AccountNo = accountNo?.Trim(),
                        ParentSupplierId = resolvedParentId,
                        Status = string.IsNullOrWhiteSpace(status) ? "Active" : status,
                        CompanyId = companyId
                    };

                    await _vendorService.UpdateVendorAsync(vendor, User.Identity?.Name ?? "System");
                    TempData["SuccessMessage"] = $"Supplier '{name}' updated successfully and Chart of Accounts synchronized.";
                }
                else
                {
                    // CREATE
                    var vendor = new Vendor
                    {
                        VendorType = VendorType.Supplier,
                        Name = name.Trim(),
                        Phone = phone?.Trim(),
                        City = villageOrJetty?.Trim(),
                        OpeningBalance = 0,
                        AccountNo = accountNo?.Trim(),
                        ParentSupplierId = resolvedParentId,
                        Status = string.IsNullOrWhiteSpace(status) ? "Active" : status,
                        CompanyId = companyId,
                        Active = true
                    };

                    var created = await _vendorService.CreateVendorAsync(vendor, User.Identity?.Name ?? "System");

                    string coaInfo = created.ChartOfAccount != null 
                        ? $" (COA Code: {created.ChartOfAccount.AccountCode})" 
                        : "";

                    TempData["SuccessMessage"] = $"Supplier '{name}' added successfully{coaInfo} and linked to Chart of Accounts.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to save supplier: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Route("DeleteSupplier")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSupplier(int id)
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid Supplier ID.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                bool deleted = await _vendorService.DeleteVendorAsync(id);
                if (deleted)
                {
                    TempData["SuccessMessage"] = "Supplier deleted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Supplier not found.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to delete supplier: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
