using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ERP_Core.Attributes;
using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP_Core.Controllers.SalesReport
{
    [Authorize]
    [Route("SalesReport")]
    public class SalesReportController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<SalesReportController> _logger;
        private static bool _spDeploymentAttempted = false;

        public SalesReportController(ERPDbContext context, ILogger<SalesReportController> logger)
        {
            _context = context;
            _logger = logger;   
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(
            DateTime? fromDate, 
            DateTime? toDate, 
            string? partyKey,
            string? partyType, 
            int? supplierId, 
            int? customerId, 
            int? partyId)
        {
            ViewData["Title"] = "Sales Report";
            ViewData["SubTitle"] = "Supplier (Boat / Catch) aur Customer (Buyer / خریدار) wise bikri, commission revenue aur settlement report.";

            int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;

            var parsedFrom = fromDate ?? DateTime.Today;
            var parsedTo = toDate ?? DateTime.Today;

            var fromDateTime = parsedFrom.Date;
            var toDateTime = parsedTo.Date.AddDays(1).AddTicks(-1);

            // Determine active party selection from partyKey or individual parameters
            string activePartyType = "supplier";
            if (!string.IsNullOrWhiteSpace(partyKey))
            {
                if (partyKey.StartsWith("c_") || partyKey.StartsWith("customer:"))
                {
                    activePartyType = "customer";
                    var idStr = partyKey.Replace("customer:", "").Replace("c_", "");
                    if (int.TryParse(idStr, out int cId)) customerId = cId;
                    supplierId = null;
                }
                else if (partyKey.StartsWith("s_") || partyKey.StartsWith("supplier:"))
                {
                    activePartyType = "supplier";
                    var idStr = partyKey.Replace("supplier:", "").Replace("s_", "");
                    if (int.TryParse(idStr, out int sId)) supplierId = sId;
                    customerId = null;
                }
            }
            else if (!string.IsNullOrWhiteSpace(partyType))
            {
                activePartyType = partyType.ToLower();
                if (partyId.HasValue && partyId.Value > 0)
                {
                    if (activePartyType == "customer") customerId = partyId.Value;
                    else supplierId = partyId.Value;
                }
            }
            else if (customerId.HasValue && customerId.Value > 0)
            {
                activePartyType = "customer";
                supplierId = null;
            }
            else if (supplierId.HasValue && supplierId.Value > 0)
            {
                activePartyType = "supplier";
                customerId = null;
            }
            else if (partyId.HasValue && partyId.Value > 0)
            {
                supplierId = partyId.Value;
                customerId = null;
            }

            if (activePartyType == "customer")
            {
                supplierId = null;
            }
            else
            {
                customerId = null;
            }

            var model = new SalesReportViewModel
            {
                FromDate = parsedFrom.ToString("yyyy-MM-dd"),
                ToDate = parsedTo.ToString("yyyy-MM-dd"),
                PartyType = activePartyType,
                SelectedSupplierId = supplierId,
                SelectedCustomerId = customerId
            };

            try
            {
                // 1. Fetch active suppliers & customers for filter dropdowns
                model.SuppliersList = await _context.Vendors
                    .AsNoTracking()
                    .Where(v => v.VendorType == VendorType.Supplier && (v.CompanyId == companyId || v.CompanyId == null) && (v.Active == null || v.Active == true))
                    .OrderBy(v => v.Name)
                    .Select(v => new PartyDropdownDto
                    {
                        Id = v.Id,
                        Name = v.Name,
                        CityOrVillage = v.City,
                        Phone = v.Phone
                    })
                    .ToListAsync();

                model.CustomersList = await _context.Vendors
                    .AsNoTracking()
                    .Where(v => v.VendorType == VendorType.Customer && (v.CompanyId == companyId || v.CompanyId == null) && (v.Active == null || v.Active == true))
                    .OrderBy(v => v.Name)
                    .Select(v => new PartyDropdownDto
                    {
                        Id = v.Id,
                        Name = v.Name,
                        CityOrVillage = v.City,
                        Phone = v.Phone
                    })
                    .ToListAsync();

                if (activePartyType == "customer" && customerId.HasValue && customerId.Value > 0)
                {
                    var selCust = await _context.Vendors
                        .AsNoTracking()
                        .Include(v => v.ChartOfAccount)
                        .FirstOrDefaultAsync(v => v.Id == customerId.Value);

                    if (selCust != null)
                    {
                        model.SelectedCustomerId = selCust.Id;
                        model.SelectedCustomerName = selCust.Name;
                        model.SelectedCustomerCity = selCust.City ?? "-";
                        model.SelectedCustomerPhone = selCust.Phone ?? "-";
                        model.SelectedCustomerAccountCode = selCust.ChartOfAccount?.AccountCode ?? "-";
                    }
                }
                else if (activePartyType == "supplier" && supplierId.HasValue && supplierId.Value > 0)
                {
                    var selSup = await _context.Vendors
                        .AsNoTracking()
                        .Include(v => v.ChartOfAccount)
                        .FirstOrDefaultAsync(v => v.Id == supplierId.Value);

                    if (selSup != null)
                    {
                        model.SelectedSupplierId = selSup.Id;
                        model.SelectedSupplierName = selSup.Name;
                        model.SelectedSupplierVillage = selSup.City ?? "-";
                        model.SelectedSupplierPhone = selSup.Phone ?? "-";
                        model.SelectedSupplierAccountCode = selSup.ChartOfAccount?.AccountCode ?? "-";
                    }
                }

                // 2. Fetch Company Details for Report Header
                var company = await _context.CCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId);
                if (company != null)
                {
                    model.CompanyName = company.CompanyName;
                    model.CompanyAddress = company.Address;
                    model.Logo = company.Logo;
                    model.Phone = company.Phone;
                }

                // 3. Try to ensure SP exists and load from SP
                bool spLoaded = await TryLoadSalesReportFromSpAsync(companyId, parsedFrom, parsedTo, supplierId, customerId, model);

                // 4. Fallback to EF Core if SP failed
                if (!spLoaded)
                {
                    await LoadSalesReportFromEfAsync(companyId, fromDateTime, toDateTime, supplierId, customerId, model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Sales Report");
                TempData["ErrorMessage"] = "Failed to load Sales Report: " + ex.Message;
            }

            return View(model);
        }

        [HttpGet]
        [Route("ExportCsv")]
        public async Task<IActionResult> ExportCsv(
            DateTime? fromDate, 
            DateTime? toDate, 
            string? partyKey,
            string? partyType, 
            int? supplierId, 
            int? customerId, 
            int? partyId)
        {
            int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;
            var parsedFrom = fromDate ?? DateTime.Today;
            var parsedTo = toDate ?? DateTime.Today;

            string activePartyType = "supplier";
            if (!string.IsNullOrWhiteSpace(partyKey))
            {
                if (partyKey.StartsWith("c_") || partyKey.StartsWith("customer:"))
                {
                    activePartyType = "customer";
                    var idStr = partyKey.Replace("customer:", "").Replace("c_", "");
                    if (int.TryParse(idStr, out int cId)) customerId = cId;
                    supplierId = null;
                }
                else if (partyKey.StartsWith("s_") || partyKey.StartsWith("supplier:"))
                {
                    activePartyType = "supplier";
                    var idStr = partyKey.Replace("supplier:", "").Replace("s_", "");
                    if (int.TryParse(idStr, out int sId)) supplierId = sId;
                    customerId = null;
                }
            }
            else if (!string.IsNullOrWhiteSpace(partyType))
            {
                activePartyType = partyType.ToLower();
                if (partyId.HasValue && partyId.Value > 0)
                {
                    if (activePartyType == "customer") customerId = partyId.Value;
                    else supplierId = partyId.Value;
                }
            }
            else if (customerId.HasValue && customerId.Value > 0)
            {
                activePartyType = "customer";
                supplierId = null;
            }
            else if (supplierId.HasValue && supplierId.Value > 0)
            {
                activePartyType = "supplier";
                customerId = null;
            }

            if (activePartyType == "customer") supplierId = null;
            else customerId = null;

            var model = new SalesReportViewModel
            {
                FromDate = parsedFrom.ToString("yyyy-MM-dd"),
                ToDate = parsedTo.ToString("yyyy-MM-dd"),
                PartyType = activePartyType,
                SelectedSupplierId = supplierId,
                SelectedCustomerId = customerId
            };

            bool spLoaded = await TryLoadSalesReportFromSpAsync(companyId, parsedFrom, parsedTo, supplierId, customerId, model);
            if (!spLoaded)
            {
                await LoadSalesReportFromEfAsync(companyId, parsedFrom.Date, parsedTo.Date.AddDays(1).AddTicks(-1), supplierId, customerId, model);
            }

            var sb = new StringBuilder();
            sb.AppendLine("Zahid Hussain & Sons - Aarhat Mandi Sales Report");
            sb.AppendLine($"Period: {parsedFrom:dd-MMM-yyyy} to {parsedTo:dd-MMM-yyyy}");
            sb.AppendLine();

            if (activePartyType == "customer" && customerId.HasValue && customerId.Value > 0)
            {
                sb.AppendLine("CUSTOMER PURCHASE SUMMARY");
                sb.AppendLine($"Total Purchased (Receivable),Rs {model.TotalGrossSale:N2}");
                sb.AppendLine($"Total Fish Weight,{model.TotalWeightKg:N2} Kg");
                sb.AppendLine($"Total Lots,{model.TotalLotsCount}");
                sb.AppendLine();
                sb.AppendLine("Bill No,Date,Supplier / Catch,Fish Varieties,Lots,Weight (Kg),Rate / Kg,Total Amount (Receivable),Voucher Ref,Status");

                foreach (var item in model.Items)
                {
                    sb.AppendLine($"\"{item.BillNo}\",\"{item.BillDateFormatted}\",\"{item.SupplierName.Replace("\"", "\"\"")}\",\"{item.FishNames?.Replace("\"", "\"\"")}\",{item.LotsCount},{item.TotalWeightKg},{item.RatePerKg},{item.GrossSaleAmount},\"{item.VoucherNo}\",\"{item.Status}\"");
                }
            }
            else
            {
                sb.AppendLine("SETTLEMENT SUMMARY");
                sb.AppendLine($"Total Sale (Receivable),Rs {model.TotalGrossSale:N2}");
                sb.AppendLine($"Less: Commission (Our Sales),-Rs {model.TotalCommissionSales:N2}");
                sb.AppendLine($"Less: Expenses (Labour+Ice),-Rs {model.TotalExpenses:N2}");
                sb.AppendLine($"Less: Martey / Deposit,-Rs {model.TotalDeposit:N2}");
                sb.AppendLine($"Net Payable to Supplier,Rs {model.TotalNetPayable:N2}");
                sb.AppendLine();
                sb.AppendLine("Bill No,Date,Supplier / Boat,Buyers,Lots,Weight (Kg),Gross Sale (Receivable),Commission %,Commission Amount (Our Sales),Labour Exp,Ice Exp,Total Exp,Martey,Net Payable,Voucher Ref,Status");

                foreach (var item in model.Items)
                {
                    sb.AppendLine($"\"{item.BillNo}\",\"{item.BillDateFormatted}\",\"{item.SupplierName.Replace("\"", "\"\"")}\",\"{item.CustomerNames.Replace("\"", "\"\"")}\",{item.LotsCount},{item.TotalWeightKg},{item.GrossSaleAmount},{item.CommissionPercent},{item.CommissionAmount},{item.LabourExpense},{item.IceExpense},{item.TotalExpense},{item.Deposit},{item.NetPayableAmount},\"{item.VoucherNo}\",\"{item.Status}\"");
                }
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            var fileName = $"SalesReport_{parsedFrom:yyyyMMdd}_to_{parsedTo:yyyyMMdd}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        // =========================================================================
        // DATA ACCESS: STORED PROCEDURE EXECUTION
        // =========================================================================
        private async Task<bool> TryLoadSalesReportFromSpAsync(
            int companyId, 
            DateTime fromDate, 
            DateTime toDate, 
            int? supplierId, 
            int? customerId, 
            SalesReportViewModel model)
        {
            try
            {
                if (!_spDeploymentAttempted)
                {
                    await EnsureStoredProcedureDeployedAsync();
                    _spDeploymentAttempted = true;
                }

                var conn = _context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                {
                    await conn.OpenAsync();
                }

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_GetSalesReport";
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 60;

                var pCompany = cmd.CreateParameter();
                pCompany.ParameterName = "@CompanyId";
                pCompany.Value = companyId;
                cmd.Parameters.Add(pCompany);

                var pFrom = cmd.CreateParameter();
                pFrom.ParameterName = "@FromDate";
                pFrom.Value = fromDate.Date;
                cmd.Parameters.Add(pFrom);

                var pTo = cmd.CreateParameter();
                pTo.ParameterName = "@ToDate";
                pTo.Value = toDate.Date;
                cmd.Parameters.Add(pTo);

                var pSup = cmd.CreateParameter();
                pSup.ParameterName = "@SupplierId";
                pSup.Value = supplierId.HasValue && supplierId.Value > 0 ? (object)supplierId.Value : DBNull.Value;
                cmd.Parameters.Add(pSup);

                var pCust = cmd.CreateParameter();
                pCust.ParameterName = "@CustomerId";
                pCust.Value = customerId.HasValue && customerId.Value > 0 ? (object)customerId.Value : DBNull.Value;
                cmd.Parameters.Add(pCust);

                await using var reader = await cmd.ExecuteReaderAsync();

                // 1. Read Result Set 1: Overall Totals
                if (await reader.ReadAsync())
                {
                    model.TotalGrossSale = !reader.IsDBNull(reader.GetOrdinal("TotalGrossSale")) ? Convert.ToDecimal(reader["TotalGrossSale"]) : 0;
                    model.TotalCommissionSales = !reader.IsDBNull(reader.GetOrdinal("TotalCommissionSales")) ? Convert.ToDecimal(reader["TotalCommissionSales"]) : 0;
                    model.TotalLabourExpense = !reader.IsDBNull(reader.GetOrdinal("TotalLabourExpense")) ? Convert.ToDecimal(reader["TotalLabourExpense"]) : 0;
                    model.TotalIceExpense = !reader.IsDBNull(reader.GetOrdinal("TotalIceExpense")) ? Convert.ToDecimal(reader["TotalIceExpense"]) : 0;
                    model.TotalExpenses = !reader.IsDBNull(reader.GetOrdinal("TotalExpenses")) ? Convert.ToDecimal(reader["TotalExpenses"]) : 0;
                    model.TotalDeposit = !reader.IsDBNull(reader.GetOrdinal("TotalDeposit")) ? Convert.ToDecimal(reader["TotalDeposit"]) : 0;
                    model.TotalNetPayable = !reader.IsDBNull(reader.GetOrdinal("TotalNetPayable")) ? Convert.ToDecimal(reader["TotalNetPayable"]) : 0;
                    model.TotalBillsCount = !reader.IsDBNull(reader.GetOrdinal("TotalBillsCount")) ? Convert.ToInt32(reader["TotalBillsCount"]) : 0;
                    model.TotalLotsCount = !reader.IsDBNull(reader.GetOrdinal("TotalLotsCount")) ? Convert.ToInt32(reader["TotalLotsCount"]) : 0;
                    model.TotalWeightKg = !reader.IsDBNull(reader.GetOrdinal("TotalWeightKg")) ? Convert.ToDecimal(reader["TotalWeightKg"]) : 0;
                    model.TotalCrates = !reader.IsDBNull(reader.GetOrdinal("TotalCrates")) ? Convert.ToInt32(reader["TotalCrates"]) : 0;
                }

                // 2. Read Result Set 2: Detailed Line Items
                if (await reader.NextResultAsync())
                {
                    var items = new List<SalesReportItemDto>();
                    while (await reader.ReadAsync())
                    {
                        decimal itemWeightKg = !reader.IsDBNull(reader.GetOrdinal("TotalWeightKg")) ? Convert.ToDecimal(reader["TotalWeightKg"]) : 0;
                        string itemWeightStr = !reader.IsDBNull(reader.GetOrdinal("WeightStr")) ? reader["WeightStr"]?.ToString() ?? "" : "";
                        if (string.IsNullOrEmpty(itemWeightStr) && itemWeightKg > 0)
                        {
                            itemWeightStr = BillLotItemDto.FormatKgToMaund(itemWeightKg);
                        }

                        items.Add(new SalesReportItemDto
                        {
                            BillId = Convert.ToInt32(reader["BillId"]),
                            BillNo = reader["BillNo"]?.ToString() ?? "",
                            BillDate = Convert.ToDateTime(reader["BillDate"]),
                            SupplierId = !reader.IsDBNull(reader.GetOrdinal("SupplierId")) ? Convert.ToInt32(reader["SupplierId"]) : null,
                            SupplierName = reader["SupplierName"]?.ToString() ?? "General Supplier",
                            SupplierCity = reader["SupplierCity"]?.ToString() ?? "-",
                            CustomerId = !reader.IsDBNull(reader.GetOrdinal("CustomerId")) ? Convert.ToInt32(reader["CustomerId"]) : customerId,
                            CustomerNames = reader["CustomerNames"]?.ToString() ?? "General Buyer",
                            LotNo = !reader.IsDBNull(reader.GetOrdinal("LotNo")) ? reader["LotNo"]?.ToString() : null,
                            FishNames = !reader.IsDBNull(reader.GetOrdinal("FishNames")) ? reader["FishNames"]?.ToString() : null,
                            WeightStr = itemWeightStr,
                            RatePerKg = !reader.IsDBNull(reader.GetOrdinal("RatePerKg")) ? Convert.ToDecimal(reader["RatePerKg"]) : 0,
                            CustomerAmount = !reader.IsDBNull(reader.GetOrdinal("CustomerAmount")) ? Convert.ToDecimal(reader["CustomerAmount"]) : 0,
                            LotsCount = !reader.IsDBNull(reader.GetOrdinal("LotsCount")) ? Convert.ToInt32(reader["LotsCount"]) : 0,
                            TotalCrates = !reader.IsDBNull(reader.GetOrdinal("TotalCrates")) ? Convert.ToInt32(reader["TotalCrates"]) : 0,
                            TotalWeightKg = itemWeightKg,
                            GrossSaleAmount = !reader.IsDBNull(reader.GetOrdinal("GrossSaleAmount")) ? Convert.ToDecimal(reader["GrossSaleAmount"]) : 0,
                            CommissionPercent = !reader.IsDBNull(reader.GetOrdinal("CommissionPercent")) ? Convert.ToDecimal(reader["CommissionPercent"]) : 0,
                            CommissionAmount = !reader.IsDBNull(reader.GetOrdinal("CommissionAmount")) ? Convert.ToDecimal(reader["CommissionAmount"]) : 0,
                            LabourExpense = !reader.IsDBNull(reader.GetOrdinal("LabourExpense")) ? Convert.ToDecimal(reader["LabourExpense"]) : 0,
                            IceExpense = !reader.IsDBNull(reader.GetOrdinal("IceExpense")) ? Convert.ToDecimal(reader["IceExpense"]) : 0,
                            TotalExpense = !reader.IsDBNull(reader.GetOrdinal("TotalExpense")) ? Convert.ToDecimal(reader["TotalExpense"]) : 0,
                            Deposit = !reader.IsDBNull(reader.GetOrdinal("Deposit")) ? Convert.ToDecimal(reader["Deposit"]) : 0,
                            NetPayableAmount = !reader.IsDBNull(reader.GetOrdinal("NetPayableAmount")) ? Convert.ToDecimal(reader["NetPayableAmount"]) : 0,
                            VoucherNo = reader["VoucherNo"]?.ToString(),
                            ExpenseVoucherNo = reader["ExpenseVoucherNo"]?.ToString(),
                            Status = reader["Status"]?.ToString() ?? "Due",
                            Remarks = reader["Remarks"]?.ToString()
                        });
                    }
                    model.Items = items;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "dbo.sp_GetSalesReport failed or not deployed; falling back to direct EF Core query.");
                return false;
            }
        }

        // =========================================================================
        // DATA ACCESS: RESILIENT EF CORE FALLBACK
        // =========================================================================
        private async Task LoadSalesReportFromEfAsync(
            int companyId,
            DateTime fromDateTime,
            DateTime toDateTime,
            int? supplierId,
            int? customerId,
            SalesReportViewModel model)
        {
            var query = _context.BillMasters
                .AsNoTracking()
                .Include(b => b.Supplier)
                .Include(b => b.Customer)
                .Include(b => b.Details)
                    .ThenInclude(d => d.Customer)
                .Include(b => b.Details)
                    .ThenInclude(d => d.Fish)
                .Where(b => (b.CompanyId == companyId || b.CompanyId == null) &&
                            b.BillDate >= fromDateTime &&
                            b.BillDate <= toDateTime);

            if (supplierId.HasValue && supplierId.Value > 0)
            {
                query = query.Where(b => b.SupplierId == supplierId.Value || b.Details.Any(d => d.SupplierId == supplierId.Value));
            }

            if (customerId.HasValue && customerId.Value > 0)
            {
                query = query.Where(b => b.CustomerId == customerId.Value || b.Details.Any(d => d.CustomerId == customerId.Value));
            }

            var bills = await query.OrderByDescending(b => b.BillDate).ThenByDescending(b => b.Id).ToListAsync();

            var billIds = bills.Select(b => b.Id).ToList();
            var billNos = bills.Select(b => b.BillNo).Where(no => !string.IsNullOrEmpty(no)).ToList();

            var vouchers = await _context.Vouchers
                .AsNoTracking()
                .Where(v => (v.InvoiceId.HasValue && billIds.Contains(v.InvoiceId.Value)) ||
                            (!string.IsNullOrEmpty(v.InvoiceNo) && billNos.Contains(v.InvoiceNo)))
                .ToListAsync();

            var items = new List<SalesReportItemDto>();

            foreach (var b in bills)
            {
                var jv = vouchers.FirstOrDefault(v => (v.InvoiceId == b.Id || v.InvoiceNo == b.BillNo) &&
                                                      (v.VoucherType == "JV" || (v.VoucherNo != null && v.VoucherNo.StartsWith("JV-"))));
                var cpv = vouchers.FirstOrDefault(v => (v.InvoiceId == b.Id || v.InvoiceNo == b.BillNo) &&
                                                       (v.VoucherType == "CP" || v.VoucherType == "CPV" || (v.VoucherNo != null && v.VoucherNo.StartsWith("CPV-"))));

                if (customerId.HasValue && customerId.Value > 0)
                {
                    // Customer Mode: List all individual lots purchased by this customer across bills
                    var cLots = b.Details.Where(d => d.CustomerId == customerId.Value || (b.CustomerId == customerId.Value && d.CustomerId == null)).OrderBy(d => d.Id).ToList();
                    if (!cLots.Any()) continue;

                    foreach (var lot in cLots)
                    {
                        string lotNoVal = !string.IsNullOrEmpty(lot.LotNo) ? lot.LotNo : $"LOT-{lot.Id}";
                        string fishNameVal = lot.Fish?.Name ?? "Fish";
                        decimal lotKg = lot.WeightKg;
                        string lotWeightStr = BillLotItemDto.FormatKgToMaund(lotKg);

                        items.Add(new SalesReportItemDto
                        {
                            BillId = b.Id,
                            BillNo = b.BillNo,
                            BillDate = b.BillDate,
                            SupplierId = b.SupplierId,
                            SupplierName = b.Supplier?.Name ?? "General Supplier / Boat",
                            SupplierCity = b.Supplier?.City ?? "-",
                            CustomerId = customerId.Value,
                            CustomerNames = model.SelectedCustomerName ?? b.Customer?.Name ?? "Buyer",
                            LotNo = lotNoVal,
                            FishNames = fishNameVal,
                            WeightStr = lotWeightStr,
                            LotsCount = 1,
                            TotalCrates = lot.Crates ?? 0,
                            TotalWeightKg = lotKg,
                            RatePerKg = lot.RatePerKg,
                            CustomerAmount = lot.Amount,
                            GrossSaleAmount = lot.Amount,
                            CommissionPercent = 0,
                            CommissionAmount = 0,
                            LabourExpense = 0,
                            IceExpense = 0,
                            TotalExpense = 0,
                            Deposit = 0,
                            NetPayableAmount = lot.Amount,
                            VoucherNo = jv?.VoucherNo,
                            ExpenseVoucherNo = cpv?.VoucherNo,
                            Status = b.Status ?? "Due",
                            Remarks = lot.Remarks ?? b.Remarks
                        });
                    }
                }
                else
                {
                    // Supplier / Consolidated Mode
                    var buyerNames = b.Details
                        .Where(d => d.Customer != null)
                        .Select(d => d.Customer!.Name)
                        .Distinct()
                        .ToList();

                    string customerDisplay = buyerNames.Any() 
                        ? string.Join(", ", buyerNames) 
                        : (b.Customer?.Name ?? "General Buyer");

                    var fishNamesList = b.Details.Select(d => d.Fish?.Name ?? "Fish").Distinct().ToList();

                    items.Add(new SalesReportItemDto
                    {
                        BillId = b.Id,
                        BillNo = b.BillNo,
                        BillDate = b.BillDate,
                        SupplierId = b.SupplierId,
                        SupplierName = b.Supplier?.Name ?? "General Supplier / Boat",
                        SupplierCity = b.Supplier?.City ?? "-",
                        CustomerNames = customerDisplay,
                        FishNames = string.Join(", ", fishNamesList),
                        LotsCount = b.Details.Count,
                        TotalCrates = b.Details.Sum(d => d.Crates ?? 0),
                        TotalWeightKg = b.Details.Sum(d => d.WeightKg),
                        GrossSaleAmount = b.TotalGrossAmount,
                        CommissionPercent = b.CommissionPercent,
                        CommissionAmount = b.CommissionAmount,
                        LabourExpense = b.LabourExpense,
                        IceExpense = b.IceExpense,
                        TotalExpense = b.TotalExpense,
                        Deposit = b.Deposit,
                        NetPayableAmount = b.NetPayable,
                        VoucherNo = jv?.VoucherNo,
                        ExpenseVoucherNo = cpv?.VoucherNo,
                        Status = b.Status ?? "Due",
                        Remarks = b.Remarks
                    });
                }
            }

            model.Items = items;
            model.TotalGrossSale = items.Sum(i => i.GrossSaleAmount);
            model.TotalCommissionSales = items.Sum(i => i.CommissionAmount);
            model.TotalLabourExpense = items.Sum(i => i.LabourExpense);
            model.TotalIceExpense = items.Sum(i => i.IceExpense);
            model.TotalExpenses = items.Sum(i => i.TotalExpense);
            model.TotalDeposit = items.Sum(i => i.Deposit);
            model.TotalNetPayable = items.Sum(i => i.NetPayableAmount);
            model.TotalBillsCount = customerId.HasValue && customerId.Value > 0 ? items.Select(i => i.BillId).Distinct().Count() : items.Count;
            model.TotalLotsCount = customerId.HasValue && customerId.Value > 0 ? items.Count : items.Sum(i => i.LotsCount);
            model.TotalWeightKg = items.Sum(i => i.TotalWeightKg);
            model.TotalCrates = items.Sum(i => i.TotalCrates);
        }

        // =========================================================================
        // AUTOMATIC SCHEMA DEPLOYMENT
        // =========================================================================
        private async Task EnsureStoredProcedureDeployedAsync()
        {
            try
            {
                const string sql = @"
CREATE OR ALTER PROCEDURE dbo.sp_GetSalesReport
    @CompanyId INT = 1,
    @FromDate DATE = NULL,
    @ToDate DATE = NULL,
    @SupplierId INT = NULL,
    @CustomerId INT = NULL,
    @FinancialYearId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FromDate IS NULL
        SET @FromDate = CAST(GETDATE() AS DATE);

    IF @ToDate IS NULL
        SET @ToDate = CAST(GETDATE() AS DATE);

    DECLARE @FromDateTime DATETIME = CAST(@FromDate AS DATETIME);
    DECLARE @ToDateTime DATETIME = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@ToDate AS DATETIME)));

    SELECT 
        b.Id AS BillId,
        b.BillNo,
        b.BillDate,
        b.SupplierId,
        ISNULL(s.Name, 'General Supplier / Boat') AS SupplierName,
        ISNULL(s.City, '-') AS SupplierCity,
        b.CustomerId AS MasterCustomerId,
        ISNULL(c.Name, '') AS MasterCustomerName,
        ISNULL(b.TotalGrossAmount, 0) AS GrossSaleAmount,
        ISNULL(b.CommissionPercent, 0) AS CommissionPercent,
        ISNULL(b.CommissionAmount, 0) AS CommissionAmount,
        ISNULL(b.LabourExpense, 0) AS LabourExpense,
        ISNULL(b.LoadingExpense, 0) AS IceExpense,
        ISNULL(b.OtherExpense, 0) AS OtherExpense,
        ISNULL(b.TotalExpense, 0) AS TotalExpense,
        ISNULL(b.Deposit, 0) AS Deposit,
        ISNULL(b.NetPayable, 0) AS NetPayableAmount,
        ISNULL(b.Status, 'Due') AS Status,
        ISNULL(b.Remarks, '') AS Remarks,
        b.CompanyId
    INTO #FilteredBills
    FROM dbo.BillMasters b
    LEFT JOIN dbo.Vendors s ON b.SupplierId = s.Id
    LEFT JOIN dbo.Vendors c ON b.CustomerId = c.Id
    WHERE (b.CompanyId = @CompanyId OR @CompanyId IS NULL OR b.CompanyId IS NULL)
      AND b.BillDate >= @FromDateTime 
      AND b.BillDate <= @ToDateTime
      AND (@SupplierId IS NULL OR b.SupplierId = @SupplierId OR EXISTS (SELECT 1 FROM dbo.BillDetails bd WHERE bd.BillId = b.Id AND bd.SupplierId = @SupplierId))
      AND (@CustomerId IS NULL OR b.CustomerId = @CustomerId OR EXISTS (SELECT 1 FROM dbo.BillDetails bd WHERE bd.BillId = b.Id AND bd.CustomerId = @CustomerId));

    SELECT 
        bd.BillId,
        COUNT(bd.Id) AS LotsCount,
        ISNULL(SUM(bd.Crates), 0) AS TotalCrates,
        ISNULL(SUM(bd.WeightKg), 0) AS TotalWeightKg,
        ISNULL(SUM(bd.Amount), 0) AS CustomerAmount,
        CASE WHEN ISNULL(SUM(bd.WeightKg), 0) > 0 
             THEN ROUND(ISNULL(SUM(bd.Amount), 0) / SUM(bd.WeightKg), 2)
             ELSE 0 
        END AS AvgRate,
        STRING_AGG(DISTINCT ISNULL(cust.Name, 'General Buyer'), ', ') WITHIN GROUP (ORDER BY ISNULL(cust.Name, 'General Buyer')) AS BuyerNames,
        STRING_AGG(DISTINCT ISNULL(fish.Name, 'Fish'), ', ') WITHIN GROUP (ORDER BY ISNULL(fish.Name, 'Fish')) AS FishNames
    INTO #BillLotAggregates
    FROM dbo.BillDetails bd
    INNER JOIN #FilteredBills fb ON bd.BillId = fb.BillId
    LEFT JOIN dbo.Vendors cust ON bd.CustomerId = cust.Id
    LEFT JOIN dbo.Fishes fish ON bd.FishId = fish.Id
    WHERE (@CustomerId IS NULL OR bd.CustomerId = @CustomerId OR (fb.MasterCustomerId = @CustomerId AND bd.CustomerId IS NULL))
    GROUP BY bd.BillId;

    SELECT 
        v.InvoiceNo,
        v.InvoiceId,
        MAX(CASE WHEN v.VoucherType = 'JV' OR v.VoucherNo LIKE 'JV-%' THEN v.VoucherNo END) AS JournalVoucherNo,
        MAX(CASE WHEN v.VoucherType = 'CP' OR v.VoucherType = 'CPV' OR v.VoucherNo LIKE 'CPV-%' THEN v.VoucherNo END) AS ExpenseVoucherNo
    INTO #LinkedVouchers
    FROM dbo.Vouchers v
    WHERE EXISTS (SELECT 1 FROM #FilteredBills fb WHERE fb.BillId = v.InvoiceId OR fb.BillNo = v.InvoiceNo)
    GROUP BY v.InvoiceNo, v.InvoiceId;

    IF @CustomerId IS NOT NULL
    BEGIN
        SELECT 
            ISNULL(SUM(la.CustomerAmount), 0) AS TotalGrossSale,
            0.00 AS TotalCommissionSales,
            0.00 AS TotalLabourExpense,
            0.00 AS TotalIceExpense,
            0.00 AS TotalExpenses,
            0.00 AS TotalDeposit,
            ISNULL(SUM(la.CustomerAmount), 0) AS TotalNetPayable,
            COUNT(DISTINCT fb.BillId) AS TotalBillsCount,
            ISNULL(SUM(la.LotsCount), 0) AS TotalLotsCount,
            ISNULL(SUM(la.TotalWeightKg), 0) AS TotalWeightKg,
            ISNULL(SUM(la.TotalCrates), 0) AS TotalCrates
        FROM #FilteredBills fb
        INNER JOIN #BillLotAggregates la ON fb.BillId = la.BillId;
    END
    ELSE
    BEGIN
        SELECT 
            ISNULL(SUM(fb.GrossSaleAmount), 0) AS TotalGrossSale,
            ISNULL(SUM(fb.CommissionAmount), 0) AS TotalCommissionSales,
            ISNULL(SUM(fb.LabourExpense), 0) AS TotalLabourExpense,
            ISNULL(SUM(fb.IceExpense), 0) AS TotalIceExpense,
            ISNULL(SUM(fb.TotalExpense), 0) AS TotalExpenses,
            ISNULL(SUM(fb.Deposit), 0) AS TotalDeposit,
            ISNULL(SUM(fb.NetPayableAmount), 0) AS TotalNetPayable,
            COUNT(fb.BillId) AS TotalBillsCount,
            ISNULL(SUM(la.LotsCount), 0) AS TotalLotsCount,
            ISNULL(SUM(la.TotalWeightKg), 0) AS TotalWeightKg,
            ISNULL(SUM(la.TotalCrates), 0) AS TotalCrates
        FROM #FilteredBills fb
        LEFT JOIN #BillLotAggregates la ON fb.BillId = la.BillId;
    END

    IF @CustomerId IS NOT NULL
    BEGIN
        SELECT 
            fb.BillId,
            fb.BillNo,
            fb.BillDate,
            fb.SupplierId,
            fb.SupplierName,
            fb.SupplierCity,
            @CustomerId AS CustomerId,
            (SELECT TOP 1 ISNULL(Name, 'Buyer') FROM dbo.Vendors WHERE Id = @CustomerId) AS CustomerNames,
            bd.LotNo,
            ISNULL(fish.Name, 'Fish') AS FishNames,
            '' AS WeightStr,
            1 AS LotsCount,
            ISNULL(bd.Crates, 0) AS TotalCrates,
            ISNULL(bd.WeightKg, 0) AS TotalWeightKg,
            ISNULL(bd.RatePerKg, 0) AS RatePerKg,
            ISNULL(bd.Amount, 0) AS CustomerAmount,
            ISNULL(bd.Amount, 0) AS GrossSaleAmount,
            0.00 AS CommissionPercent,
            0.00 AS CommissionAmount,
            0.00 AS LabourExpense,
            0.00 AS IceExpense,
            0.00 AS TotalExpense,
            0.00 AS Deposit,
            ISNULL(bd.Amount, 0) AS NetPayableAmount,
            ISNULL(lv.JournalVoucherNo, '') AS VoucherNo,
            ISNULL(lv.ExpenseVoucherNo, '') AS ExpenseVoucherNo,
            fb.Status,
            ISNULL(bd.Remarks, fb.Remarks) AS Remarks
        FROM dbo.BillDetails bd
        INNER JOIN #FilteredBills fb ON bd.BillId = fb.BillId
        LEFT JOIN dbo.Fishes fish ON bd.FishId = fish.Id
        LEFT JOIN #LinkedVouchers lv ON (fb.BillId = lv.InvoiceId OR fb.BillNo = lv.InvoiceNo)
        WHERE (bd.CustomerId = @CustomerId OR (fb.MasterCustomerId = @CustomerId AND bd.CustomerId IS NULL))
        ORDER BY fb.BillDate DESC, fb.BillId DESC, bd.Id ASC;
    END
    ELSE
    BEGIN
        SELECT 
            fb.BillId,
            fb.BillNo,
            fb.BillDate,
            fb.SupplierId,
            fb.SupplierName,
            fb.SupplierCity,
            @CustomerId AS CustomerId,
            CASE 
                WHEN la.BuyerNames IS NOT NULL AND LEN(la.BuyerNames) > 0 THEN la.BuyerNames
                WHEN fb.MasterCustomerName IS NOT NULL AND LEN(fb.MasterCustomerName) > 0 THEN fb.MasterCustomerName
                ELSE 'General Buyer'
            END AS CustomerNames,
            NULL AS LotNo,
            la.FishNames,
            '' AS WeightStr,
            ISNULL(la.LotsCount, 0) AS LotsCount,
            ISNULL(la.TotalCrates, 0) AS TotalCrates,
            ISNULL(la.TotalWeightKg, 0) AS TotalWeightKg,
            ISNULL(la.AvgRate, 0) AS RatePerKg,
            ISNULL(la.CustomerAmount, 0) AS CustomerAmount,
            fb.GrossSaleAmount,
            fb.CommissionPercent,
            fb.CommissionAmount,
            fb.LabourExpense,
            fb.IceExpense,
            fb.TotalExpense,
            fb.Deposit,
            fb.NetPayableAmount,
            ISNULL(lv.JournalVoucherNo, '') AS VoucherNo,
            ISNULL(lv.ExpenseVoucherNo, '') AS ExpenseVoucherNo,
            fb.Status,
            fb.Remarks
        FROM #FilteredBills fb
        LEFT JOIN #BillLotAggregates la ON fb.BillId = la.BillId
        LEFT JOIN #LinkedVouchers lv ON (fb.BillId = lv.InvoiceId OR fb.BillNo = lv.InvoiceNo)
        ORDER BY fb.BillDate DESC, fb.BillId DESC;
    END

    DROP TABLE #FilteredBills;
    DROP TABLE #BillLotAggregates;
    DROP TABLE #LinkedVouchers;
END";
                await _context.Database.ExecuteSqlRawAsync(sql);
                _logger.LogInformation("Stored procedure dbo.sp_GetSalesReport successfully deployed/updated.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Automated deployment of dbo.sp_GetSalesReport failed: {Message}", ex.Message);
            }
        }

        private async Task<bool> TryLoadSupplierLedgerFromSpAsync(int companyId, int supplierId, DateTime fromDate, DateTime toDate, SalesReportViewModel model)
        {
            try
            {
                var conn = _context.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                {
                    await conn.OpenAsync();
                }

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_GetPartyLedger";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;

                var pCompany = cmd.CreateParameter();
                pCompany.ParameterName = "@CompanyId";
                pCompany.Value = companyId;
                cmd.Parameters.Add(pCompany);

                var pFrom = cmd.CreateParameter();
                pFrom.ParameterName = "@FromDate";
                pFrom.Value = fromDate.Date;
                cmd.Parameters.Add(pFrom);

                var pTo = cmd.CreateParameter();
                pTo.ParameterName = "@ToDate";
                pTo.Value = toDate.Date;
                cmd.Parameters.Add(pTo);

                var pParty = cmd.CreateParameter();
                pParty.ParameterName = "@PartyId";
                pParty.Value = supplierId;
                cmd.Parameters.Add(pParty);

                var pFy = cmd.CreateParameter();
                pFy.ParameterName = "@FinancialYearId";
                pFy.Value = 1;
                cmd.Parameters.Add(pFy);

                var statementItems = new List<PartyStatementItemDto>();
                decimal openingBal = 0;
                decimal outstandingBal = 0;
                string? supName = null;

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (supName == null && !reader.IsDBNull(reader.GetOrdinal("Name")))
                        supName = reader["Name"]?.ToString();

                    if (!reader.IsDBNull(reader.GetOrdinal("OpeningBalance")))
                        openingBal = Convert.ToDecimal(reader["OpeningBalance"]);

                    if (!reader.IsDBNull(reader.GetOrdinal("OutstandingBalance")))
                        outstandingBal = Convert.ToDecimal(reader["OutstandingBalance"]);

                    DateTime txnDate = !reader.IsDBNull(reader.GetOrdinal("Date"))
                        ? Convert.ToDateTime(reader["Date"])
                        : fromDate;

                    string? vCode = !reader.IsDBNull(reader.GetOrdinal("VoucherCode")) ? reader["VoucherCode"]?.ToString() : null;
                    string? invNo = !reader.IsDBNull(reader.GetOrdinal("InvoiceNo")) ? reader["InvoiceNo"]?.ToString() : null;
                    string remarks = !reader.IsDBNull(reader.GetOrdinal("Remarks")) ? reader["Remarks"]?.ToString() ?? "" : "";
                    string? chqNo = !reader.IsDBNull(reader.GetOrdinal("ChqNo")) ? reader["ChqNo"]?.ToString() : null;
                    DateTime? chqDate = !reader.IsDBNull(reader.GetOrdinal("ChqDate")) ? Convert.ToDateTime(reader["ChqDate"]) : null;

                    decimal debit = !reader.IsDBNull(reader.GetOrdinal("Debit")) ? Convert.ToDecimal(reader["Debit"]) : 0;
                    decimal credit = !reader.IsDBNull(reader.GetOrdinal("Credit")) ? Convert.ToDecimal(reader["Credit"]) : 0;
                    decimal bal = !reader.IsDBNull(reader.GetOrdinal("Balance")) ? Convert.ToDecimal(reader["Balance"]) : 0;

                    string refNo = !string.IsNullOrWhiteSpace(vCode) ? vCode : (!string.IsNullOrWhiteSpace(invNo) ? invNo : "-");

                    if (remarks == "No transactions in selected period" && debit == 0 && credit == 0 && string.IsNullOrWhiteSpace(vCode))
                    {
                        continue;
                    }

                    statementItems.Add(new PartyStatementItemDto
                    {
                        Date = txnDate,
                        RefNo = refNo,
                        VoucherCode = vCode,
                        InvoiceNo = invNo,
                        Description = remarks,
                        ChequeNo = chqNo,
                        ChequeDate = chqDate,
                        Debit = debit,
                        Credit = credit,
                        Balance = bal,
                        OutstandingBalance = outstandingBal,
                        TransactionType = credit > 0 ? "Purchase / Catch" : (debit > 0 ? "Payment / Adaigi" : "Adjustment")
                    });
                }

                if (outstandingBal < 0 && statementItems.Sum(s => s.Credit) >= statementItems.Sum(s => s.Debit))
                {
                    outstandingBal = Math.Abs(outstandingBal);
                    foreach (var item in statementItems)
                    {
                        if (item.Balance < 0 && item.Credit >= item.Debit)
                            item.Balance = Math.Abs(item.Balance);
                    }
                }

                if (!string.IsNullOrEmpty(supName)) model.SelectedSupplierName = supName;
                model.OpeningBalance = openingBal;
                model.TotalPayableSum = outstandingBal;
                model.TotalPaid = statementItems.Sum(s => s.Debit);
                model.TotalPurchased = statementItems.Sum(s => s.Credit);
                model.StatementTransactions = statementItems;

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "sp_GetPartyLedger execution failed in SalesReportController: {Message}", ex.Message);
                return false;
            }
        }

    }
}
