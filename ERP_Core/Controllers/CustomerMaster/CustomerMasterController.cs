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

namespace ERP_Core.Controllers.CustomerMaster
{
    [Authorize]
    [FormAccessAuthorize("Customer Master")]
    [Route("CustomerMaster")]
    public class CustomerMasterController : Controller
    {
        private readonly IVendorService _vendorService;

        public CustomerMasterController(IVendorService vendorService)
        {
            _vendorService = vendorService;
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Customer Master";
            ViewData["SubTitle"] = "Customers, hotels aur buyers manage karein.";

            int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;
            var vendors = await _vendorService.GetVendorsAsync(VendorType.Customer, companyId);

            var customerDtos = vendors.Select(v => new CustomerDto
            {
                Id = v.Id,
                Name = v.Name,
                Phone = v.Phone ?? "-",
                City = v.City ?? "-",
                TotalBills = 0,
                ReceivableBalance = v.OpeningBalance,
                OpeningBalance = v.OpeningBalance,
                Cnic = v.Cnic ?? "-",
                Status = v.Status ?? "Active",
                AccountCode = v.ChartOfAccount?.AccountCode ?? ""
            }).ToList();

            var model = new CustomerMasterViewModel
            {
                Customers = customerDtos
            };

            return View(model);
        }

        [HttpPost]
        [Route("SaveCustomer")]
        [Route("AddCustomer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCustomer(int id, string name, string phone, string city, string? cnic, string status = "Active")
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "Customer Name is required.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;

                if (id > 0)
                {
                    // UPDATE
                    var vendor = new Vendor
                    {
                        Id = id,
                        VendorType = VendorType.Customer,
                        Name = name.Trim(),
                        Phone = phone?.Trim(),
                        City = city?.Trim(),
                        Cnic = cnic?.Trim(),
                        Status = string.IsNullOrWhiteSpace(status) ? "Active" : status,
                        CompanyId = companyId
                    };

                    await _vendorService.UpdateVendorAsync(vendor, User.Identity?.Name ?? "System");
                    TempData["SuccessMessage"] = $"Customer '{name}' updated successfully and Chart of Accounts synchronized.";
                }
                else
                {
                    // CREATE
                    var vendor = new Vendor
                    {
                        VendorType = VendorType.Customer,
                        Name = name.Trim(),
                        Phone = phone?.Trim(),
                        City = city?.Trim(),
                        OpeningBalance = 0,
                        Cnic = cnic?.Trim(),
                        Status = string.IsNullOrWhiteSpace(status) ? "Active" : status,
                        CompanyId = companyId,
                        Active = true
                    };

                    var created = await _vendorService.CreateVendorAsync(vendor, User.Identity?.Name ?? "System");

                    string coaInfo = created.ChartOfAccount != null 
                        ? $" (COA Code: {created.ChartOfAccount.AccountCode})" 
                        : "";

                    TempData["SuccessMessage"] = $"Customer '{name}' added successfully{coaInfo} and linked to Chart of Accounts.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to save customer: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Route("DeleteCustomer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid Customer ID.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                bool deleted = await _vendorService.DeleteVendorAsync(id);
                if (deleted)
                {
                    TempData["SuccessMessage"] = "Customer deleted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Customer not found.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to delete customer: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
