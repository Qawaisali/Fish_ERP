using ERP_Core.Attributes;
using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using BankMaster = ERP_Core.Models.BankMaster;
using ERP_Core.Repositories.IRepositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ERP_Core.Services.IServices;

namespace ERP_Core.Controllers.NewBill
{
    [Authorize]
    [FormAccessAuthorize("Daily Purchase")]
    [Route("NewBill")]
    [Route("new-bill")]
    [Route("DailyPurchase")]
    public class NewBillController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<NewBillController> _logger;
        private static bool _schemaEnsured = false;

        public NewBillController(
            ERPDbContext context, 
            IUnitOfWork unitOfWork, 
            ILogger<NewBillController> logger)
        {
            _context = context;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        private async Task EnsureBillMasterColumnsAsync()
        {
            if (_schemaEnsured) return;
            try
            {
                await _context.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BillMasters') AND name = 'FarmerIceExpense')
                    BEGIN
                        ALTER TABLE [BillMasters] ADD [FarmerIceExpense] decimal(18,2) NOT NULL DEFAULT 0;
                    END
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BillMasters') AND name = 'FreightExpense')
                    BEGIN
                        ALTER TABLE [BillMasters] ADD [FreightExpense] decimal(18,2) NOT NULL DEFAULT 0;
                    END
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BillMasters') AND name = 'OtherExpense')
                    BEGIN
                        ALTER TABLE [BillMasters] ADD [OtherExpense] decimal(18,2) NOT NULL DEFAULT 0;
                    END
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BillMasters') AND name = 'Deposit')
                    BEGIN
                        ALTER TABLE [BillMasters] ADD [Deposit] decimal(18,2) NOT NULL DEFAULT 0;
                    END
                ");
                _schemaEnsured = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Column auto-check skipped: {Message}", ex.Message);
            }
        }

        // =========================================================================
        // 1. INDEX: LOAD BILL ENTRY & RECENT BILLS
        // =========================================================================
        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "New Bill";
            ViewData["SubTitle"] = "Customer select karein, lots jodein — commission, expense aur accounting entries automatic post hongi.";
            ViewData["ActivePage"] = "NewBill";

            try
            {
                await EnsureBillMasterColumnsAsync();
                int companyId = GetCurrentCompanyId();
                //await EnsureDefaultEntitiesAsync();
                //await EnsureCoreChartOfAccountsAsync(companyId);

                // 1. Load active Fish dropdowns from FishMaster
                var fishes = await _context.Fishes
                    .AsNoTracking()
                    .Where(f => f.Active == true || f.Active == null)
                    .OrderBy(f => f.Name)
                    .Select(f => new FishDropdownDto
                    {
                        Id = f.Id,
                        Code = f.Code ?? "",
                        Name = f.Name,
                        Type = f.Type,
                        Unit = f.Unit ?? "Kg"
                    })
                    .ToListAsync();

                // 2. Load active Suppliers (Vendors where VendorType == Supplier)
                var suppliers = await _context.Vendors
                    .Include(v => v.ChartOfAccount)
                    .AsNoTracking()
                    .Where(v => v.VendorType == VendorType.Supplier && (v.Status == "Active" || v.Active == true || v.Active == null))
                    .OrderBy(v => v.Name)
                    .Select(v => new VendorDropdownDto
                    {
                        Id = v.Id,
                        Name = v.Name,
                        Code = v.ChartOfAccount != null ? v.ChartOfAccount.AccountCode : (v.AccountNo ?? ""),
                        Phone = v.Phone,
                        City = v.City
                    })
                    .ToListAsync();

                // 3. Load active Customers (Vendors where VendorType == Customer)
                var customers = await _context.Vendors
                    .Include(v => v.ChartOfAccount)
                    .AsNoTracking()
                    .Where(v => v.VendorType == VendorType.Customer && (v.Status == "Active" || v.Active == true || v.Active == null))
                    .OrderBy(v => v.Name)
                    .Select(v => new VendorDropdownDto
                    {
                        Id = v.Id,
                        Name = v.Name,
                        Code = v.ChartOfAccount != null ? v.ChartOfAccount.AccountCode : (v.AccountNo ?? ""),
                        Phone = v.Phone,
                        City = v.City
                    })
                    .ToListAsync();

                // 4. Generate next Bill Number
                string nextBillNo = await GenerateNextBillNoAsync();

                // 5. Load Recent Bills from Database (Master + Details)
                var rawBills = await _context.BillMasters
                    .Include(b => b.Supplier)
                    .Include(b => b.Details)
                        .ThenInclude(d => d.Fish)
                    .Include(b => b.Details)
                        .ThenInclude(d => d.Customer)
                    .AsNoTracking()
                    .OrderByDescending(b => b.BillDate)
                    .ThenByDescending(b => b.Id)
                    .Take(50)
                    .ToListAsync();

                var billIds = rawBills.Select(b => b.Id).ToHashSet();
                var billNos = rawBills.Select(b => b.BillNo).Where(n => !string.IsNullOrEmpty(n)).ToHashSet();
                
                var vouchers = await _context.Vouchers
                    .AsNoTracking()
                    .Where(v => v.CompanyId == null || v.CompanyId == companyId)
                    .OrderByDescending(v => v.VoucherId)
                    .Take(200)
                    .ToListAsync();

                var mainVoucherLookup = vouchers
                    .Where(v => (v.VoucherType == "JV" || (v.VoucherNo != null && v.VoucherNo.StartsWith("JV-"))) &&
                                ((v.InvoiceId.HasValue && billIds.Contains(v.InvoiceId.Value)) ||
                                 (!string.IsNullOrEmpty(v.InvoiceNo) && billNos.Contains(v.InvoiceNo))))
                    .GroupBy(v => v.InvoiceNo ?? (v.InvoiceId?.ToString() ?? ""))
                    .ToDictionary(g => g.Key, g => g.First().VoucherNo);

                var expVoucherLookup = vouchers
                    .Where(v => (v.VoucherType == "CP" || v.VoucherType == "CPV" || (v.VoucherNo != null && v.VoucherNo.StartsWith("CPV-"))) &&
                                IsFarmerExpenseVoucher(v) &&
                                ((v.InvoiceId.HasValue && billIds.Contains(v.InvoiceId.Value)) ||
                                 (!string.IsNullOrEmpty(v.InvoiceNo) && billNos.Contains(v.InvoiceNo))))
                    .GroupBy(v => v.InvoiceNo ?? (v.InvoiceId?.ToString() ?? ""))
                    .ToDictionary(g => g.Key, g => g.First().VoucherNo);

                var supplierPaymentVouchers = vouchers
                    .Where(v => (v.VoucherType == "CP" || v.VoucherType == "BP" || v.VoucherType == "CPV" || v.VoucherType == "BPV") &&
                                (v.DeleteStatus == 0 || v.DeleteStatus == null) &&
                                !IsFarmerExpenseVoucher(v))
                    .ToList();

                var customerReceiptVouchers = vouchers
                    .Where(v => (v.VoucherType == "CR" || v.VoucherType == "BR" || v.VoucherType == "CRV" || v.VoucherType == "BRV") &&
                                (v.DeleteStatus == 0 || v.DeleteStatus == null))
                    .ToList();

                var recentBills = rawBills.Select(b =>
                {
                    var paidToSupplier = supplierPaymentVouchers
                        .Where(v => (v.InvoiceId == b.Id || (!string.IsNullOrEmpty(b.BillNo) && v.InvoiceNo == b.BillNo)) &&
                                    (b.SupplierId == null || v.VendorId == b.SupplierId))
                        .Sum(v => v.Amount);

                    string calculatedStatus = "Due";
                    if (paidToSupplier >= b.NetPayable && b.NetPayable > 0)
                    {
                        calculatedStatus = "Paid";
                    }
                    else if (paidToSupplier > 0)
                    {
                        calculatedStatus = "Partial";
                    }
                    else
                    {
                        calculatedStatus = (b.Status == "Paid" || b.Status == "Partial") && paidToSupplier == 0 ? "Due" : (b.Status ?? "Due");
                    }

                    return new BillSummaryDto
                    {
                        Id = b.Id,
                        BillNo = b.BillNo,
                        VoucherNo = (!string.IsNullOrEmpty(b.BillNo) && mainVoucherLookup.ContainsKey(b.BillNo)) ? mainVoucherLookup[b.BillNo] : null,
                        ExpenseVoucherNo = (!string.IsNullOrEmpty(b.BillNo) && expVoucherLookup.ContainsKey(b.BillNo)) ? expVoucherLookup[b.BillNo] : null,
                        Date = b.BillDate.ToString("dd-MMM-yyyy"),
                        SupplierId = b.SupplierId,
                        SupplierName = b.Supplier?.Name ?? "General Boat Catch",
                        LotsCount = b.Details.Count,
                        TotalSale = b.TotalGrossAmount,
                        Commission = b.CommissionAmount,
                        CommissionPercent = b.CommissionPercent,
                        LabourExpense = b.LabourExpense,
                        IceExpense = b.IceExpense,
                        FarmerIceExpense = b.FarmerIceExpense,
                        FreightExpense = b.FreightExpense,
                        OtherExpense = b.OtherExpense,
                        Expenses = b.TotalExpense,
                        Deposit = b.Deposit,
                        NetPayable = Math.Round(b.NetPayable, 0),
                        PaidToSupplier = paidToSupplier,
                        Status = calculatedStatus,
                        Remarks = b.Remarks ?? "",
                        Lots = b.Details.Select(d =>
                        {
                            var lotReceived = customerReceiptVouchers
                                .Where(v => IsLotReceiptVoucher(v, d, b))
                                .Sum(v => v.Amount);

                            return new BillLotItemDto
                            {
                                Id = d.Id,
                                LotNo = d.LotNo ?? $"LOT-{d.Id}",
                                FishId = d.FishId,
                                FishName = d.Fish?.Name ?? "Fish",
                                CustomerId = d.CustomerId,
                                CustomerName = d.Customer?.Name ?? "General Buyer",
                                Crates = d.Crates,
                                WeightStr = BillLotItemDto.FormatKgToMaund(d.WeightKg),
                                WeightKg = d.WeightKg,
                                RatePerKg = d.RatePerKg,
                                Amount = d.Amount,
                                ReceivedAmount = lotReceived
                            };
                        }).ToList()
                    };
                }).ToList();

                // Initialize clean empty lots table with one fresh row
                var initialLots = new List<BillLotItemDto>
                {
                    new BillLotItemDto
                    {
                        LotNo = "LOT-1001",
                        FishId = null,
                        FishName = "",
                        CustomerId = null,
                        CustomerName = "",
                        WeightStr = "000-00-0",
                        WeightKg = 0,
                        RatePerKg = 0,
                        Amount = 0
                    }
                };

                var model = new NewBillViewModel
                {
                    BillNo = nextBillNo,
                    Date = DateTime.Now.ToString("yyyy-MM-dd"),
                    SelectedSupplierId = null,
                    SelectedSupplier = "",
                    CommissionPercent = 6.0m,
                    LabourExpense = 0,
                    IceExpense = 0,
                    FarmerIceExpense = 0,
                    FreightExpense = 0,
                    OtherExpense = 0,
                    TotalExpense = 0,
                    Deposit = 0,
                    TotalGrossAmount = 0,
                    NetPayable = 0,
                    Lots = initialLots,
                    RecentBills = recentBills,
                    FishList = fishes,
                    SuppliersList = suppliers,
                    CustomersList = customers,
                    CashAndBankAccounts = await GetCashAndBankAccountsAsync(companyId)
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing New Bill screen");
                TempData["ErrorMessage"] = "Failed to load billing data: " + ex.Message;
                return View(new NewBillViewModel());
            }
        }

        // =========================================================================
        // 2. SAVE BILL (MASTER, DETAILS & AUTOMATIC ACCOUNTING VOUCHER GENERATION)
        // =========================================================================
        [HttpPost]
        [Route("SaveBill")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveBill(NewBillViewModel model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.BillNo))
                {
                    model.BillNo = await GenerateNextBillNoAsync();
                }

                // Filter valid lots
                var validLots = (model.Lots ?? new List<BillLotItemDto>())
                    .Where(l => !string.IsNullOrWhiteSpace(l.WeightStr) && l.WeightStr != "000-00-0" && l.WeightStr != "000-000-0" || l.WeightKg > 0 || l.RatePerKg > 0 || l.FishId.HasValue || !string.IsNullOrEmpty(l.LotNo))
                    .ToList();

                if (!validLots.Any())
                {
                    TempData["ErrorMessage"] = "Please add at least one valid lot row with weight and rate.";
                    return RedirectToAction(nameof(Index));
                }

                // 1. Calculate exact Maund-Kg-Pao weights, totals and resolve Customer IDs server-side
                decimal totalGross = 0;
                for (int i = 0; i < validLots.Count; i++)
                {
                    var lot = validLots[i];
                    if (!string.IsNullOrWhiteSpace(lot.WeightStr))
                    {
                        lot.WeightKg = BillLotItemDto.ParseMaundToKg(lot.WeightStr);
                    }
                    lot.Amount = Math.Round(lot.WeightKg * lot.RatePerKg, 0);
                    totalGross += lot.Amount;

                    // Ensure CustomerId is resolved from model or direct Form collection
                    if (!lot.CustomerId.HasValue || lot.CustomerId.Value <= 0)
                    {
                        if (Request.HasFormContentType)
                        {
                            string formKey = $"Lots[{i}].CustomerId";
                            if (Request.Form.ContainsKey(formKey) && int.TryParse(Request.Form[formKey], out int parsedCId) && parsedCId > 0)
                            {
                                lot.CustomerId = parsedCId;
                            }
                        }
                    }

                    // Fallback to lookup by CustomerName if CustomerId is still missing
                    if ((!lot.CustomerId.HasValue || lot.CustomerId.Value <= 0) && !string.IsNullOrWhiteSpace(lot.CustomerName))
                    {
                        var matchedCust = await _context.Vendors
                            .AsNoTracking()
                            .FirstOrDefaultAsync(v => v.VendorType == VendorType.Customer && v.Name.ToLower() == lot.CustomerName.Trim().ToLower());
                        if (matchedCust != null)
                        {
                            lot.CustomerId = matchedCust.Id;
                        }
                    }

                    // Required Validations for Bill Detail Lot Row
                    if (!lot.FishId.HasValue || lot.FishId.Value <= 0)
                    {
                        TempData["ErrorMessage"] = $"Row #{i + 1}: Product / Fish Variety is required.";
                        return RedirectToAction(nameof(Index));
                    }

                    if (!lot.CustomerId.HasValue || lot.CustomerId.Value <= 0)
                    {
                        TempData["ErrorMessage"] = $"Row #{i + 1}: Customer (Buyer) is required.";
                        return RedirectToAction(nameof(Index));
                    }

                    if (lot.WeightKg <= 0 || string.IsNullOrWhiteSpace(lot.WeightStr) || lot.WeightStr == "000-00-0" || lot.WeightStr == "000-000-0")
                    {
                        TempData["ErrorMessage"] = $"Row #{i + 1}: Weight is required and must be greater than zero.";
                        return RedirectToAction(nameof(Index));
                    }

                    if (lot.RatePerKg <= 0)
                    {
                        TempData["ErrorMessage"] = $"Row #{i + 1}: Rate per Kg is required and must be greater than zero.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                decimal commAmt = 0;
                decimal commPct = 0;

                if (model.CommissionAmount > 0 && model.CommissionPercent <= 0)
                {
                    commAmt = model.CommissionAmount;
                    commPct = totalGross > 0 ? Math.Round((commAmt / totalGross) * 100m, 2) : 0;
                }
                else if (model.CommissionAmount > 0 && model.CommissionPercent > 0)
                {
                    commAmt = model.CommissionAmount;
                    commPct = model.CommissionPercent;
                }
                else
                {
                    commPct = model.CommissionPercent >= 0 ? model.CommissionPercent : 6.0m;
                    commAmt = Math.Round(totalGross * (commPct / 100m), 2);
                }
                decimal totalExp = Math.Max(0, model.LabourExpense) + Math.Max(0, model.IceExpense) + Math.Max(0, model.FarmerIceExpense) + Math.Max(0, model.FreightExpense) + Math.Max(0, model.OtherExpense);
                decimal depositAmt = Math.Max(0, model.Deposit);
                decimal netPayable = Math.Round(Math.Max(0, totalGross - commAmt - totalExp - depositAmt), 0);

                DateTime billDate = DateTime.TryParse(model.Date, out var parsedDate) ? parsedDate : DateTime.Today;
                int companyId = GetCurrentCompanyId();
                int fyId = GetCurrentFinancialYearId();
                int? userId = GetCurrentUserId();
                string currentUser = User.Identity?.Name ?? "System";
                int? primaryCustomerId = validLots.FirstOrDefault(l => l.CustomerId.HasValue && l.CustomerId.Value > 0)?.CustomerId;

                await EnsureBillMasterColumnsAsync();

                BillMaster? bill;
                if (model.Id > 0)
                {
                    // Edit Existing Bill
                    bill = await _context.BillMasters
                        .Include(b => b.Details)
                        .FirstOrDefaultAsync(b => b.Id == model.Id);

                    if (bill == null)
                    {
                        TempData["ErrorMessage"] = "Bill record not found for update.";
                        return RedirectToAction(nameof(Index));
                    }

                    bill.BillNo = model.BillNo;
                    bill.BillDate = billDate;
                    bill.SupplierId = (model.SelectedSupplierId.HasValue && model.SelectedSupplierId.Value > 0) ? model.SelectedSupplierId : null;
                    bill.CustomerId = primaryCustomerId;
                    bill.CommissionPercent = commPct;
                    bill.CommissionAmount = commAmt;
                    bill.LabourExpense = model.LabourExpense;
                    bill.IceExpense = model.IceExpense;
                    bill.FarmerIceExpense = model.FarmerIceExpense;
                    bill.FreightExpense = model.FreightExpense;
                    bill.OtherExpense = model.OtherExpense;
                    bill.TotalExpense = totalExp;
                    bill.Deposit = depositAmt;
                    bill.TotalGrossAmount = totalGross;
                    bill.NetPayable = netPayable;
                    bill.Status = string.IsNullOrWhiteSpace(model.Status) ? "Due" : model.Status;
                    bill.Remarks = model.Remarks;
                    bill.ModifiedOn = DateTime.Now;
                    bill.ModifiedBy = currentUser;

                    // Remove old details and recreate
                    _context.BillDetails.RemoveRange(bill.Details);
                    bill.Details.Clear();

                    foreach (var l in validLots)
                    {
                        bill.Details.Add(new BillDetail
                        {
                            LotNo = string.IsNullOrWhiteSpace(l.LotNo) ? "LOT" : l.LotNo.Trim(),
                            FishId = (l.FishId.HasValue && l.FishId.Value > 0) ? l.FishId : null,
                            CustomerId = (l.CustomerId.HasValue && l.CustomerId.Value > 0) ? l.CustomerId : null,
                            SupplierId = bill.SupplierId,
                            Crates = l.Crates,
                            WeightKg = l.WeightKg,
                            RatePerKg = l.RatePerKg,
                            Amount = Math.Round(l.Amount, 0),
                            CreatedOn = DateTime.Now,
                            CreatedBy = currentUser,
                            Active = true
                        });
                    }

                    _context.BillMasters.Update(bill);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // Create New Bill
                    bill = new BillMaster
                    {
                        BillNo = model.BillNo,
                        BillDate = billDate,
                        SupplierId = (model.SelectedSupplierId.HasValue && model.SelectedSupplierId.Value > 0) ? model.SelectedSupplierId : null,
                        CustomerId = primaryCustomerId,
                        CommissionPercent = commPct,
                        CommissionAmount = commAmt,
                        LabourExpense = model.LabourExpense,
                        IceExpense = model.IceExpense,
                        FarmerIceExpense = model.FarmerIceExpense,
                        FreightExpense = model.FreightExpense,
                        OtherExpense = model.OtherExpense,
                        TotalExpense = totalExp,
                        Deposit = depositAmt,
                        TotalGrossAmount = totalGross,
                        NetPayable = netPayable,
                        Status = string.IsNullOrWhiteSpace(model.Status) ? "Due" : model.Status,
                        Remarks = model.Remarks,
                        CompanyId = companyId,
                        CreatedOn = DateTime.Now,
                        CreatedBy = currentUser,
                        Active = true
                    };

                    foreach (var l in validLots)
                    {
                        bill.Details.Add(new BillDetail
                        {
                            LotNo = string.IsNullOrWhiteSpace(l.LotNo) ? "LOT" : l.LotNo.Trim(),
                            FishId = (l.FishId.HasValue && l.FishId.Value > 0) ? l.FishId : null,
                            CustomerId = (l.CustomerId.HasValue && l.CustomerId.Value > 0) ? l.CustomerId : null,
                            SupplierId = bill.SupplierId,
                            Crates = l.Crates,
                            WeightKg = l.WeightKg,
                            RatePerKg = l.RatePerKg,
                            Amount = Math.Round(l.Amount, 0),
                            CreatedOn = DateTime.Now,
                            CreatedBy = currentUser,
                            Active = true
                        });
                    }

                    await _context.BillMasters.AddAsync(bill);
                    await _context.SaveChangesAsync();
                }

                // =========================================================================
                // 3. AUTOMATIC DOUBLE-ENTRY ACCOUNTING VOUCHER POSTING
                // =========================================================================
                var postedVoucher = await PostBillAccountingVoucherAsync(bill, validLots, companyId, fyId, userId, currentUser);
                string vNoMsg = postedVoucher != null ? $" | Main Voucher: {postedVoucher.VoucherNo}" : "";

                _logger.LogInformation("Bill {BillNo} (ID: {Id}) and Accounting Entries saved successfully", bill.BillNo, bill.Id);
                TempData["SuccessMessage"] = $"Bill '{bill.BillNo}' saved successfully! Gross (Receivables): Rs {totalGross:N0}, Payable: Rs {netPayable:N0}, Commission: Rs {commAmt:N0}{vNoMsg}.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving bill {BillNo}", model.BillNo);
                TempData["ErrorMessage"] = "Failed to save bill: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 3. GET BILL JSON BY ID (FOR PRINTING & VIEWING)
        // =========================================================================
        [HttpGet]
        [Route("GetBillJson/{id:int}")]
        public async Task<IActionResult> GetBillJson(int id)
        {
            try
            {
                var bill = await _context.BillMasters
                    .Include(b => b.Supplier)
                    .Include(b => b.Details)
                        .ThenInclude(d => d.Fish)
                    .Include(b => b.Details)
                        .ThenInclude(d => d.Customer)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == id);

                if (bill == null)
                {
                    return Json(new { success = false, message = "Bill not found." });
                }

                var voucher = await _context.Vouchers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.InvoiceId == bill.Id || v.InvoiceNo == bill.BillNo);

                var billVouchers = await _context.Vouchers
                    .AsNoTracking()
                    .Where(v => (v.InvoiceId == bill.Id || (!string.IsNullOrEmpty(bill.BillNo) && v.InvoiceNo == bill.BillNo)) &&
                                (v.VoucherType == "CP" || v.VoucherType == "BP" || v.VoucherType == "CPV" || v.VoucherType == "BPV") &&
                                (v.DeleteStatus == 0 || v.DeleteStatus == null) &&
                                (bill.SupplierId == null || v.VendorId == bill.SupplierId))
                    .ToListAsync();

                decimal paidToSupplier = billVouchers
                    .Where(v => !IsFarmerExpenseVoucher(v))
                    .Sum(v => v.Amount);
                string calculatedStatus = "Due";
                if (paidToSupplier >= bill.NetPayable && bill.NetPayable > 0)
                {
                    calculatedStatus = "Paid";
                }
                else if (paidToSupplier > 0)
                {
                    calculatedStatus = "Partial";
                }
                else
                {
                    calculatedStatus = (bill.Status == "Paid" || bill.Status == "Partial") && paidToSupplier == 0 ? "Due" : (bill.Status ?? "Due");
                }

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        id = bill.Id,
                        billNo = bill.BillNo,
                        voucherNo = voucher?.VoucherNo ?? "",
                        date = bill.BillDate.ToString("yyyy-MM-dd"),
                        dateFormatted = bill.BillDate.ToString("dd-MMM-yyyy"),
                        supplierId = bill.SupplierId,
                        supplierName = bill.Supplier?.Name ?? "",
                        commissionPercent = bill.CommissionPercent,
                        commissionAmount = bill.CommissionAmount,
                        labourExpense = bill.LabourExpense,
                        iceExpense = bill.IceExpense,
                        farmerIceExpense = bill.FarmerIceExpense,
                        freightExpense = bill.FreightExpense,
                        otherExpense = bill.OtherExpense,
                        totalExpense = bill.TotalExpense,
                        deposit = bill.Deposit,
                        totalGrossAmount = bill.TotalGrossAmount,
                        netPayable = bill.NetPayable,
                        paidToSupplier = paidToSupplier,
                        status = calculatedStatus,
                        remarks = bill.Remarks ?? "",
                        lots = bill.Details.Select((d, idx) => new
                        {
                            id = d.Id,
                            lotNo = (!string.IsNullOrWhiteSpace(d.LotNo) && d.LotNo != "LOT") ? d.LotNo : $"LOT-{1000 + idx + 1}",
                            fishId = d.FishId,
                            fishName = d.Fish?.Name ?? "Fish",
                            customerId = d.CustomerId,
                            customerName = d.Customer?.Name ?? "",
                            crates = d.Crates,
                            weightStr = BillLotItemDto.FormatKgToMaund(d.WeightKg),
                            weightKg = d.WeightKg,
                            ratePerKg = d.RatePerKg,
                            amount = d.Amount
                        }).ToList()
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting bill JSON for ID {Id}", id);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================================================================
        // 4. GET NEXT BILL NUMBER (AJAX HELPER)
        // =========================================================================
        [HttpGet]
        [Route("GetNextBillNo")]
        public async Task<IActionResult> GetNextBillNo()
        {
            try
            {
                string nextNo = await GenerateNextBillNoAsync();
                return Json(new { success = true, nextBillNo = nextNo });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================================================================
        // 5. EDIT BILL (LOAD DATA INTO FORM)
        // =========================================================================
        [HttpGet]
        [Route("Edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            ViewData["Title"] = "Edit Bill";
            ViewData["SubTitle"] = "Update bill lots, rates, and expenses.";
            ViewData["ActivePage"] = "NewBill";

            try
            {
                var fishes = await _context.Fishes
                    .AsNoTracking()
                    .Where(f => f.Active == true || f.Active == null)
                    .OrderBy(f => f.Name)
                    .Select(f => new FishDropdownDto
                    {
                        Id = f.Id,
                        Code = f.Code ?? "",
                        Name = f.Name,
                        Type = f.Type,
                        Unit = f.Unit ?? "Kg"
                    })
                    .ToListAsync();

                var suppliers = await _context.Vendors
                    .AsNoTracking()
                    .Where(v => v.VendorType == VendorType.Supplier && (v.Status == "Active" || v.Active == true || v.Active == null))
                    .OrderBy(v => v.Name)
                    .Select(v => new VendorDropdownDto
                    {
                        Id = v.Id,
                        Name = v.Name,
                        Phone = v.Phone,
                        City = v.City
                    })
                    .ToListAsync();

                var customers = await _context.Vendors
                    .AsNoTracking()
                    .Where(v => v.VendorType == VendorType.Customer && (v.Status == "Active" || v.Active == true || v.Active == null))
                    .OrderBy(v => v.Name)
                    .Select(v => new VendorDropdownDto
                    {
                        Id = v.Id,
                        Name = v.Name,
                        Phone = v.Phone,
                        City = v.City
                    })
                    .ToListAsync();

                var bill = await _context.BillMasters
                    .Include(b => b.Supplier)
                    .Include(b => b.Details)
                        .ThenInclude(d => d.Fish)
                    .Include(b => b.Details)
                        .ThenInclude(d => d.Customer)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == id);

                if (bill == null)
                {
                    TempData["ErrorMessage"] = "Bill record not found.";
                    return RedirectToAction(nameof(Index));
                }

                var rawBills = await _context.BillMasters
                    .Include(b => b.Supplier)
                    .Include(b => b.Details)
                        .ThenInclude(d => d.Fish)
                    .Include(b => b.Details)
                        .ThenInclude(d => d.Customer)
                    .AsNoTracking()
                    .OrderByDescending(b => b.BillDate)
                    .ThenByDescending(b => b.Id)
                    .Take(50)
                    .ToListAsync();

                var billIds = rawBills.Select(b => b.Id).ToHashSet();
                var billNos = rawBills.Select(b => b.BillNo).Where(n => !string.IsNullOrEmpty(n)).ToHashSet();
                var vouchers = await _context.Vouchers
                    .AsNoTracking()
                    .Where(v => v.CompanyId == null || v.CompanyId == (bill.CompanyId ?? 1))
                    .OrderByDescending(v => v.VoucherId)
                    .Take(200)
                    .ToListAsync();

                var mainVoucherLookup = vouchers
                    .Where(v => (v.VoucherType == "JV" || (v.VoucherNo != null && v.VoucherNo.StartsWith("JV-"))) &&
                                ((v.InvoiceId.HasValue && billIds.Contains(v.InvoiceId.Value)) ||
                                 (!string.IsNullOrEmpty(v.InvoiceNo) && billNos.Contains(v.InvoiceNo))))
                    .GroupBy(v => v.InvoiceNo ?? (v.InvoiceId?.ToString() ?? ""))
                    .ToDictionary(g => g.Key, g => g.First().VoucherNo);

                var expVoucherLookup = vouchers
                    .Where(v => (v.VoucherType == "CP" || v.VoucherType == "CPV" || (v.VoucherNo != null && v.VoucherNo.StartsWith("CPV-"))) &&
                                IsFarmerExpenseVoucher(v) &&
                                ((v.InvoiceId.HasValue && billIds.Contains(v.InvoiceId.Value)) ||
                                 (!string.IsNullOrEmpty(v.InvoiceNo) && billNos.Contains(v.InvoiceNo))))
                    .GroupBy(v => v.InvoiceNo ?? (v.InvoiceId?.ToString() ?? ""))
                    .ToDictionary(g => g.Key, g => g.First().VoucherNo);

                var supplierPaymentVouchers = vouchers
                    .Where(v => (v.VoucherType == "CP" || v.VoucherType == "BP" || v.VoucherType == "CPV" || v.VoucherType == "BPV") &&
                                (v.DeleteStatus == 0 || v.DeleteStatus == null) &&
                                !IsFarmerExpenseVoucher(v))
                    .ToList();

                var customerReceiptVouchers = vouchers
                    .Where(v => (v.VoucherType == "CR" || v.VoucherType == "BR" || v.VoucherType == "CRV" || v.VoucherType == "BRV") &&
                                (v.DeleteStatus == 0 || v.DeleteStatus == null))
                    .ToList();

                var recentBills = rawBills.Select(b =>
                {
                    var paidToSupplier = supplierPaymentVouchers
                        .Where(v => (v.InvoiceId == b.Id || (!string.IsNullOrEmpty(b.BillNo) && v.InvoiceNo == b.BillNo)) &&
                                    (b.SupplierId == null || v.VendorId == b.SupplierId))
                        .Sum(v => v.Amount);

                    string calculatedStatus = "Due";
                    if (paidToSupplier >= b.NetPayable && b.NetPayable > 0)
                    {
                        calculatedStatus = "Paid";
                    }
                    else if (paidToSupplier > 0)
                    {
                        calculatedStatus = "Partial";
                    }
                    else
                    {
                        calculatedStatus = (b.Status == "Paid" || b.Status == "Partial") && paidToSupplier == 0 ? "Due" : (b.Status ?? "Due");
                    }

                    return new BillSummaryDto
                    {
                        Id = b.Id,
                        BillNo = b.BillNo,
                        VoucherNo = (!string.IsNullOrEmpty(b.BillNo) && mainVoucherLookup.ContainsKey(b.BillNo)) ? mainVoucherLookup[b.BillNo] : null,
                        ExpenseVoucherNo = (!string.IsNullOrEmpty(b.BillNo) && expVoucherLookup.ContainsKey(b.BillNo)) ? expVoucherLookup[b.BillNo] : null,
                        Date = b.BillDate.ToString("dd-MMM-yyyy"),
                        SupplierId = b.SupplierId,
                        SupplierName = b.Supplier?.Name ?? "General Boat Catch",
                        LotsCount = b.Details.Count,
                        TotalSale = b.TotalGrossAmount,
                        Commission = b.CommissionAmount,
                        CommissionPercent = b.CommissionPercent,
                        LabourExpense = b.LabourExpense,
                        IceExpense = b.IceExpense,
                        FarmerIceExpense = b.FarmerIceExpense,
                        FreightExpense = b.FreightExpense,
                        OtherExpense = b.OtherExpense,
                        Expenses = b.TotalExpense,
                        Deposit = b.Deposit,
                        NetPayable = Math.Round(b.NetPayable, 0),
                        PaidToSupplier = paidToSupplier,
                        Status = calculatedStatus,
                        Remarks = b.Remarks ?? "",
                        Lots = b.Details.Select(d =>
                        {
                            var lotReceived = customerReceiptVouchers
                                .Where(v => IsLotReceiptVoucher(v, d, b))
                                .Sum(v => v.Amount);

                            return new BillLotItemDto
                            {
                                Id = d.Id,
                                LotNo = d.LotNo ?? $"LOT-{d.Id}",
                                FishId = d.FishId,
                                FishName = d.Fish?.Name ?? "Fish",
                                CustomerId = d.CustomerId,
                                CustomerName = d.Customer?.Name ?? "General Buyer",
                                Crates = d.Crates,
                                WeightStr = BillLotItemDto.FormatKgToMaund(d.WeightKg),
                                WeightKg = d.WeightKg,
                                RatePerKg = d.RatePerKg,
                                Amount = d.Amount,
                                ReceivedAmount = lotReceived
                            };
                        }).ToList()
                    };
                }).ToList();

                var model = new NewBillViewModel
                {
                    Id = bill.Id,
                    BillNo = bill.BillNo,
                    Date = bill.BillDate.ToString("yyyy-MM-dd"),
                    SelectedSupplierId = bill.SupplierId,
                    SelectedSupplier = bill.Supplier?.Name ?? "",
                    CommissionPercent = bill.CommissionPercent,
                    CommissionAmount = bill.CommissionAmount,
                    LabourExpense = bill.LabourExpense,
                    IceExpense = bill.IceExpense,
                    FarmerIceExpense = bill.FarmerIceExpense,
                    FreightExpense = bill.FreightExpense,
                    OtherExpense = bill.OtherExpense,
                    TotalExpense = bill.TotalExpense,
                    Deposit = bill.Deposit,
                    TotalGrossAmount = bill.TotalGrossAmount,
                    NetPayable = Math.Round(bill.NetPayable, 0),
                    Status = bill.Status,
                    Remarks = bill.Remarks,
                    Lots = bill.Details.Select(d => new BillLotItemDto
                    {
                        Id = d.Id,
                        LotNo = d.LotNo ?? "",
                        FishId = d.FishId,
                        FishName = d.Fish?.Name ?? "",
                        CustomerId = d.CustomerId,
                        CustomerName = d.Customer?.Name ?? "",
                        Crates = d.Crates,
                        WeightStr = BillLotItemDto.FormatKgToMaund(d.WeightKg),
                        WeightKg = d.WeightKg,
                        RatePerKg = d.RatePerKg,
                        Amount = d.Amount
                    }).ToList(),
                    RecentBills = recentBills,
                    FishList = fishes,
                    SuppliersList = suppliers,
                    CustomersList = customers,
                    CashAndBankAccounts = await GetCashAndBankAccountsAsync(bill.CompanyId ?? 1)
                };

                return View("Index", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading bill for editing ID {Id}", id);
                TempData["ErrorMessage"] = "Failed to load bill: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // =========================================================================
        // 6. DELETE BILL (WITH LINKED ACCOUNTING VOUCHER REMOVAL)
        // =========================================================================
        [HttpPost]
        [Route("DeleteBill/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBill(int id)
        {
            try
            {
                var bill = await _context.BillMasters
                    .Include(b => b.Details)
                    .FirstOrDefaultAsync(b => b.Id == id);

                if (bill == null)
                {
                    TempData["ErrorMessage"] = "Bill record not found.";
                    return RedirectToAction(nameof(Index));
                }

                // 1. Remove linked Voucher & AccountTransactions
                var linkedVouchers = await _context.Vouchers
                    .Include(v => v.AccountTransactions)
                    .Where(v => v.InvoiceId == id || (v.InvoiceNo != null && v.InvoiceNo == bill.BillNo))
                    .ToListAsync();

                if (linkedVouchers.Any())
                {
                    foreach (var v in linkedVouchers)
                    {
                        if (v.AccountTransactions != null && v.AccountTransactions.Any())
                        {
                            _context.AccountTransactions.RemoveRange(v.AccountTransactions);
                        }
                    }
                    _context.Vouchers.RemoveRange(linkedVouchers);
                }

                // 2. Remove bill details & bill master
                _context.BillDetails.RemoveRange(bill.Details);
                _context.BillMasters.Remove(bill);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Bill {BillNo} (ID: {Id}) and associated Accounting Voucher(s) deleted successfully", bill.BillNo, id);
                TempData["SuccessMessage"] = $"Bill '{bill.BillNo}' and its accounting voucher deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting bill ID {Id}", id);
                TempData["ErrorMessage"] = "Failed to delete bill: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 7. GET CASH / BANK ACCOUNTS (AJAX HELPER)
        // =========================================================================
        [HttpGet]
        [Route("GetPaymentAccounts")]
        public async Task<IActionResult> GetPaymentAccounts()
        {
            try
            {
                int companyId = GetCurrentCompanyId();
                var accounts = await GetCashAndBankAccountsAsync(companyId);
                return Json(new { success = true, data = accounts });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================================================================
        // 8. POST PARTY PAYMENT (PAY SUPPLIER / RECEIVE FROM CUSTOMER)
        // Stores in Voucher and AccountTransaction tables with balanced double-entry
        // =========================================================================
        [HttpPost]
        [Route("PostPartyPayment")]
        public async Task<IActionResult> PostPartyPayment([FromBody] BillPaymentRequestDto request)
        {
            try
            {
                if (request == null)
                    return Json(new { success = false, message = "Invalid payment data supplied." });

                if (request.Amount <= 0)
                    return Json(new { success = false, message = "Payment amount must be greater than zero." });

                int companyId = GetCurrentCompanyId();
                int fyId = 1;
                int? userId = GetCurrentUserId();
                string currentUser = User?.Identity?.Name ?? "Admin";

                //await EnsureCoreChartOfAccountsAsync(companyId);
                //await EnsureDefaultBankMastersAsync(companyId);

                bool isSupplier = string.Equals(request.PartyType, "Supplier", StringComparison.OrdinalIgnoreCase);
                bool isBank = string.Equals(request.PaymentMode, "Bank", StringComparison.OrdinalIgnoreCase);

                // --- DUPLICATE / FULL PAYMENT RESTRICTION CHECKS ---
                if (isSupplier)
                {
                    var bill = await _context.BillMasters
                        .Include(b => b.Details)
                        .FirstOrDefaultAsync(b => (request.BillId > 0 && b.Id == request.BillId) || (!string.IsNullOrEmpty(request.BillNo) && b.BillNo == request.BillNo));

                    if (bill != null)
                    {
                        var existingPaid = await _context.Vouchers
                            .Where(v => (v.InvoiceId == bill.Id || v.InvoiceNo == bill.BillNo) &&
                                        (v.VoucherType == "CP" || v.VoucherType == "BP" || v.VoucherType == "CPV" || v.VoucherType == "BPV") &&
                                        (v.DeleteStatus == 0 || v.DeleteStatus == null) &&
                                        (v.InvoiceRefNo == null || v.InvoiceRefNo != "EXPENSE") &&
                                        (v.Narration == null || (!v.Narration.Contains("Farmer Expenses") && !v.Narration.Contains("Expenses Voucher"))))
                            .SumAsync(v => (decimal?)v.Amount) ?? 0;

                        decimal remainingPayable = Math.Round(Math.Max(0, bill.NetPayable - existingPaid), 0);
                        decimal roundedNetPayable = Math.Round(bill.NetPayable, 0);

                        if (existingPaid >= roundedNetPayable && roundedNetPayable > 0)
                        {
                            return Json(new
                            {
                                success = false,
                                message = $"Bill #{bill.BillNo} ki full payment (Rs {existingPaid:N0}) pehle hi ada ho chuki he. Dubara payment allow nahi he."
                            });
                        }

                        if (request.Amount > remainingPayable + 0.5m)
                        {
                            return Json(new
                            {
                                success = false,
                                message = $"Payment amount (Rs {request.Amount:N0}) baqaya payable raqam (Rs {remainingPayable:N0}) se zyada he. Sirf Rs {remainingPayable:N0} tak payment ki ja sakti he."
                            });
                        }
                    }
                }
                else
                {
                    // Customer Receipt Duplicate / Full Payment Check
                    var bill = await _context.BillMasters
                        .Include(b => b.Details)
                        .FirstOrDefaultAsync(b => (request.BillId > 0 && b.Id == request.BillId) || (!string.IsNullOrEmpty(request.BillNo) && b.BillNo == request.BillNo));

                    if (bill != null)
                    {
                        BillDetail? targetLot = null;
                        if (request.LotId.HasValue && request.LotId.Value > 0)
                        {
                            targetLot = bill.Details.FirstOrDefault(d => d.Id == request.LotId.Value);
                        }
                        else if (!string.IsNullOrWhiteSpace(request.LotNo))
                        {
                            targetLot = bill.Details.FirstOrDefault(d => d.LotNo == request.LotNo);
                        }

                        if (targetLot != null)
                        {
                            var billReceiptVouchers = await _context.Vouchers
                                .Where(v => (v.InvoiceId == bill.Id || (!string.IsNullOrEmpty(bill.BillNo) && v.InvoiceNo == bill.BillNo)) &&
                                            (v.VoucherType == "CR" || v.VoucherType == "BR" || v.VoucherType == "CRV" || v.VoucherType == "BRV") &&
                                            (v.DeleteStatus == 0 || v.DeleteStatus == null))
                                .ToListAsync();

                            decimal existingReceipts = billReceiptVouchers
                                .Where(v => IsLotReceiptVoucher(v, targetLot, bill))
                                .Sum(v => v.Amount);

                            decimal remainingReceivable = Math.Max(0, targetLot.Amount - existingReceipts);

                            if (existingReceipts >= targetLot.Amount && targetLot.Amount > 0)
                            {
                                return Json(new
                                {
                                    success = false,
                                    message = $"Lot {targetLot.LotNo} ki full payment (Rs {existingReceipts:N0}) pehle hi receive ho chuki he. Dubara receipt allow nahi he."
                                });
                            }

                            if (request.Amount > remainingReceivable)
                            {
                                return Json(new
                                {
                                    success = false,
                                    message = $"Receipt amount (Rs {request.Amount:N0}) baqaya receivable (Rs {remainingReceivable:N0}) se zyada he. Sirf Rs {remainingReceivable:N0} tak receive kiya ja sakta he."
                                });
                            }
                        }
                        else if (request.PartyId > 0)
                        {
                            decimal custTotal = bill.Details.Where(d => d.CustomerId == request.PartyId).Sum(d => d.Amount);

                            var existingReceipts = await _context.Vouchers
                                .Where(v => (v.InvoiceId == bill.Id || v.InvoiceNo == bill.BillNo) &&
                                            (v.VoucherType == "CR" || v.VoucherType == "BR" || v.VoucherType == "CRV" || v.VoucherType == "BRV") &&
                                            (v.DeleteStatus == 0 || v.DeleteStatus == null) &&
                                            v.VendorId == request.PartyId)
                                .SumAsync(v => (decimal?)v.Amount) ?? 0;

                            decimal remainingReceivable = Math.Max(0, custTotal - existingReceipts);

                            if (existingReceipts >= custTotal && custTotal > 0)
                            {
                                return Json(new
                                {
                                    success = false,
                                    message = $"Customer ({request.PartyName}) ki is bill se full amount (Rs {existingReceipts:N0}) pehle hi receive ho chuki he. Dubara receipt allow nahi he."
                                });
                            }

                            if (request.Amount > remainingReceivable)
                            {
                                return Json(new
                                {
                                    success = false,
                                    message = $"Receipt amount (Rs {request.Amount:N0}) baqaya receivable (Rs {remainingReceivable:N0}) se zyada he. Sirf Rs {remainingReceivable:N0} tak receive kiya ja sakta he."
                                });
                            }
                        }
                    }
                }

                // 1. Resolve BankMaster and Chart of Account
                ChartOfAccount? cashBankCoa = null;
                ERP_Core.Models.BankMaster? selectedBankMaster = null;

                if (request.PaymentAccountId > 0)
                {
                    selectedBankMaster = await _context.BankMasters
                        .Include(b => b.ChartOfAccount)
                        .FirstOrDefaultAsync(b => b.Id == request.PaymentAccountId);

                    if (selectedBankMaster != null)
                    {
                        isBank = selectedBankMaster.IsBank;
                        if (selectedBankMaster.ChartOfAccountId.HasValue && selectedBankMaster.ChartOfAccountId.Value > 0)
                        {
                            cashBankCoa = selectedBankMaster.ChartOfAccount ?? await _context.ChartOfAccounts.FirstOrDefaultAsync(c => c.Id == selectedBankMaster.ChartOfAccountId.Value);
                        }
                    }
                    else
                    {
                        cashBankCoa = await _context.ChartOfAccounts.FirstOrDefaultAsync(c => c.Id == request.PaymentAccountId);
                    }
                }

                if (cashBankCoa == null)
                {
                    cashBankCoa = await _context.ChartOfAccounts.FirstOrDefaultAsync(c => isBank 
                        ? (c.AccountCode == "1002" || c.AccountName.Contains("Bank")) 
                        : (c.AccountCode == "1001" || c.AccountName.Contains("Cash")));
                }

                if (cashBankCoa == null)
                {
                    return Json(new { success = false, message = "Selected Cash/Bank account was not found in Chart of Accounts." });
                }

                // 2. Determine Voucher Type:
                // Supplier: CP (Cash Payment) or BP (Bank Payment)
                // Customer: CR (Cash Receipt) or BR (Bank Receipt)
                string voucherType = isSupplier 
                    ? (isBank ? "BP" : "CP") 
                    : (isBank ? "BR" : "CR");

                string nextVoucherNo = await _unitOfWork.GenerateVoucherNoAsync(voucherType, fyId, companyId);

                // 3. Resolve Party (Vendor) Chart of Account
                ChartOfAccount? partyCoa = null;
                if (request.PartyId > 0)
                {
                    var vendor = await _context.Vendors
                        .Include(v => v.ChartOfAccount)
                        .FirstOrDefaultAsync(v => v.Id == request.PartyId);

                    if (vendor != null)
                    {
                        var debtorNature = await _context.NatureofAccounts.FirstOrDefaultAsync(n => n.DebtorOrCreditor == "D");
                        var creditorNature = await _context.NatureofAccounts.FirstOrDefaultAsync(n => n.DebtorOrCreditor == "C");
                        var setup = await _context.AccountSetups.AsNoTracking().FirstOrDefaultAsync(s => s.CompanyId == companyId)
                            ?? await _context.AccountSetups.AsNoTracking().FirstOrDefaultAsync();

                        int? parentAccId = isSupplier ? setup?.SupplierAccID : setup?.CustomerAccID;
                        ChartOfAccount? parentAcc = null;
                        if (parentAccId.HasValue && parentAccId.Value > 0)
                        {
                            parentAcc = await _context.ChartOfAccounts.FindAsync(parentAccId.Value);
                        }
                        if (parentAcc == null)
                        {
                            parentAcc = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => isSupplier 
                                ? (a.AccountCode == "0201001" || a.AccountCode == "2001" || a.AccountName.ToLower().Contains("supplier payable")) 
                                : (a.AccountCode == "0101003" || a.AccountCode == "1003" || a.AccountName.ToLower().Contains("customer receivable")));
                        }
                        var allCoas = await _context.ChartOfAccounts.Where(a => a.CompanyId == null || a.CompanyId == companyId).ToListAsync();

                        partyCoa = await GetOrCreateVendorAccountAsync(vendor, isSupplier, debtorNature, creditorNature, parentAcc, allCoas, companyId, currentUser);
                    }
                }

                if (partyCoa == null)
                {
                    string defaultCode = isSupplier ? "0201001" : "0101003";
                    partyCoa = await _context.ChartOfAccounts.FirstOrDefaultAsync(c => c.AccountCode == defaultCode)
                        ?? new ChartOfAccount { Id = 0, AccountName = request.PartyName, AccountCode = defaultCode };
                }

                string entryTypeName = isSupplier 
                    ? (isBank ? "Bank Payment" : "Cash Payment")
                    : (isBank ? "Bank Receipt" : "Cash Receipt");

                string partyTitle = isSupplier ? "Supplier" : "Customer";
                string narration = !string.IsNullOrWhiteSpace(request.Narration)
                    ? request.Narration
                    : (isSupplier 
                        ? $"Payment to {partyTitle} {partyCoa.AccountName} against Bill #{request.BillNo} via {cashBankCoa.AccountName}"
                        : $"Receipt from {partyTitle} {partyCoa.AccountName} against Bill #{request.BillNo} into {cashBankCoa.AccountName}");

                // 4. Create Voucher Master
                var voucher = new Voucher
                {
                    VoucherNo = nextVoucherNo,
                    VoucherDate = request.PaymentDate,
                    VoucherType = voucherType,
                    Amount = request.Amount,
                    VendorId = request.PartyId > 0 ? request.PartyId : null,
                    InvoiceId = request.BillId > 0 ? request.BillId : null,
                    InvoiceNo = request.BillNo,
                    InvoiceRefNo = (!isSupplier && !string.IsNullOrWhiteSpace(request.LotNo)) ? request.LotNo.Trim() : null,
                    Narration = narration,
                    CompanyId = companyId,
                    FinancialYearID = fyId,
                    UserId = userId,
                    Username = currentUser,
                    ApprovalStatus = "Approved",
                    DeleteStatus = 0,
                    CreatedBy = currentUser,
                    CreatedOn = DateTime.Now
                };

                await _context.Vouchers.AddAsync(voucher);
                await _context.SaveChangesAsync(); // generates VoucherId

                // 5. Create Balanced Account Transactions
                var transactions = new List<AccountTransaction>();

                if (isSupplier)
                {
                    // DEBIT: Supplier Payable (Liability decreases)
                    transactions.Add(new AccountTransaction
                    {
                        VoucherId = voucher.VoucherId,
                        VoucherNo = voucher.VoucherNo,
                        ChartofAccountId = partyCoa.Id > 0 ? partyCoa.Id : null,
                        ChartofAccountName = partyCoa.AccountName,
                        Code = partyCoa.AccountCode ?? "",
                        AccountName = partyCoa.AccountName,
                        Debit = request.Amount,
                        Credit = 0,
                        Amount = request.Amount,
                        InvoiceNo = request.BillNo,
                        Narration = narration,
                        EntryTypeName = entryTypeName,
                        Type = "Supplier Payment",
                        ChequeNo = request.ChequeNo,
                        ExtraDate = request.PaymentDate,
                        CompanyId = companyId,
                        UserId = userId
                    });

                    // CREDIT: Cash/Bank Account (Asset decreases)
                    transactions.Add(new AccountTransaction
                    {
                        VoucherId = voucher.VoucherId,
                        VoucherNo = voucher.VoucherNo,
                        ChartofAccountId = cashBankCoa.Id,
                        ChartofAccountName = cashBankCoa.AccountName,
                        Code = cashBankCoa.AccountCode ?? "",
                        AccountName = cashBankCoa.AccountName,
                        Debit = 0,
                        Credit = request.Amount,
                        Amount = request.Amount,
                        InvoiceNo = request.BillNo,
                        Narration = narration,
                        EntryTypeName = entryTypeName,
                        Type = "Cash/Bank",
                        ChequeNo = request.ChequeNo,
                        ExtraDate = request.PaymentDate,
                        CompanyId = companyId,
                        UserId = userId
                    });
                }
                else
                {
                    // CUSTOMER RECEIPT
                    // DEBIT: Cash/Bank Account (Asset increases)
                    transactions.Add(new AccountTransaction
                    {
                        VoucherId = voucher.VoucherId,
                        VoucherNo = voucher.VoucherNo,
                        ChartofAccountId = cashBankCoa.Id,
                        ChartofAccountName = cashBankCoa.AccountName,
                        Code = cashBankCoa.AccountCode ?? "",
                        AccountName = cashBankCoa.AccountName,
                        Debit = request.Amount,
                        Credit = 0,
                        Amount = request.Amount,
                        InvoiceNo = request.BillNo,
                        Narration = narration,
                        EntryTypeName = entryTypeName,
                        Type = "Cash/Bank",
                        ChequeNo = request.ChequeNo,
                        ExtraDate = request.PaymentDate,
                        CompanyId = companyId,
                        UserId = userId
                    });

                    // CREDIT: Customer Receivable (Asset decreases)
                    transactions.Add(new AccountTransaction
                    {
                        VoucherId = voucher.VoucherId,
                        VoucherNo = voucher.VoucherNo,
                        ChartofAccountId = partyCoa.Id > 0 ? partyCoa.Id : null,
                        ChartofAccountName = partyCoa.AccountName,
                        Code = partyCoa.AccountCode ?? "",
                        AccountName = partyCoa.AccountName,
                        Debit = 0,
                        Credit = request.Amount,
                        Amount = request.Amount,
                        InvoiceNo = request.BillNo,
                        Narration = narration,
                        EntryTypeName = entryTypeName,
                        Type = "Customer Receipt",
                        ChequeNo = request.ChequeNo,
                        ExtraDate = request.PaymentDate,
                        CompanyId = companyId,
                        UserId = userId
                    });
                }

                await _context.AccountTransactions.AddRangeAsync(transactions);
                await _context.SaveChangesAsync();

                // 6. Update Bill Status if Supplier was paid
                string updatedStatus = "Due";
                if (request.BillId > 0)
                {
                    var bill = await _context.BillMasters.FirstOrDefaultAsync(b => b.Id == request.BillId);
                    if (bill != null)
                    {
                        if (isSupplier)
                        {
                            // Sum total payments to supplier for this bill (excluding automatic farmer expense vouchers)
                            var paidVouchers = await _context.Vouchers
                                .Where(v => (v.InvoiceId == bill.Id || v.InvoiceNo == bill.BillNo) &&
                                            (v.VoucherType == "CP" || v.VoucherType == "BP" || v.VoucherType == "CPV" || v.VoucherType == "BPV") &&
                                            (v.DeleteStatus == 0 || v.DeleteStatus == null) &&
                                            (v.InvoiceRefNo == null || v.InvoiceRefNo != "EXPENSE") &&
                                            (v.Narration == null || (!v.Narration.Contains("Farmer Expenses") && !v.Narration.Contains("Expenses Voucher"))))
                                .SumAsync(v => (decimal?)v.Amount) ?? 0;

                            if (paidVouchers >= Math.Round(bill.NetPayable, 0) && bill.NetPayable > 0)
                            {
                                bill.Status = "Paid";
                            }
                            else if (paidVouchers > 0)
                            {
                                bill.Status = "Partial";
                            }
                            else
                            {
                                bill.Status = "Due";
                            }
                            _context.BillMasters.Update(bill);
                            await _context.SaveChangesAsync();
                            updatedStatus = bill.Status;
                        }
                    }
                }

                _logger.LogInformation("Party Payment Voucher '{VoucherNo}' ({VoucherType}) posted for {PartyType} '{PartyName}' (Rs {Amount:N0}).",
                    voucher.VoucherNo, voucher.VoucherType, request.PartyType, request.PartyName, request.Amount);

                return Json(new
                {
                    success = true,
                    message = $"{(isSupplier ? "Payment" : "Receipt")} Voucher '{voucher.VoucherNo}' posted successfully in Accounts!",
                    voucherNo = voucher.VoucherNo,
                    voucherId = voucher.VoucherId,
                    billStatus = updatedStatus
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting party payment: {Message}", ex.Message);
                return Json(new { success = false, message = "An error occurred while posting payment: " + ex.Message });
            }
        }

        // =========================================================================
        // QUICK CREATE: SUPPLIER, CUSTOMER & FISH (MODAL AJAX HELPERS)
        // =========================================================================
        public class QuickSupplierRequest
        {
            public string? Name { get; set; }
            public string? Phone { get; set; }
            public string? VillageOrJetty { get; set; }
            public string? AccountNo { get; set; }
        }

        public class QuickCustomerRequest
        {
            public string? Name { get; set; }
            public string? Phone { get; set; }
            public string? City { get; set; }
            public string? Cnic { get; set; }
        }

        public class QuickFishRequest
        {
            public string? Name { get; set; }
            public string? Code { get; set; }
            public string? Type { get; set; }
            public string? Unit { get; set; }
        }

        [HttpPost]
        [Route("QuickCreateSupplier")]
        public async Task<IActionResult> QuickCreateSupplier([FromBody] QuickSupplierRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Name))
            {
                return Json(new { success = false, message = "Supplier / Boat Name is required." });
            }

            try
            {
                int companyId = GetCurrentCompanyId();
                var vendorService = HttpContext.RequestServices.GetRequiredService<IVendorService>();

                var vendor = new Vendor
                {
                    VendorType = VendorType.Supplier,
                    Name = request.Name.Trim(),
                    Phone = request.Phone?.Trim(),
                    City = request.VillageOrJetty?.Trim(),
                    AccountNo = request.AccountNo?.Trim(),
                    OpeningBalance = 0,
                    Status = "Active",
                    CompanyId = companyId,
                    Active = true,
                    ParentSupplierId = null
                };

                var created = await vendorService.CreateVendorAsync(vendor, User.Identity?.Name ?? "System");
                // Use UpdateVendorAsync to generate code and synchronize vendor COA account
                created = await vendorService.UpdateVendorAsync(created, User.Identity?.Name ?? "System");

                string code = created.ChartOfAccount?.AccountCode ?? created.AccountNo ?? "";
                string displayName = !string.IsNullOrWhiteSpace(code)
                    ? (!string.IsNullOrWhiteSpace(created.City) ? $"[{code}] {created.Name} ({created.City})" : $"[{code}] {created.Name}")
                    : (!string.IsNullOrWhiteSpace(created.City) ? $"{created.Name} ({created.City})" : created.Name);

                return Json(new
                {
                    success = true,
                    id = created.Id,
                    name = created.Name,
                    code = code,
                    displayName = displayName,
                    message = $"Supplier '{created.Name}' [{code}] created successfully and linked to Chart of Accounts."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error quick-creating supplier");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Route("QuickCreateCustomer")]
        public async Task<IActionResult> QuickCreateCustomer([FromBody] QuickCustomerRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Name))
            {
                return Json(new { success = false, message = "Customer / Buyer Name is required." });
            }

            try
            {
                int companyId = GetCurrentCompanyId();
                var vendorService = HttpContext.RequestServices.GetRequiredService<IVendorService>();

                var vendor = new Vendor
                {
                    VendorType = VendorType.Customer,
                    Name = request.Name.Trim(),
                    Phone = request.Phone?.Trim(),
                    City = request.City?.Trim(),
                    Cnic = request.Cnic?.Trim(),
                    OpeningBalance = 0,
                    Status = "Active",
                    CompanyId = companyId,
                    Active = true
                };

                var created = await vendorService.CreateVendorAsync(vendor, User.Identity?.Name ?? "System");
                // Use UpdateVendorAsync to generate code and synchronize vendor COA account
                created = await vendorService.UpdateVendorAsync(created, User.Identity?.Name ?? "System");

                string code = created.ChartOfAccount?.AccountCode ?? created.AccountNo ?? "";
                string displayName = !string.IsNullOrWhiteSpace(code)
                    ? (!string.IsNullOrWhiteSpace(created.City) ? $"[{code}] {created.Name} ({created.City})" : $"[{code}] {created.Name}")
                    : (!string.IsNullOrWhiteSpace(created.City) ? $"{created.Name} ({created.City})" : created.Name);

                return Json(new
                {
                    success = true,
                    id = created.Id,
                    name = created.Name,
                    code = code,
                    displayName = displayName,
                    message = $"Customer '{created.Name}' [{code}] created successfully and linked to Chart of Accounts."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error quick-creating customer");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        [Route("GetNextFishCode")]
        public async Task<IActionResult> GetNextFishCode()
        {
            try
            {
                var codes = await _context.Fishes
                    .Where(f => f.Code != null && f.Code.StartsWith("PRO-"))
                    .Select(f => f.Code!)
                    .ToListAsync();

                int maxNum = 0;
                foreach (var c in codes)
                {
                    if (!string.IsNullOrEmpty(c))
                    {
                        var numStr = c.Replace("PRO-", "").Trim();
                        if (int.TryParse(numStr, out int parsed))
                        {
                            if (parsed > maxNum) maxNum = parsed;
                        }
                    }
                }
                int nextNum = maxNum + 1;
                string nextCode;
                do
                {
                    nextCode = $"PRO-{nextNum:D4}";
                    nextNum++;
                } while (codes.Contains(nextCode));

                return Json(new { success = true, nextCode });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Route("QuickCreateFish")]
        public async Task<IActionResult> QuickCreateFish([FromBody] QuickFishRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Name))
            {
                return Json(new { success = false, message = "Fish Variety Name is required." });
            }

            try
            {
                int companyId = GetCurrentCompanyId();
                string code = request.Code?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(code))
                {
                    var codes = await _context.Fishes
                        .Where(f => f.Code != null && f.Code.StartsWith("PRO-"))
                        .Select(f => f.Code!)
                        .ToListAsync();

                    int maxNum = 0;
                    foreach (var c in codes)
                    {
                        if (!string.IsNullOrEmpty(c))
                        {
                            var numStr = c.Replace("PRO-", "").Trim();
                            if (int.TryParse(numStr, out int parsed))
                            {
                                if (parsed > maxNum) maxNum = parsed;
                            }
                        }
                    }
                    int nextNum = maxNum + 1;
                    do
                    {
                        code = $"PRO-{nextNum:D4}";
                        nextNum++;
                    } while (codes.Contains(code));
                }

                var fish = new Fish
                {
                    Name = request.Name.Trim(),
                    Code = code,
                    Type = string.IsNullOrWhiteSpace(request.Type) ? "Sea Fish" : request.Type.Trim(),
                    Unit = string.IsNullOrWhiteSpace(request.Unit) ? "Kg" : request.Unit.Trim(),
                    CompanyId = companyId,
                    Active = true
                };

                _context.Fishes.Add(fish);
                await _context.SaveChangesAsync();

                string displayName = !string.IsNullOrEmpty(fish.Code) ? $"[{fish.Code}] {fish.Name}" : fish.Name;

                return Json(new
                {
                    success = true,
                    id = fish.Id,
                    name = fish.Name,
                    code = fish.Code,
                    displayName = displayName,
                    message = $"Fish variety '{fish.Name}' created successfully."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error quick-creating fish");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================================================================
        // 9. ACCOUNTING VOUCHER POSTING ENGINE (DOUBLE-ENTRY POSTING)
        // Brokerage Bill Voucher:
        // - Debit: Customer Receivable(s) for Gross Catch Amount
        // - Credit: Commission Income (4001) for Commission Amount
        // - Credit: Supplier Payable (2001) for Gross - Commission
        // (Expenses are paid by suppliers directly, so no expense voucher entries are created)
        // =========================================================================
        private async Task<Voucher?> PostBillAccountingVoucherAsync(
            BillMaster bill, 
            List<BillLotItemDto> validLots, 
            int companyId, 
            int fyId, 
            int? userId, 
            string currentUser)
        {
            try
            {
                // 1. Ensure Top-Level Chart of Accounts Hierarchy Exists
                //await EnsureCoreChartOfAccountsAsync(companyId);

                // 2. Fetch Nature of Accounts for mapping
                var debtorNature = await _context.NatureofAccounts.FirstOrDefaultAsync(n => n.DebtorOrCreditor == "D");
                var creditorNature = await _context.NatureofAccounts.FirstOrDefaultAsync(n => n.DebtorOrCreditor == "C");

                // Load all active accounts for this company into memory
                var allAccounts = await _context.ChartOfAccounts
                    .Where(a => a.CompanyId == null || a.CompanyId == companyId)
                    .ToListAsync();

                // 3. Resolve / Ensure Parent Heads in memory
                var assetsParent = allAccounts.FirstOrDefault(a => a.AccountCode == "1000")
                    ?? allAccounts.FirstOrDefault(a => a.AccountName != null && a.AccountName.IndexOf("Assets", StringComparison.OrdinalIgnoreCase) >= 0)
                    ?? allAccounts.FirstOrDefault(a => a.ParentAccountId == null && debtorNature != null && a.NatureOfAccountId == debtorNature.Id);

                var setup = await _context.AccountSetups.AsNoTracking().FirstOrDefaultAsync(s => s.CompanyId == companyId)
                    ?? await _context.AccountSetups.AsNoTracking().FirstOrDefaultAsync();

                var custReceivableParent = (setup?.CustomerAccID.HasValue == true && setup.CustomerAccID.Value > 0)
                    ? allAccounts.FirstOrDefault(a => a.Id == setup.CustomerAccID.Value)
                    : (allAccounts.FirstOrDefault(a => a.AccountCode == "0101003" || a.AccountCode == "1003")
                       ?? allAccounts.FirstOrDefault(a => a.AccountName != null && a.AccountName.IndexOf("Customer Receivable", StringComparison.OrdinalIgnoreCase) >= 0)
                       ?? allAccounts.FirstOrDefault(a => a.AccountCode == "1000"));

                var supPayableParent = (setup?.SupplierAccID.HasValue == true && setup.SupplierAccID.Value > 0)
                    ? allAccounts.FirstOrDefault(a => a.Id == setup.SupplierAccID.Value)
                    : (allAccounts.FirstOrDefault(a => a.AccountCode == "0201001" || a.AccountCode == "2001")
                       ?? allAccounts.FirstOrDefault(a => a.AccountName != null && a.AccountName.IndexOf("Supplier Payable", StringComparison.OrdinalIgnoreCase) >= 0)
                       ?? allAccounts.FirstOrDefault(a => a.AccountCode == "2000"));

                var revenueParent = allAccounts.FirstOrDefault(a => a.AccountCode == "4000")
                    ?? allAccounts.FirstOrDefault(a => a.AccountName != null && a.AccountName.IndexOf("Revenue", StringComparison.OrdinalIgnoreCase) >= 0)
                    ?? allAccounts.FirstOrDefault(a => a.ParentAccountId == null && creditorNature != null && a.NatureOfAccountId == creditorNature.Id);

                // 4. Ensure Commission Income Account exists (4001)
                var commissionCoa = allAccounts.FirstOrDefault(a => a.AccountCode == "0401001")
                    ?? allAccounts.FirstOrDefault(a => a.AccountName != null && a.AccountName.IndexOf("Commission", StringComparison.OrdinalIgnoreCase) >= 0);


                // 5. Ensure Supplier Account in Chart of Accounts
                ChartOfAccount? supplierCoa = null;
                if (bill.SupplierId.HasValue && bill.SupplierId.Value > 0)
                {
                    var supplier = await _context.Vendors
                        .Include(v => v.ChartOfAccount)
                        .FirstOrDefaultAsync(v => v.Id == bill.SupplierId.Value);

                    if (supplier != null)
                    {
                        if (supplier.ChartOfAccountId.HasValue && supplier.ChartOfAccountId.Value > 0)
                        {
                            supplierCoa = supplier.ChartOfAccount ?? allAccounts.FirstOrDefault(a => a.Id == supplier.ChartOfAccountId.Value);
                        }

                        if (supplierCoa == null && supPayableParent != null)
                        {
                            string nextSupCode = await GenerateNextAccountCodeAsync(supPayableParent.Id);
                            supplierCoa = new ChartOfAccount
                            {
                                AccountCode = nextSupCode,
                                AccountName = supplier.Name.Trim(),
                                ParentAccountId = supPayableParent.Id,
                                NatureOfAccountId = creditorNature?.Id ?? supPayableParent.NatureOfAccountId,
                                Transable = true,
                                Active = true,
                                Currency = "PKR",
                                CompanyId = companyId,
                                CreatedOn = DateTime.Now,
                                CreatedBy = currentUser
                            };
                            await _context.ChartOfAccounts.AddAsync(supplierCoa);
                            await _context.SaveChangesAsync();
                            allAccounts.Add(supplierCoa);

                            supplier.ChartOfAccountId = supplierCoa.Id;
                            _context.Vendors.Update(supplier);
                            await _context.SaveChangesAsync();
                        }
                    }
                }

                if (supplierCoa == null)
                {
                    supplierCoa = supPayableParent ?? allAccounts.FirstOrDefault(a => a.AccountCode == "2000");
                }

                // 6. Find or Create Main Voucher (JV)
                var voucher = await _context.Vouchers
                    .Include(v => v.AccountTransactions)
                    .FirstOrDefaultAsync(v => (v.InvoiceId == bill.Id || v.InvoiceNo == bill.BillNo) && (v.CompanyId == null || v.CompanyId == companyId));

                string supplierName = bill.Supplier?.Name ?? supplierCoa?.AccountName ?? "Boat / Supplier";
                decimal supplierCatchPayable = Math.Max(0, bill.TotalGrossAmount - bill.CommissionAmount - bill.TotalExpense);
                decimal depositAmt = Math.Max(0, bill.Deposit);
                decimal IceExpense = Math.Max(0, bill.IceExpense);
                decimal FarmerIceExpense = Math.Max(0, bill.FarmerIceExpense);
                decimal LabourExpense = Math.Max(0, bill.LabourExpense);
                decimal FreightExpense = Math.Max(0, bill.FreightExpense);
                decimal OtherExpense = Math.Max(0, bill.OtherExpense);
                decimal netSupplierCredit = Math.Max(0, supplierCatchPayable - depositAmt);

                if (voucher == null)
                {
                    string voucherNo = await _unitOfWork.GenerateVoucherNoAsync("JV", fyId, companyId);
                    voucher = new Voucher
                    {
                        VoucherNo = voucherNo,
                        VoucherDate = bill.BillDate,
                        VoucherType = "JV",
                        Amount = bill.TotalGrossAmount,
                        VendorId = bill.SupplierId,
                        InvoiceId = bill.Id,
                        InvoiceNo = bill.BillNo,
                        Narration = $"Brokerage Bill #{bill.BillNo} - {supplierName} | Gross: Rs {bill.TotalGrossAmount:N0}, Comm ({bill.CommissionPercent}%): Rs {bill.CommissionAmount:N0}, Martey: Rs {depositAmt:N0}, Supplier Catch: Rs {netSupplierCredit:N0}",
                        CompanyId = companyId,
                        FinancialYearID = fyId,
                        UserId = userId,
                        Username = currentUser,
                        ApprovalStatus = "Approved",
                        DeleteStatus = 0,
                        CreatedBy = currentUser,
                        CreatedOn = DateTime.Now
                    };

                    await _context.Vouchers.AddAsync(voucher);
                    await _context.SaveChangesAsync(); // generates VoucherId
                }
                else
                {
                    voucher.VoucherDate = bill.BillDate;
                    voucher.Amount = bill.TotalGrossAmount;
                    voucher.VendorId = bill.SupplierId;
                    voucher.InvoiceNo = bill.BillNo;
                    voucher.Narration = $"Brokerage Bill #{bill.BillNo} - {supplierName} | Gross: Rs {bill.TotalGrossAmount:N0}, Comm ({bill.CommissionPercent}%): Rs {bill.CommissionAmount:N0}, Martey: Rs {depositAmt:N0}, Supplier Catch: Rs {netSupplierCredit:N0}";
                    voucher.ModifiedBy = currentUser;
                    voucher.ModifiedOn = DateTime.Now;

                    // Remove existing transactions to re-post fresh balanced ones
                    if (voucher.AccountTransactions != null && voucher.AccountTransactions.Any())
                    {
                        _context.AccountTransactions.RemoveRange(voucher.AccountTransactions);
                    }
                    _context.Vouchers.Update(voucher);
                    await _context.SaveChangesAsync();
                }

                var transactions = new List<AccountTransaction>();

                // 7. DEBIT ENTRIES: Customer Receivables (Grouped by Customer)
                var customerGroups = validLots
                    .GroupBy(l => l.CustomerId)
                    .ToList();

                foreach (var group in customerGroups)
                {
                    decimal custGross = Math.Round(group.Sum(l => l.Amount), 0);
                    if (custGross <= 0) continue;

                    ChartOfAccount? custCoa = null;
                    if (group.Key.HasValue && group.Key.Value > 0)
                    {
                        var custVendor = await _context.Vendors
                            .Include(v => v.ChartOfAccount)
                            .FirstOrDefaultAsync(v => v.Id == group.Key.Value);

                        if (custVendor != null)
                        {
                            if (custVendor.ChartOfAccountId.HasValue && custVendor.ChartOfAccountId.Value > 0)
                            {
                                custCoa = custVendor.ChartOfAccount ?? allAccounts.FirstOrDefault(a => a.Id == custVendor.ChartOfAccountId.Value);
                            }

                            if (custCoa == null && custReceivableParent != null)
                            {
                                string nextCustCode = await GenerateNextAccountCodeAsync(custReceivableParent.Id);
                                custCoa = new ChartOfAccount
                                {
                                    AccountCode = nextCustCode,
                                    AccountName = custVendor.Name.Trim(),
                                    ParentAccountId = custReceivableParent.Id,
                                    NatureOfAccountId = debtorNature?.Id ?? custReceivableParent.NatureOfAccountId,
                                    Transable = true,
                                    Active = true,
                                    Currency = "PKR",
                                    CompanyId = companyId,
                                    CreatedOn = DateTime.Now,
                                    CreatedBy = currentUser
                                };
                                await _context.ChartOfAccounts.AddAsync(custCoa);
                                await _context.SaveChangesAsync();
                                allAccounts.Add(custCoa);

                                custVendor.ChartOfAccountId = custCoa.Id;
                                _context.Vendors.Update(custVendor);
                                await _context.SaveChangesAsync();
                            }
                        }
                    }

                    if (custCoa == null)
                    {
                        custCoa = custReceivableParent ?? allAccounts.FirstOrDefault(a => a.AccountCode == "1003");
                    }

                    string custName = custCoa?.AccountName ?? "General Buyer";

                    transactions.Add(new AccountTransaction
                    {
                        VoucherId = voucher.VoucherId,
                        VoucherNo = voucher.VoucherNo,
                        ChartofAccountId = custCoa?.Id,
                        ChartofAccountName = custName,
                        Code = custCoa?.AccountCode ?? "",
                        AccountName = custName,
                        Debit = custGross,
                        Credit = 0,
                        Amount = custGross,
                        InvoiceNo = bill.BillNo,
                        Narration = $"Brokerage Bill #{bill.BillNo} - Catch Purchase (Receivable from {custName})",
                        EntryTypeName = "Journal Voucher",
                        Type = "Customer",
                        ExtraDate = bill.BillDate,
                        CompanyId = companyId,
                        UserId = userId
                    });
                }

                if (LabourExpense > 0 && supplierCoa != null)
                {
                    transactions.Add(new AccountTransaction
                    {
                        VoucherId = voucher.VoucherId,
                        VoucherNo = voucher.VoucherNo,
                        ChartofAccountId = supplierCoa.Id,
                        ChartofAccountName = supplierCoa.AccountName,
                        Code = supplierCoa.AccountCode ?? "",
                        AccountName = supplierCoa.AccountName,
                        Debit = 0,
                        Credit = LabourExpense,
                        Amount = LabourExpense,
                        InvoiceNo = bill.BillNo,
                        Narration = $"Labour Expense for #{bill.BillNo} - Credited to {supplierCoa.AccountName}",
                        EntryTypeName = "Journal Voucher",
                        Type = "Labour Expense",
                        ExtraDate = bill.BillDate,
                        CompanyId = companyId,
                        UserId = userId
                    });
                }


                if (IceExpense > 0 && supplierCoa != null)
                {
                    transactions.Add(new AccountTransaction
                    {
                        VoucherId = voucher.VoucherId,
                        VoucherNo = voucher.VoucherNo,
                        ChartofAccountId = supplierCoa.Id,
                        ChartofAccountName = supplierCoa.AccountName,
                        Code = supplierCoa.AccountCode ?? "",
                        AccountName = supplierCoa.AccountName,
                        Debit = 0,
                        Credit = IceExpense,
                        Amount = IceExpense,
                        InvoiceNo = bill.BillNo,
                        Narration = $"Ice Expense for #{bill.BillNo} - Credited to {supplierCoa.AccountName}",
                        EntryTypeName = "Journal Voucher",
                        Type = "Ice Expense",
                        ExtraDate = bill.BillDate,
                        CompanyId = companyId,
                        UserId = userId
                    });
                }

                if (FarmerIceExpense > 0 && supplierCoa != null)
                {
                    transactions.Add(new AccountTransaction
                    {
                        VoucherId = voucher.VoucherId,
                        VoucherNo = voucher.VoucherNo,
                        ChartofAccountId = supplierCoa.Id,
                        ChartofAccountName = supplierCoa.AccountName,
                        Code = supplierCoa.AccountCode ?? "",
                        AccountName = supplierCoa.AccountName,
                        Debit = 0,
                        Credit = FarmerIceExpense,
                        Amount = FarmerIceExpense,
                        InvoiceNo = bill.BillNo,
                        Narration = $"Farmer Ice Expense for #{bill.BillNo} - Credited to {supplierCoa.AccountName}",
                        EntryTypeName = "Journal Voucher",
                        Type = "Farmer Ice Expense",
                        ExtraDate = bill.BillDate,
                        CompanyId = companyId,
                        UserId = userId
                    });
                }

                if (FreightExpense > 0 && supplierCoa != null)
                {
                    transactions.Add(new AccountTransaction
                    {
                        VoucherId = voucher.VoucherId,
                        VoucherNo = voucher.VoucherNo,
                        ChartofAccountId = supplierCoa.Id,
                        ChartofAccountName = supplierCoa.AccountName,
                        Code = supplierCoa.AccountCode ?? "",
                        AccountName = supplierCoa.AccountName,
                        Debit = 0,
                        Credit = FreightExpense,
                        Amount = FreightExpense,
                        InvoiceNo = bill.BillNo,
                        Narration = $"Freight Expense for #{bill.BillNo} - Credited to {supplierCoa.AccountName}",
                        EntryTypeName = "Journal Voucher",
                        Type = "Freight Expense",
                        ExtraDate = bill.BillDate,
                        CompanyId = companyId,
                        UserId = userId
                    });
                }

                if (OtherExpense > 0 && supplierCoa != null)
                {
                    transactions.Add(new AccountTransaction
                    {
                        VoucherId = voucher.VoucherId,
                        VoucherNo = voucher.VoucherNo,
                        ChartofAccountId = supplierCoa.Id,
                        ChartofAccountName = supplierCoa.AccountName,
                        Code = supplierCoa.AccountCode ?? "",
                        AccountName = supplierCoa.AccountName,
                        Debit = 0,
                        Credit = OtherExpense,
                        Amount = OtherExpense,
                        InvoiceNo = bill.BillNo,
                        Narration = $"Other Expense for #{bill.BillNo} - Credited to {supplierCoa.AccountName}",
                        EntryTypeName = "Journal Voucher",
                        Type = "Other Expense",
                        ExtraDate = bill.BillDate,
                        CompanyId = companyId,
                        UserId = userId
                    });
                }

                // 8. CREDIT ENTRY 1: Supplier Payable (Gross Catch minus Commission minus Deposit)
                if (netSupplierCredit > 0 && supplierCoa != null)
                {
                    transactions.Add(new AccountTransaction
                    {
                        VoucherId = voucher.VoucherId,
                        VoucherNo = voucher.VoucherNo,
                        ChartofAccountId = supplierCoa.Id,
                        ChartofAccountName = supplierCoa.AccountName,
                        Code = supplierCoa.AccountCode ?? "",
                        AccountName = supplierCoa.AccountName,
                        Debit = 0,
                        Credit = netSupplierCredit,
                        Amount = netSupplierCredit,
                        InvoiceNo = bill.BillNo,
                        Narration = $"Brokerage Bill #{bill.BillNo} - Catch Settlement (Payable to {supplierCoa.AccountName})",
                        EntryTypeName = "Journal Voucher",
                        Type = "Supplier",
                        ExtraDate = bill.BillDate,
                        CompanyId = companyId,
                        UserId = userId
                    });
                }




                // 8b. CREDIT ENTRY 2: Supplier Martey / Deposit Entry
                if (depositAmt > 0 && supplierCoa != null)
                {
                    transactions.Add(new AccountTransaction
                    {
                        VoucherId = voucher.VoucherId,
                        VoucherNo = voucher.VoucherNo,
                        ChartofAccountId = supplierCoa.Id,
                        ChartofAccountName = supplierCoa.AccountName,
                        Code = supplierCoa.AccountCode ?? "",
                        AccountName = supplierCoa.AccountName,
                        Debit = 0,
                        Credit = depositAmt,
                        Amount = depositAmt,
                        InvoiceNo = bill.BillNo,
                        Narration = $"Brokerage Bill #{bill.BillNo} - Martey / Deposit for {supplierCoa.AccountName}",
                        EntryTypeName = "Journal Voucher",
                        Type = "Martey",
                        ExtraDate = bill.BillDate,
                        CompanyId = companyId,
                        UserId = userId
                    });
                }

                // 9. CREDIT ENTRY 3: Commission Income (4001)
                if (bill.CommissionAmount > 0 && commissionCoa != null)
                {
                    transactions.Add(new AccountTransaction
                    {
                        VoucherId = voucher.VoucherId,
                        VoucherNo = voucher.VoucherNo,
                        ChartofAccountId = commissionCoa.Id,
                        ChartofAccountName = commissionCoa.AccountName,
                        Code = commissionCoa.AccountCode ?? "",
                        AccountName = commissionCoa.AccountName,
                        Debit = 0,
                        Credit = bill.CommissionAmount,
                        Amount = bill.CommissionAmount,
                        InvoiceNo = bill.BillNo,
                        Narration = $"Brokerage Bill #{bill.BillNo} - Aarhat Agency Commission ({bill.CommissionPercent}%)",
                        EntryTypeName = "Journal Voucher",
                        Type = "Commission",
                        ExtraDate = bill.BillDate,
                        CompanyId = companyId,
                        UserId = userId
                    });
                }

                // (Note: Labour and Ice expenses are paid directly by suppliers, so no expense credit entries are posted in the Aarhat Brokerage JV)

                await _context.AccountTransactions.AddRangeAsync(transactions);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Main Accounting Voucher '{VoucherNo}' (ID: {VoucherId}) posted successfully for Bill '{BillNo}' with {TxCount} transactions (Total: {TotalGross}).",
                    voucher.VoucherNo, voucher.VoucherId, bill.BillNo, transactions.Count, bill.TotalGrossAmount);

                // =========================================================================
                // 10. VOUCHER #2: FARMER / SUPPLIER EXPENSES VOUCHER (CPV)
                // If the Aarhat Mandi pays Labour + Ice on behalf of the farmer/supplier:
                // - DEBIT: Supplier Payable (Reduces liability to supplier)
                // - CREDIT: Cash in Hand (1001) (Cash paid out by Mandi for Labour & Ice)
                // Total Debit = Total Credit = TotalExpense (Labour + Ice)
                // =========================================================================
                if (bill.TotalExpense > 0 && supplierCoa != null)
                {
                    var cashCoa = allAccounts.FirstOrDefault(a => a.AccountCode == "1001")
                        ?? allAccounts.FirstOrDefault(a => a.AccountName.Contains("Cash", StringComparison.OrdinalIgnoreCase));

                    if (cashCoa == null && assetsParent != null)
                    {
                        cashCoa = new ChartOfAccount
                        {
                            AccountCode = "1001",
                            AccountName = "Cash in Hand",
                            ParentAccountId = assetsParent.Id,
                            NatureOfAccountId = debtorNature?.Id ?? assetsParent.NatureOfAccountId,
                            Transable = true,
                            Active = true,
                            Currency = "PKR",
                            CompanyId = companyId,
                            CreatedOn = DateTime.Now,
                            CreatedBy = "System"
                        };
                        await _context.ChartOfAccounts.AddAsync(cashCoa);
                        await _context.SaveChangesAsync();
                        allAccounts.Add(cashCoa);
                    }

                    var expVoucher = await _context.Vouchers
                        .Include(v => v.AccountTransactions)
                        .FirstOrDefaultAsync(v => (v.InvoiceId == bill.Id || v.InvoiceNo == bill.BillNo) &&
                                                  (v.VoucherType == "CP" || v.VoucherType == "CPV" || (v.VoucherNo != null && v.VoucherNo.StartsWith("CPV-"))) &&
                                                  (v.InvoiceRefNo == "EXPENSE" || (v.Narration != null && (v.Narration.Contains("Farmer Expenses") || v.Narration.Contains("Expenses Voucher")))) &&
                                                  (v.CompanyId == null || v.CompanyId == companyId));

                    if (expVoucher == null)
                    {
                        string expVoucherNo = await _unitOfWork.GenerateVoucherNoAsync("CP", fyId, companyId);
                        expVoucher = new Voucher
                        {
                            VoucherNo = expVoucherNo,
                            VoucherDate = bill.BillDate,
                            VoucherType = "CP",
                            Amount = bill.TotalExpense,
                            VendorId = bill.SupplierId,
                            InvoiceId = bill.Id,
                            InvoiceNo = bill.BillNo,
                            InvoiceRefNo = "EXPENSE",
                            Narration = $"Farmer Expenses Voucher (Labour: Rs {bill.LabourExpense:N0}, Ice: Rs {bill.IceExpense:N0}, Farmer Ice: Rs {bill.FarmerIceExpense:N0}, Freight: Rs {bill.FreightExpense:N0}, Other: Rs {bill.OtherExpense:N0}) for Bill #{bill.BillNo} - {supplierName}",
                            CompanyId = companyId,
                            FinancialYearID = fyId,
                            UserId = userId,
                            Username = currentUser,
                            ApprovalStatus = "Approved",
                            DeleteStatus = 0,
                            CreatedBy = currentUser,
                            CreatedOn = DateTime.Now
                        };

                        await _context.Vouchers.AddAsync(expVoucher);
                        await _context.SaveChangesAsync(); // generates VoucherId
                    }
                    else
                    {
                        expVoucher.VoucherDate = bill.BillDate;
                        expVoucher.Amount = bill.TotalExpense;
                        expVoucher.VendorId = bill.SupplierId;
                        expVoucher.InvoiceNo = bill.BillNo;
                        expVoucher.InvoiceRefNo = "EXPENSE";
                        expVoucher.Narration = $"Farmer Expenses Voucher (Labour: Rs {bill.LabourExpense:N0}, Ice: Rs {bill.IceExpense:N0}, Farmer Ice: Rs {bill.FarmerIceExpense:N0}, Freight: Rs {bill.FreightExpense:N0}, Other: Rs {bill.OtherExpense:N0}) for Bill #{bill.BillNo} - {supplierName}";
                        expVoucher.ModifiedBy = currentUser;
                        expVoucher.ModifiedOn = DateTime.Now;

                        if (expVoucher.AccountTransactions != null && expVoucher.AccountTransactions.Any())
                        {
                            _context.AccountTransactions.RemoveRange(expVoucher.AccountTransactions);
                        }
                        _context.Vouchers.Update(expVoucher);
                        await _context.SaveChangesAsync();
                    }

                    var expTransactions = new List<AccountTransaction>
                    {
                        // DEBIT: Supplier Payable
                        new AccountTransaction
                        {
                            VoucherId = expVoucher.VoucherId,
                            VoucherNo = expVoucher.VoucherNo,
                            ChartofAccountId = supplierCoa.Id,
                            ChartofAccountName = supplierCoa.AccountName,
                            Code = supplierCoa.AccountCode ?? "",
                            AccountName = supplierCoa.AccountName,
                            Debit = bill.TotalExpense,
                            Credit = 0,
                            Amount = bill.TotalExpense,
                            InvoiceNo = bill.BillNo,
                            Narration = $"Farmer Expenses paid on behalf of {supplierCoa.AccountName} (Labour: Rs {bill.LabourExpense:N0}, Ice: Rs {bill.IceExpense:N0}, Farmer Ice: Rs {bill.FarmerIceExpense:N0}, Freight: Rs {bill.FreightExpense:N0}, Other: Rs {bill.OtherExpense:N0}) for Bill #{bill.BillNo}",
                            EntryTypeName = "Cash Payment",
                            Type = "Expense",
                            ExtraDate = bill.BillDate,
                            CompanyId = companyId,
                            UserId = userId
                        },

                        // CREDIT: Cash in Hand
                        new AccountTransaction
                        {
                            VoucherId = expVoucher.VoucherId,
                            VoucherNo = expVoucher.VoucherNo,
                            ChartofAccountId = cashCoa?.Id,
                            ChartofAccountName = cashCoa?.AccountName ?? "Cash in Hand",
                            Code = cashCoa?.AccountCode ?? "1001",
                            AccountName = cashCoa?.AccountName ?? "Cash in Hand",
                            Debit = 0,
                            Credit = bill.TotalExpense,
                            Amount = bill.TotalExpense,
                            InvoiceNo = bill.BillNo,
                            Narration = $"Cash paid for Expenses for Bill #{bill.BillNo} ({supplierName})",
                            EntryTypeName = "Cash Payment",
                            Type = "Cash",
                            ExtraDate = bill.BillDate,
                            CompanyId = companyId,
                            UserId = userId
                        }
                    };

                    await _context.AccountTransactions.AddRangeAsync(expTransactions);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Farmer Expenses Voucher '{ExpVoucherNo}' posted successfully for Bill '{BillNo}' (Debit Supplier: Rs {ExpAmt:N0}, Credit Cash: Rs {CashAmt:N0}).",
                        expVoucher.VoucherNo, bill.BillNo, bill.TotalExpense, bill.TotalExpense);
                }
                else
                {
                    // If expenses are 0, clean up any previously created expense voucher
                    var staleExpVoucher = await _context.Vouchers
                        .Include(v => v.AccountTransactions)
                        .FirstOrDefaultAsync(v => (v.InvoiceId == bill.Id || v.InvoiceNo == bill.BillNo) &&
                                                  (v.VoucherType == "CP" || v.VoucherType == "CPV" || (v.VoucherNo != null && v.VoucherNo.StartsWith("CPV-"))) &&
                                                  (v.InvoiceRefNo == "EXPENSE" || (v.Narration != null && (v.Narration.Contains("Farmer Expenses") || v.Narration.Contains("Expenses Voucher")))) &&
                                                  (v.CompanyId == null || v.CompanyId == companyId));

                    if (staleExpVoucher != null)
                    {
                        if (staleExpVoucher.AccountTransactions != null && staleExpVoucher.AccountTransactions.Any())
                        {
                            _context.AccountTransactions.RemoveRange(staleExpVoucher.AccountTransactions);
                        }
                        _context.Vouchers.Remove(staleExpVoucher);
                        await _context.SaveChangesAsync();
                    }
                }

                return voucher;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to post accounting voucher for Bill {BillNo}: {Message}", bill.BillNo, ex.Message);
                return null;
            }
        }

        // =========================================================================
        // PRIVATE HELPERS
        // =========================================================================
        //private async Task EnsureCoreChartOfAccountsAsync(int companyId)
        //{
        //    if (!await _context.ChartOfAccounts.AnyAsync())
        //    {
        //        var debtorNature = await _context.NatureofAccounts.FirstOrDefaultAsync(n => n.DebtorOrCreditor == "D");
        //        var creditorNature = await _context.NatureofAccounts.FirstOrDefaultAsync(n => n.DebtorOrCreditor == "C");

        //        var defaultAccounts = new List<ChartOfAccount>
        //        {
        //            new ChartOfAccount { AccountCode = "1000", AccountName = "Assets", Transable = false, Active = true, NatureOfAccountId = debtorNature?.Id, Currency = "PKR", CompanyId = companyId, CreatedBy = "System", CreatedOn = DateTime.Now },
        //            new ChartOfAccount { AccountCode = "2000", AccountName = "Liabilities", Transable = false, Active = true, NatureOfAccountId = creditorNature?.Id, Currency = "PKR", CompanyId = companyId, CreatedBy = "System", CreatedOn = DateTime.Now },
        //            new ChartOfAccount { AccountCode = "3000", AccountName = "Equity & Capital", Transable = false, Active = true, NatureOfAccountId = creditorNature?.Id, Currency = "PKR", CompanyId = companyId, CreatedBy = "System", CreatedOn = DateTime.Now },
        //            new ChartOfAccount { AccountCode = "4000", AccountName = "Revenue & Sales", Transable = false, Active = true, NatureOfAccountId = creditorNature?.Id, Currency = "PKR", CompanyId = companyId, CreatedBy = "System", CreatedOn = DateTime.Now },
        //            new ChartOfAccount { AccountCode = "5000", AccountName = "Expenses", Transable = false, Active = true, NatureOfAccountId = debtorNature?.Id, Currency = "PKR", CompanyId = companyId, CreatedBy = "System", CreatedOn = DateTime.Now }
        //        };

        //        await _context.ChartOfAccounts.AddRangeAsync(defaultAccounts);
        //        await _context.SaveChangesAsync();

        //        var assets = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.AccountCode == "1000");
        //        var liab = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.AccountCode == "2000");
        //        var rev = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.AccountCode == "4000");
        //        var exp = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.AccountCode == "5000");

        //        if (assets != null)
        //        {
        //            var cash = new ChartOfAccount { AccountCode = "1001", AccountName = "Cash in Hand", ParentAccountId = assets.Id, NatureOfAccountId = debtorNature?.Id, Transable = true, Active = true, Currency = "PKR", CompanyId = companyId, CreatedBy = "System", CreatedOn = DateTime.Now };
        //            var bank = new ChartOfAccount { AccountCode = "1002", AccountName = "Bank Accounts", ParentAccountId = assets.Id, NatureOfAccountId = debtorNature?.Id, Transable = true, Active = true, Currency = "PKR", CompanyId = companyId, CreatedBy = "System", CreatedOn = DateTime.Now };
        //            var receivables = new ChartOfAccount { AccountCode = "1003", AccountName = "Customer Receivables", ParentAccountId = assets.Id, NatureOfAccountId = debtorNature?.Id, Transable = true, Active = true, Currency = "PKR", CompanyId = companyId, CreatedBy = "System", CreatedOn = DateTime.Now };
        //            await _context.ChartOfAccounts.AddRangeAsync(cash, bank, receivables);
        //        }

        //        if (liab != null)
        //        {
        //            var payables = new ChartOfAccount { AccountCode = "2001", AccountName = "Supplier Payables", ParentAccountId = liab.Id, NatureOfAccountId = creditorNature?.Id, Transable = true, Active = true, Currency = "PKR", CompanyId = companyId, CreatedBy = "System", CreatedOn = DateTime.Now };
        //            await _context.ChartOfAccounts.AddAsync(payables);
        //        }

        //        if (rev != null)
        //        {
        //            var comm = new ChartOfAccount { AccountCode = "4001", AccountName = "Commission Income", ParentAccountId = rev.Id, NatureOfAccountId = creditorNature?.Id, Transable = true, Active = true, Currency = "PKR", CompanyId = companyId, CreatedBy = "System", CreatedOn = DateTime.Now };
        //            await _context.ChartOfAccounts.AddAsync(comm);
        //        }

        //        if (exp != null)
        //        {
        //            var labour = new ChartOfAccount { AccountCode = "5001", AccountName = "Labour / Tulai Expense", ParentAccountId = exp.Id, NatureOfAccountId = debtorNature?.Id, Transable = true, Active = true, Currency = "PKR", CompanyId = companyId, CreatedBy = "System", CreatedOn = DateTime.Now };
        //            var loading = new ChartOfAccount { AccountCode = "5002", AccountName = "Loading & Transport Expense", ParentAccountId = exp.Id, NatureOfAccountId = debtorNature?.Id, Transable = true, Active = true, Currency = "PKR", CompanyId = companyId, CreatedBy = "System", CreatedOn = DateTime.Now };
        //            var other = new ChartOfAccount { AccountCode = "5003", AccountName = "Other Mandi Expenses", ParentAccountId = exp.Id, NatureOfAccountId = debtorNature?.Id, Transable = true, Active = true, Currency = "PKR", CompanyId = companyId, CreatedBy = "System", CreatedOn = DateTime.Now };
        //            await _context.ChartOfAccounts.AddRangeAsync(labour, loading, other);
        //        }

        //        await _context.SaveChangesAsync();
        //    }
        //}

        private async Task<string> GenerateNextAccountCodeAsync(int parentAccountId)
        {
            var vendorService = HttpContext.RequestServices.GetRequiredService<IVendorService>();
            return await vendorService.GenerateNextAccountCodeAsync(parentAccountId);
        }

        private async Task<string> GenerateNextBillNoAsync()
        {
            var bills = await _context.BillMasters
                .Where(b => b.BillNo != null)
                .Select(b => b.BillNo)
                .ToListAsync();

            int maxNum = 1000;
            foreach (var no in bills)
            {
                if (!string.IsNullOrEmpty(no) && no.StartsWith("BL-"))
                {
                    var numStr = no.Replace("BL-", "").Trim();
                    if (int.TryParse(numStr, out int parsed))
                    {
                        if (parsed > maxNum) maxNum = parsed;
                    }
                }
            }

            return $"BL-{(maxNum + 1):D4}";
        }

        //private async Task EnsureDefaultEntitiesAsync()
        //{
        //    try
        //    {
        //        await _context.Database.ExecuteSqlRawAsync(@"
        //            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BillMasters') AND name = 'Deposit')
        //            BEGIN
        //                ALTER TABLE [BillMasters] ADD [Deposit] decimal(18,2) NOT NULL DEFAULT 0;
        //            END
        //            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AccountTransaction') AND name = 'Type')
        //            BEGIN
        //                ALTER TABLE [AccountTransaction] ADD [Type] nvarchar(50) NULL;
        //            END
        //        ");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogWarning(ex, "Column auto-check skipped: {Message}", ex.Message);
        //    }

        //    // Seed sample vendors if none exist
        //    if (!await _context.Vendors.AnyAsync())
        //    {
        //        var starterSuppliers = new List<Vendor>
        //        {
        //            new Vendor { VendorType = VendorType.Supplier, Name = "Younus Boat Co.", Phone = "0300-1122334", City = "Gwadar / Karachi", Status = "Active", OpeningBalance = 44000, CompanyId = 1, CreatedOn = DateTime.Now, CreatedBy = "System" },
        //            new Vendor { VendorType = VendorType.Supplier, Name = "Rasool Bux Mallah", Phone = "0321-9988771", City = "Ibrahim Hyderi", Status = "Active", OpeningBalance = 71200, CompanyId = 1, CreatedOn = DateTime.Now, CreatedBy = "System" },
        //            new Vendor { VendorType = VendorType.Supplier, Name = "Haji Karim Baksh", Phone = "0345-5544332", City = "West Wharf", Status = "Active", OpeningBalance = 96400, CompanyId = 1, CreatedOn = DateTime.Now, CreatedBy = "System" },
        //            new Vendor { VendorType = VendorType.Supplier, Name = "Sikandar Fisheries", Phone = "0311-6677889", City = "Pasni", Status = "Active", OpeningBalance = 58900, CompanyId = 1, CreatedOn = DateTime.Now, CreatedBy = "System" }
        //        };

        //        var starterCustomers = new List<Vendor>
        //        {
        //            new Vendor { VendorType = VendorType.Customer, Name = "Al-Bahar Hotel", Phone = "0321-2345671", City = "Clifton, Karachi", Status = "Active", OpeningBalance = 284000, CompanyId = 1, CreatedOn = DateTime.Now, CreatedBy = "System" },
        //            new Vendor { VendorType = VendorType.Customer, Name = "Seafood Express (Pvt) Ltd", Phone = "0333-4567890", City = "SITE, Karachi", Status = "Active", OpeningBalance = 461500, CompanyId = 1, CreatedOn = DateTime.Now, CreatedBy = "System" },
        //            new Vendor { VendorType = VendorType.Customer, Name = "Do Darya Restaurant", Phone = "0300-9988776", City = "Do Darya, Karachi", Status = "Active", OpeningBalance = 96200, CompanyId = 1, CreatedOn = DateTime.Now, CreatedBy = "System" },
        //            new Vendor { VendorType = VendorType.Customer, Name = "Karachi Export Traders", Phone = "0345-1122334", City = "Fish Harbour, Karachi", Status = "Active", OpeningBalance = 377300, CompanyId = 1, CreatedOn = DateTime.Now, CreatedBy = "System" }
        //        };

        //        await _context.Vendors.AddRangeAsync(starterSuppliers);
        //        await _context.Vendors.AddRangeAsync(starterCustomers);
        //        await _context.SaveChangesAsync();
        //    }

        //    // Seed sample bills if none exist
        //    if (!await _context.BillMasters.AnyAsync())
        //    {
        //        var customer = await _context.Vendors.FirstOrDefaultAsync(v => v.VendorType == VendorType.Customer);
        //        var supplier = await _context.Vendors.FirstOrDefaultAsync(v => v.VendorType == VendorType.Supplier);
        //        var allFishes = await _context.Fishes.ToListAsync();
        //        var fish1 = allFishes.FirstOrDefault(f => f.Name != null && f.Name.IndexOf("Pomfret", StringComparison.OrdinalIgnoreCase) >= 0) ?? allFishes.FirstOrDefault();
        //        var fish2 = allFishes.FirstOrDefault(f => f.Name != null && f.Name.IndexOf("Rohu", StringComparison.OrdinalIgnoreCase) >= 0) ?? allFishes.Skip(1).FirstOrDefault();

        //        if (customer != null && supplier != null && fish1 != null)
        //        {
        //            var sampleBill = new BillMaster
        //            {
        //                BillNo = "BL-1001",
        //                BillDate = DateTime.Today.AddDays(-1),
        //                SupplierId = supplier.Id,
        //                CommissionPercent = 5.0m,
        //                CommissionAmount = 9650,
        //                LabourExpense = 800,
        //                LoadingExpense = 600,
        //                OtherExpense = 300,
        //                TotalExpense = 1700,
        //                TotalGrossAmount = 193000,
        //                NetPayable = 181650,
        //                Status = "Paid",
        //                Remarks = "First auction sale bill",
        //                CompanyId = 1,
        //                CreatedOn = DateTime.Now,
        //                CreatedBy = "System",
        //                Active = true
        //            };

        //            sampleBill.Details.Add(new BillDetail
        //            {
        //                LotNo = "LOT-1001",
        //                FishId = fish1.Id,
        //                SupplierId = supplier.Id,
        //                CustomerId = customer.Id,
        //                WeightKg = 120,
        //                RatePerKg = 980,
        //                Amount = 117600,
        //                CreatedOn = DateTime.Now,
        //                CreatedBy = "System",
        //                Active = true
        //            });

        //            if (fish2 != null)
        //            {
        //                sampleBill.Details.Add(new BillDetail
        //                {
        //                    LotNo = "LOT-1002",
        //                    FishId = fish2.Id,
        //                    SupplierId = supplier.Id,
        //                    CustomerId = customer.Id,
        //                    WeightKg = 180,
        //                    RatePerKg = 420,
        //                    Amount = 75600,
        //                    CreatedOn = DateTime.Now,
        //                    CreatedBy = "System",
        //                    Active = true
        //                });
        //            }

        //            await _context.BillMasters.AddAsync(sampleBill);
        //            await _context.SaveChangesAsync();

        //            // Automatically post accounting voucher for seed bill
        //            var lotsDto = sampleBill.Details.Select(d => new BillLotItemDto
        //            {
        //                Id = d.Id,
        //                LotNo = d.LotNo ?? "LOT-1001",
        //                CustomerId = d.CustomerId,
        //                FishId = d.FishId,
        //                WeightKg = d.WeightKg,
        //                RatePerKg = d.RatePerKg,
        //                Amount = d.Amount
        //            }).ToList();

        //            await PostBillAccountingVoucherAsync(sampleBill, lotsDto, 1, 1, 1, "System");
        //        }
        //    }
        //}

        private async Task<ChartOfAccount?> GetOrCreateVendorAccountAsync(
            Vendor vendor,
            bool isSupplier,
            ERP_Core.Models.NatureofAccount? debtorNature,
            ERP_Core.Models.NatureofAccount? creditorNature,
            ChartOfAccount? parentAcc,
            List<ChartOfAccount> allAccounts,
            int companyId,
            string currentUser)
        {
            ChartOfAccount? partyCoa = null;
            if (vendor.ChartOfAccountId.HasValue && vendor.ChartOfAccountId.Value > 0)
            {
                partyCoa = vendor.ChartOfAccount ?? allAccounts.FirstOrDefault(a => a.Id == vendor.ChartOfAccountId.Value);
            }

            if (partyCoa == null && parentAcc != null)
            {
                string nextCode = await GenerateNextAccountCodeAsync(parentAcc.Id);
                partyCoa = new ChartOfAccount
                {
                    AccountCode = nextCode,
                    AccountName = vendor.Name.Trim(),
                    ParentAccountId = parentAcc.Id,
                    NatureOfAccountId = isSupplier ? (creditorNature?.Id ?? parentAcc.NatureOfAccountId) : (debtorNature?.Id ?? parentAcc.NatureOfAccountId),
                    Transable = true,
                    Active = true,
                    Currency = "PKR",
                    CompanyId = companyId,
                    CreatedOn = DateTime.Now,
                    CreatedBy = currentUser
                };
                await _context.ChartOfAccounts.AddAsync(partyCoa);
                await _context.SaveChangesAsync();
                allAccounts.Add(partyCoa);

                vendor.ChartOfAccountId = partyCoa.Id;
                _context.Vendors.Update(vendor);
                await _context.SaveChangesAsync();
            }

            return partyCoa;
        }

        //private async Task EnsureDefaultBankMastersAsync(int companyId)
        //{
        //    try
        //    {
        //        bool anyBanks = await _context.BankMasters.AnyAsync(b => b.CompanyId == null || b.CompanyId == companyId);
        //        if (!anyBanks)
        //        {
        //            var cashCoa = await _context.ChartOfAccounts.FirstOrDefaultAsync(c => c.AccountCode == "1001" || c.AccountName.Contains("Cash"));
        //            var bankCoa = await _context.ChartOfAccounts.FirstOrDefaultAsync(c => c.AccountCode == "1002" || c.AccountName.Contains("Bank"));

        //            var defaultCash = new ERP_Core.Models.BankMaster
        //            {
        //                BankName = "Cash in Hand",
        //                AccountName = "Cash Ledger",
        //                IsCash = true,
        //                IsBank = false,
        //                ChartOfAccountId = cashCoa?.Id,
        //                CompanyId = companyId,
        //                Active = true,
        //                CreatedBy = "System",
        //                CreatedOn = DateTime.Now
        //            };

        //            var defaultBank = new ERP_Core.Models.BankMaster
        //            {
        //                BankName = "Habib Bank Limited (HBL)",
        //                AccountName = "Main Operating A/C",
        //                AccountNo = "PK36HABB00012345678901",
        //                IsCash = false,
        //                IsBank = true,
        //                ChartOfAccountId = bankCoa?.Id,
        //                CompanyId = companyId,
        //                Active = true,
        //                CreatedBy = "System",
        //                CreatedOn = DateTime.Now
        //            };

        //            await _context.BankMasters.AddRangeAsync(defaultCash, defaultBank);
        //            await _context.SaveChangesAsync();
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogWarning(ex, "Could not ensure default BankMasters: {Message}", ex.Message);
        //    }
        //}

        private async Task<List<BankMasterDropdownDto>> GetCashAndBankAccountsAsync(int companyId)
        {
            // Fetch active Bank Masters only (Cash in Hand & Bank Accounts)
            return await _context.BankMasters
                .AsNoTracking()
                .Include(b => b.ChartOfAccount)
                .Where(b => b.Active != false && (b.CompanyId == null || b.CompanyId == companyId))
                .OrderBy(b => b.IsCash ? 0 : 1)
                .ThenBy(b => b.BankName)
                .Select(b => new BankMasterDropdownDto
                {
                    Id = b.Id,
                    BankName = b.BankName,
                    AccountNo = b.AccountNo,
                    ChartOfAccountId = b.ChartOfAccountId,
                    IsCash = b.IsCash,
                    IsBank = b.IsBank
                })
                .ToListAsync();
        }

        private int GetCurrentCompanyId()
        {
            int? sessionCompany = HttpContext.Session.GetInt32("CompanyId");
            if (sessionCompany.HasValue && sessionCompany.Value > 0)
                return sessionCompany.Value;

            var claim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId");
            if (claim != null && int.TryParse(claim.Value, out int cid) && cid > 0)
                return cid;

            int dbCompany = _context.CCompanies.Select(c => c.CompanyId).FirstOrDefault();
            return dbCompany > 0 ? dbCompany : 1;
        }

        private int GetCurrentFinancialYearId()
        {
            int? sessionFy = HttpContext.Session.GetInt32("FinancialYearId");
            if (sessionFy.HasValue && sessionFy.Value > 0)
                return sessionFy.Value;

            var claim = User.Claims.FirstOrDefault(c => c.Type == "FinancialYearId");
            if (claim != null && int.TryParse(claim.Value, out int fyId) && fyId > 0)
                return fyId;

            int activeFy = _context.CFinancialYears.Where(f => f.Active != false).Select(f => f.Id).FirstOrDefault();
            return activeFy > 0 ? activeFy : 1;
        }

        private int? GetCurrentUserId()
        {
            var claim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int uid))
                return uid;
            return null;
        }

        /// <summary>
        /// Identifies whether a voucher is a Farmer Expenses voucher (Labour &amp; Ice)
        /// generated automatically during bill creation, rather than an actual cash/bank payment to the supplier.
        /// </summary>
        /// <summary>
        /// Accurately matches whether a Customer Receipt Voucher belongs to a specific Catch Lot item in a Bill.
        /// Prevents cross-contamination when multiple lots have the same buyer/customer (e.g. CASH SALE).
        /// </summary>
        private static bool IsLotReceiptVoucher(Voucher v, BillDetail lot, BillMaster bill)
        {
            if (v == null || lot == null || bill == null) return false;

            // 1. Must match the Bill
            bool billMatch = (v.InvoiceId == bill.Id || (!string.IsNullOrEmpty(bill.BillNo) && string.Equals(v.InvoiceNo, bill.BillNo, StringComparison.OrdinalIgnoreCase)));
            if (!billMatch) return false;

            string lotNo = lot.LotNo?.Trim() ?? string.Empty;

 // 2. Direct match via InvoiceRefNo (e.g. LOT-1002)
 if (!string.IsNullOrWhiteSpace(v.InvoiceRefNo) && !string.IsNullOrWhiteSpace(lotNo))
 {
 if (string.Equals(v.InvoiceRefNo.Trim(), lotNo, StringComparison.OrdinalIgnoreCase))
 return true;
 }

 // 3. Direct match via Narration (e.g. contains (LOT-1002) or LOT-1002)
 if (!string.IsNullOrWhiteSpace(v.Narration) && !string.IsNullOrWhiteSpace(lotNo))
 {
 if (v.Narration.IndexOf(lotNo, StringComparison.OrdinalIgnoreCase) >= 0)
 return true;
 }

 // 4. Fallback: If voucher has no lot reference in InvoiceRefNo or Narration,
 // match by CustomerId ONLY IF this customer has exactly 1 lot in this bill AND the voucher does not mention any other lot in this bill
 if (lot.CustomerId.HasValue && v.VendorId == lot.CustomerId.Value)
 {
 bool referencesOtherLot = bill.Details != null && bill.Details.Any(other =>
 other.Id != lot.Id &&
 !string.IsNullOrWhiteSpace(other.LotNo) &&
 ((!string.IsNullOrWhiteSpace(v.InvoiceRefNo) && string.Equals(v.InvoiceRefNo.Trim(), other.LotNo.Trim(), StringComparison.OrdinalIgnoreCase)) ||
 (!string.IsNullOrWhiteSpace(v.Narration) && v.Narration.IndexOf(other.LotNo.Trim(), StringComparison.OrdinalIgnoreCase) >= 0))
 );

 if (!referencesOtherLot)
 {
 var customerLots = bill.Details?.Where(x => x.CustomerId == lot.CustomerId.Value).ToList();
 if (customerLots != null && customerLots.Count == 1)
 {
 return true;
 }
 }
 }

 return false;
 }

        private static bool IsFarmerExpenseVoucher(Voucher v)
        {
            if (v.InvoiceRefNo == "EXPENSE" || v.InvoiceRefNo == "FarmerExpense")
                return true;

            if (!string.IsNullOrEmpty(v.Narration) &&
                (v.Narration.Contains("Farmer Expenses", StringComparison.OrdinalIgnoreCase) ||
                 v.Narration.Contains("Expenses Voucher", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }
    }
}
