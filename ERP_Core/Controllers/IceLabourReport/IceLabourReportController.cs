using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP_Core.Controllers.IceLabourReport
{
    [Authorize]
    [Route("IceLabourReport")]
    public class IceLabourReportController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<IceLabourReportController> _logger;
        private static bool _spDeploymentAttempted = false;

        public IceLabourReportController(ERPDbContext context, ILogger<IceLabourReportController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, int? supplierId, string? expenseType)
        {
            ViewData["Title"] = "Ice & Labour Report";
            ViewData["SubTitle"] = "Mandi boat-wise Ice (Loading) aur Labour expenses reconciliation & analysis.";

            int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;

            var parsedFrom = fromDate ?? DateTime.Today;
            var parsedTo = toDate ?? DateTime.Today;

            var fromDateTime = parsedFrom.Date;
            var toDateTime = parsedTo.Date.AddDays(1).AddTicks(-1);

            string selectedExpenseType = string.IsNullOrWhiteSpace(expenseType) ? "All" : expenseType.Trim();

            var model = new IceLabourReportViewModel
            {
                FromDate = parsedFrom.ToString("yyyy-MM-dd"),
                ToDate = parsedTo.ToString("yyyy-MM-dd"),
                SelectedSupplierId = supplierId,
                ExpenseType = selectedExpenseType
            };

            try
            {
                // 1. Fetch active suppliers for dropdown
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

                if (supplierId.HasValue && supplierId.Value > 0)
                {
                    var selSup = await _context.Vendors
                        .AsNoTracking()
                        .Include(v => v.ChartOfAccount)
                        .FirstOrDefaultAsync(v => v.Id == supplierId.Value);

                    if (selSup != null)
                    {
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
                    var rawName = company.CompanyName ?? "Zahid Hussain & Sons";
                    model.CompanyName = rawName.Replace(" - Fish Commission Agent", "", StringComparison.OrdinalIgnoreCase)
                                               .Replace("- Fish Commission Agent", "", StringComparison.OrdinalIgnoreCase)
                                               .Replace("Fish Commission Agent", "", StringComparison.OrdinalIgnoreCase)
                                               .Trim();
                    model.CompanyAddress = company.Address;
                    model.Logo = company.Logo;
                    model.Phone = company.Phone;
                }

                // 4. Try loading from SP
                bool spSuccess = await TryLoadFromSpAsync(companyId, parsedFrom, parsedTo, supplierId, selectedExpenseType, model);

                // 5. Fallback to EF Core if SP execution did not succeed
                if (!spSuccess)
                {
                    await LoadFromEfAsync(companyId, fromDateTime, toDateTime, supplierId, selectedExpenseType, model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Ice & Labour Report");
                TempData["ErrorMessage"] = "Failed to load Ice & Labour Report: " + ex.Message;
            }

            return View(model);
        }

        private async Task<bool> TryLoadFromSpAsync(
            int companyId,
            DateTime fromDate,
            DateTime toDate,
            int? supplierId,
            string expenseType,
            IceLabourReportViewModel model)
        {
            try
            {
                var conn = _context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                {
                    await conn.OpenAsync();
                }

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_GetIceLabourReport";
                cmd.CommandType = CommandType.StoredProcedure;

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
                pSup.Value = (object?)supplierId ?? DBNull.Value;
                cmd.Parameters.Add(pSup);

                var pExp = cmd.CreateParameter();
                pExp.ParameterName = "@ExpenseType";
                pExp.Value = string.IsNullOrWhiteSpace(expenseType) ? "All" : expenseType;
                cmd.Parameters.Add(pExp);

                await using var reader = await cmd.ExecuteReaderAsync();

                // 1. Result Set 1: Overall Summary KPIs
                if (await reader.ReadAsync())
                {
                    model.TotalBillsCount = !reader.IsDBNull(reader.GetOrdinal("TotalBillsCount")) ? Convert.ToInt32(reader["TotalBillsCount"]) : 0;
                    model.TotalGrossCatch = !reader.IsDBNull(reader.GetOrdinal("TotalGrossCatch")) ? Convert.ToDecimal(reader["TotalGrossCatch"]) : 0;
                    model.TotalIceExpense = !reader.IsDBNull(reader.GetOrdinal("TotalIceExpense")) ? Convert.ToDecimal(reader["TotalIceExpense"]) : 0;
                    model.TotalLabourExpense = !reader.IsDBNull(reader.GetOrdinal("TotalLabourExpense")) ? Convert.ToDecimal(reader["TotalLabourExpense"]) : 0;
                    model.TotalOtherExpense = !reader.IsDBNull(reader.GetOrdinal("TotalOtherExpense")) ? Convert.ToDecimal(reader["TotalOtherExpense"]) : 0;
                    model.TotalNetPayable = !reader.IsDBNull(reader.GetOrdinal("TotalNetPayable")) ? Convert.ToDecimal(reader["TotalNetPayable"]) : 0;
                }

                // 2. Result Set 2: Line Items
                if (await reader.NextResultAsync())
                {
                    var items = new List<IceLabourReportItemDto>();
                    while (await reader.ReadAsync())
                    {
                        var iceVal = !reader.IsDBNull(reader.GetOrdinal("IceExpense")) ? Convert.ToDecimal(reader["IceExpense"]) : 0;
                        var labVal = !reader.IsDBNull(reader.GetOrdinal("LabourExpense")) ? Convert.ToDecimal(reader["LabourExpense"]) : 0;
                        var combVal = !reader.IsDBNull(reader.GetOrdinal("CombinedExpense")) ? Convert.ToDecimal(reader["CombinedExpense"]) : (iceVal + labVal);

                        if (string.Equals(expenseType, "Ice", StringComparison.OrdinalIgnoreCase))
                        {
                            labVal = 0;
                            combVal = iceVal;
                        }
                        else if (string.Equals(expenseType, "Labour", StringComparison.OrdinalIgnoreCase))
                        {
                            iceVal = 0;
                            combVal = labVal;
                        }

                        items.Add(new IceLabourReportItemDto
                        {
                            BillId = Convert.ToInt32(reader["BillId"]),
                            BillNo = reader["BillNo"]?.ToString() ?? "",
                            BillDate = Convert.ToDateTime(reader["BillDate"]),
                            SupplierId = !reader.IsDBNull(reader.GetOrdinal("SupplierId")) ? Convert.ToInt32(reader["SupplierId"]) : null,
                            SupplierName = reader["SupplierName"]?.ToString() ?? "",
                            SupplierCity = !reader.IsDBNull(reader.GetOrdinal("SupplierCity")) ? reader["SupplierCity"]?.ToString() : "-",
                            BuyerNames = !reader.IsDBNull(reader.GetOrdinal("BuyerNames")) ? reader["BuyerNames"]?.ToString() : "General Buyer",
                            LotsCount = !reader.IsDBNull(reader.GetOrdinal("LotsCount")) ? Convert.ToInt32(reader["LotsCount"]) : 0,
                            TotalGrossCatch = !reader.IsDBNull(reader.GetOrdinal("TotalGrossCatch")) ? Convert.ToDecimal(reader["TotalGrossCatch"]) : 0,
                            IceExpense = iceVal,
                            LabourExpense = labVal,
                            OtherExpense = !reader.IsDBNull(reader.GetOrdinal("OtherExpense")) ? Convert.ToDecimal(reader["OtherExpense"]) : 0,
                            TotalExpense = !reader.IsDBNull(reader.GetOrdinal("TotalExpense")) ? Convert.ToDecimal(reader["TotalExpense"]) : 0,
                            CommissionAmount = !reader.IsDBNull(reader.GetOrdinal("CommissionAmount")) ? Convert.ToDecimal(reader["CommissionAmount"]) : 0,
                            NetPayable = !reader.IsDBNull(reader.GetOrdinal("NetPayable")) ? Convert.ToDecimal(reader["NetPayable"]) : 0,
                            Status = reader["Status"]?.ToString() ?? "Due",
                            Remarks = !reader.IsDBNull(reader.GetOrdinal("Remarks")) ? reader["Remarks"]?.ToString() : ""
                        });
                    }
                    model.Items = items;
                    model.TotalBillsCount = items.Count;

                    if (string.Equals(expenseType, "Ice", StringComparison.OrdinalIgnoreCase))
                    {
                        model.TotalLabourExpense = 0;
                    }
                    else if (string.Equals(expenseType, "Labour", StringComparison.OrdinalIgnoreCase))
                    {
                        model.TotalIceExpense = 0;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "sp_GetIceLabourReport execution failed; falling back to direct EF Core query: {Message}", ex.Message);
                return false;
            }
        }

        private async Task LoadFromEfAsync(
            int companyId,
            DateTime fromDateTime,
            DateTime toDateTime,
            int? supplierId,
            string expenseType,
            IceLabourReportViewModel model)
        {
            var query = _context.BillMasters.AsNoTracking()
                .Include(b => b.Supplier)
                .Include(b => b.Customer)
                .Include(b => b.Details)
                    .ThenInclude(d => d.Customer)
                .Where(b => (b.CompanyId == companyId || b.CompanyId == null) && b.BillDate >= fromDateTime && b.BillDate <= toDateTime);

            if (supplierId.HasValue && supplierId.Value > 0)
            {
                query = query.Where(b => b.SupplierId == supplierId.Value || b.Details.Any(d => d.SupplierId == supplierId.Value));
            }

            if (string.Equals(expenseType, "Ice", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(b => b.IceExpense > 0);
            }
            else if (string.Equals(expenseType, "Labour", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(b => b.LabourExpense > 0);
            }
            else
            {
                query = query.Where(b => b.IceExpense > 0 || b.LabourExpense > 0);
            }

            var bills = await query.OrderByDescending(b => b.BillDate).ThenByDescending(b => b.Id).ToListAsync();

            var items = new List<IceLabourReportItemDto>();
            foreach (var b in bills)
            {
                var buyerNames = b.Details.Any(d => d.Customer != null)
                    ? string.Join(", ", b.Details.Where(d => d.Customer != null).Select(d => d.Customer!.Name).Distinct())
                    : (b.Customer?.Name ?? "General Buyer");

                decimal itmIce = string.Equals(expenseType, "Labour", StringComparison.OrdinalIgnoreCase) ? 0 : b.IceExpense;
                decimal itmLabour = string.Equals(expenseType, "Ice", StringComparison.OrdinalIgnoreCase) ? 0 : b.LabourExpense;

                items.Add(new IceLabourReportItemDto
                {
                    BillId = b.Id,
                    BillNo = b.BillNo,
                    BillDate = b.BillDate,
                    SupplierId = b.SupplierId,
                    SupplierName = b.Supplier?.Name ?? "General Boat / Supplier",
                    SupplierCity = b.Supplier?.City ?? "-",
                    BuyerNames = buyerNames,
                    LotsCount = b.Details.Count,
                    TotalGrossCatch = b.TotalGrossAmount,
                    IceExpense = itmIce,
                    LabourExpense = itmLabour,
                    OtherExpense = b.OtherExpense,
                    TotalExpense = b.TotalExpense,
                    CommissionAmount = b.CommissionAmount,
                    NetPayable = b.NetPayable,
                    Status = b.Status ?? "Due",
                    Remarks = b.Remarks
                });
            }

            model.Items = items;
            model.TotalBillsCount = items.Count;
            model.TotalIceExpense = string.Equals(expenseType, "Labour", StringComparison.OrdinalIgnoreCase) ? 0 : items.Sum(i => i.IceExpense);
            model.TotalLabourExpense = string.Equals(expenseType, "Ice", StringComparison.OrdinalIgnoreCase) ? 0 : items.Sum(i => i.LabourExpense);
            model.TotalGrossCatch = items.Sum(i => i.TotalGrossCatch);
            model.TotalOtherExpense = items.Sum(i => i.OtherExpense);
            model.TotalNetPayable = items.Sum(i => i.NetPayable);
        }
    }
}
