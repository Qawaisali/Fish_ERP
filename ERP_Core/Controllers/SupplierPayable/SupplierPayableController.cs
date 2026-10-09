using System;
using System.Collections.Generic;
using System.Linq;
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

namespace ERP_Core.Controllers.SupplierPayable
{
    [Authorize]
    [FormAccessAuthorize("Farmer Payable")]
    [Route("SupplierPayable")]
    public class SupplierPayableController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<SupplierPayableController> _logger;

        public SupplierPayableController(ERPDbContext context, ILogger<SupplierPayableController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, int? supplierId, string? type = null, int? financialYearId = null, int? fyId = null)
        {
            ViewData["Title"] = "Supplier Payable";
            ViewData["SubTitle"] = "Supplier aur boat wise khareedari (purchases), adaigi (payments) aur baqaya balance summary.";

            int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;

            // Default dates: start of current month to today
            var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var parsedFrom = fromDate ?? startOfMonth;
            var parsedTo = toDate ?? DateTime.Today;

            int effectiveFyId = await GetEffectiveFinancialYearIdAsync(companyId, parsedFrom, financialYearId ?? fyId);

            // Ensure start of day and end of day for precise comparisons
            var fromDateTime = parsedFrom.Date;
            var toDateTime = parsedTo.Date.AddDays(1).AddTicks(-1);

            var model = new SupplierPayableViewModel
            {
                FromDate = parsedFrom.ToString("yyyy-MM-dd"),
                ToDate = parsedTo.ToString("yyyy-MM-dd"),
                SelectedSupplierId = supplierId,
                SelectedType = type
            };

            try
            {
                // Populate Available Transaction Types
                var standardTypes = new List<string> { "All", "Advance", "Martey", "Bill / Catch", "Loan" };
                //try
                //{
                //    var distinctTypes = await _context.AccountTransactions.AsNoTracking()
                //        .Where(t => (t.CompanyId == companyId || t.CompanyId == null) && !string.IsNullOrEmpty(t.Type))
                //        .Select(t => t.Type!)
                //        .Distinct()
                //        .ToListAsync();

                //    foreach (var dt in distinctTypes)
                //    {
                //        if (!string.Equals(dt, "Supplier", StringComparison.OrdinalIgnoreCase) &&
                //            !string.Equals(dt, "Customer", StringComparison.OrdinalIgnoreCase) &&
                //            !string.Equals(dt, "Expense", StringComparison.OrdinalIgnoreCase) &&
                //            !string.Equals(dt, "Commission", StringComparison.OrdinalIgnoreCase) &&
                //            !standardTypes.Any(st => string.Equals(st, dt, StringComparison.OrdinalIgnoreCase)))
                //        {
                //            standardTypes.Add(dt);
                //        }
                //    }
                //}
                //catch { }
                model.AvailableTypes = standardTypes;

                // 1. Fetch active suppliers for dropdown (hierarchical)
                var suppliers = await _context.Vendors
                    .AsNoTracking()
                    .Include(v => v.ParentSupplier)
                    .Include(v => v.ChartOfAccount)
                    .Where(v => v.VendorType == VendorType.Supplier && v.CompanyId == companyId && (v.Active == null || v.Active == true))
                    .OrderBy(v => v.ParentSupplierId.HasValue ? 1 : 0)
                    .ThenBy(v => v.Name)
                    .Select(v => new PartyDropdownDto
                    {
                        Id = v.Id,
                        Name = v.Name,
                        Code = v.ChartOfAccount != null ? v.ChartOfAccount.AccountCode : null,
                        CityOrVillage = v.City,
                        Phone = v.Phone,
                        ParentSupplierId = v.ParentSupplierId,
                        ParentSupplierName = v.ParentSupplier != null ? v.ParentSupplier.Name : null
                    })
                    .ToListAsync();

                model.SuppliersList = suppliers;
                model.TotalSuppliersCount = suppliers.Count;

                // Fetch Company Details for Report Header
                var company = await _context.CCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId);
                if (company != null)
                {
                    model.CompanyName = company.CompanyName;
                    model.CompanyAddress = company.Address;
                    model.Logo = company.Logo;
                }

                bool hasTypeFilter = !string.IsNullOrWhiteSpace(type) && !string.Equals(type, "All", StringComparison.OrdinalIgnoreCase);

                // 2. Load All Suppliers Master Summary
                if (hasTypeFilter)
                {
                    await LoadSupplierPayablesSummaryFromEfAsync(companyId, fromDateTime, toDateTime, model, type);
                }
                else
                {
                    model.Payables = await TryLoadSupplierPayablesSummaryFromSpAsync(companyId, parsedFrom, parsedTo);

                    // Fallback to EF raw fetching ONLY if SP returned empty and suppliers exist
                    if (model.Payables.Count == 0 && model.TotalSuppliersCount > 0)
                    {
                        await LoadSupplierPayablesSummaryFromEfAsync(companyId, fromDateTime, toDateTime, model);
                    }
                }

                // Organize hierarchy (Parent and accordion Children)
                await OrganizeSupplierPayablesHierarchyAsync(companyId, model);

                // 3. Single Supplier Statement View for the "Supplier Ledger Report" subtab
                if (supplierId.HasValue && supplierId.Value > 0)
                {
                    var selectedVendor = await _context.Vendors
                        .AsNoTracking()
                        .Include(v => v.ChartOfAccount)
                        .FirstOrDefaultAsync(v => v.Id == supplierId.Value && v.CompanyId == companyId);

                    if (selectedVendor != null)
                    {
                        model.SelectedSupplierName = selectedVendor.Name;
                        model.SelectedSupplierPhone = selectedVendor.Phone ?? "-";
                        model.SelectedSupplierVillage = selectedVendor.City ?? "-";
                        model.SelectedSupplierAccountCode = selectedVendor.ChartOfAccount?.AccountCode ?? "-";
                    }

                    // Execute dbo.sp_GetPartyLedger
                    await LoadSupplierLedgerFromSpAsync(companyId, supplierId.Value, parsedFrom, parsedTo, effectiveFyId, model);

                    // If Type filter is applied, filter the statement items accordingly
                    if (hasTypeFilter)
                    {
                        model.StatementTransactions = model.StatementTransactions
                            .Where(item => MatchesTransactionType(item.Type, item.Description, item.VoucherCode, type!))
                            .ToList();

                        decimal running = model.OpeningBalance;
                        foreach (var item in model.StatementTransactions)
                        {
                            running += (item.Credit - item.Debit);
                            item.Balance = running;
                        }

                        model.TotalPaid = model.StatementTransactions.Sum(s => s.Debit);
                        model.TotalPurchased = model.StatementTransactions.Sum(s => s.Credit);
                        model.TotalPayableSum = running;
                        model.OutstandingBalance = running;
                    }

                    // Compute Breakdown Totals for Supplier Statement (TotalAdvance, TotalMartey, TotalBillAmount)
                    var directBills = await _context.BillMasters.AsNoTracking()
                        .Include(b => b.Details)
                        .Where(b => (b.CompanyId == companyId || b.CompanyId == 0) && (b.SupplierId == supplierId.Value || b.Details.Any(d => d.SupplierId == supplierId.Value)) && b.BillDate >= fromDateTime && b.BillDate <= toDateTime)
                        .ToListAsync();

                    decimal dbMartey = directBills.Sum(b => b.Deposit);
                    decimal dbNetPayable = directBills.Sum(b => b.NetPayable);

                    decimal txMartey = model.StatementTransactions
                        .Where(s => s.Description != null &&
                                    (s.Description.IndexOf("Martey / Deposit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     ((s.Description.IndexOf("Martey", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       s.Description.IndexOf("Deposit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       s.Description.IndexOf("مارتی", StringComparison.OrdinalIgnoreCase) >= 0) &&
                                      !s.Description.Contains("Martey: Rs 0", StringComparison.OrdinalIgnoreCase) &&
                                      !s.Description.Contains("Catch Settlement", StringComparison.OrdinalIgnoreCase))))
                        .Sum(s => s.Credit > 0 ? s.Credit : s.Debit);

                    model.TotalMartey = dbMartey > 0 ? dbMartey : txMartey;

                    // Catch Settlement / Net Catch payable to supplier (e.g. Rs 60,000)
                    decimal txCatch = model.StatementTransactions
                        .Where(s => s.Credit > 0 && s.Description != null &&
                                    (s.Description.IndexOf("Catch Settlement", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     s.Description.IndexOf("Catch (Payable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     s.Description.IndexOf("Settlement", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     (s.Description.IndexOf("Catch", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                      !s.Description.Contains("Expense", StringComparison.OrdinalIgnoreCase) &&
                                      !s.Description.Contains("Labour", StringComparison.OrdinalIgnoreCase) &&
                                      !s.Description.Contains("Ice", StringComparison.OrdinalIgnoreCase) &&
                                      !s.Description.Contains("Martey / Deposit", StringComparison.OrdinalIgnoreCase))))
                        .Sum(s => s.Credit);

                    model.TotalBillAmount = txCatch > 0 ? txCatch : (dbNetPayable > 0 ? dbNetPayable : model.TotalPurchased);

                    // Total Advance: Sum of actual payments/advances to supplier (excluding farmer expenses paid on behalf)
                    decimal advanceSum = model.StatementTransactions
                        .Where(s => s.Debit > 0 &&
                                    (string.IsNullOrWhiteSpace(s.Description) ||
                                     (!s.Description.Contains("Farmer Expenses", StringComparison.OrdinalIgnoreCase) &&
                                      !s.Description.Contains("paid on behalf of", StringComparison.OrdinalIgnoreCase) &&
                                      !s.Description.Contains("Expense paid on behalf", StringComparison.OrdinalIgnoreCase))))
                        .Sum(s => s.Debit);

                    model.TotalAdvance = advanceSum;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Supplier Payable page");
                if (model.Payables.Count == 0 && model.StatementTransactions.Count == 0)
                {
                    TempData["ErrorMessage"] = "Failed to load Supplier Payable: " + ex.Message;
                }
            }

            return View(model);
        }

        private async Task<int> GetEffectiveFinancialYearIdAsync(int companyId, DateTime fromDate, int? explicitFyId)
        {
            if (explicitFyId.HasValue && explicitFyId.Value > 0)
                return explicitFyId.Value;

            int? sessionFy = HttpContext.Session.GetInt32("FinancialYearId");
            if (sessionFy.HasValue && sessionFy.Value > 0)
                return sessionFy.Value;

            var claim = User.Claims.FirstOrDefault(c => c.Type == "FinancialYearId");
            if (claim != null && int.TryParse(claim.Value, out int fyId) && fyId > 0)
                return fyId;

            var dateMatch = await _context.CFinancialYears
                .AsNoTracking()
                .Where(f => (f.CompanyID == companyId || f.CompanyID == 0) && f.StartDate <= fromDate && f.EndDate >= fromDate && (f.Active == null || f.Active == true))
                .Select(f => f.Id)
                .FirstOrDefaultAsync();

            if (dateMatch > 0)
                return dateMatch;

            var currentMatch = await _context.CFinancialYears
                .AsNoTracking()
                .Where(f => (f.CompanyID == companyId || f.CompanyID == 0) && (f.CurrentYear || f.Active != false))
                .OrderByDescending(f => f.CurrentYear)
                .ThenByDescending(f => f.StartDate)
                .Select(f => f.Id)
                .FirstOrDefaultAsync();

            return currentMatch > 0 ? currentMatch : 1;
        }

        private async Task LoadSupplierLedgerFromSpAsync(
            int companyId,
            int supplierId,
            DateTime fromDate,
            DateTime toDate,
            int financialYearId,
            SupplierPayableViewModel model)
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
                pFy.Value = financialYearId;
                cmd.Parameters.Add(pFy);

                var statementItems = new List<PartyStatementItemDto>();
                decimal openingBal = 0;
                decimal outstandingBal = 0;
                string? supName = null;
                string? compName = null;
                string? compAddress = null;
                string? logo = null;

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (supName == null && !reader.IsDBNull(reader.GetOrdinal("Name")))
                        supName = reader["Name"]?.ToString();

                    if (!reader.IsDBNull(reader.GetOrdinal("OpeningBalance")))
                        openingBal = Convert.ToDecimal(reader["OpeningBalance"]);

                    if (!reader.IsDBNull(reader.GetOrdinal("OutstandingBalance")))
                        outstandingBal = Convert.ToDecimal(reader["OutstandingBalance"]);

                    if (compName == null && !reader.IsDBNull(reader.GetOrdinal("CompanyName")))
                        compName = reader["CompanyName"]?.ToString();

                    if (compAddress == null && !reader.IsDBNull(reader.GetOrdinal("CompanyAddress")))
                        compAddress = reader["CompanyAddress"]?.ToString();

                    if (logo == null && !reader.IsDBNull(reader.GetOrdinal("Logo")))
                        logo = reader["Logo"]?.ToString();

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

                // Enrich description with voucher type (e.g. [Advance], [Martey], [Loan], [Customer Recovery], etc.)
                // ONLY for Payment/Receipt Vouchers (CPV, CRV, BPV, BRV), NEVER for Journal Vouchers (JV) or Bills
                if (statementItems.Any())
                {
                    var vCodes = statementItems
                        .Where(s => !string.IsNullOrEmpty(s.VoucherCode) &&
                                    !s.VoucherCode.StartsWith("JV-", StringComparison.OrdinalIgnoreCase) &&
                                    !s.VoucherCode.StartsWith("JV", StringComparison.OrdinalIgnoreCase))
                        .Select(s => s.VoucherCode!)
                        .Distinct()
                        .ToList();

                    if (vCodes.Any())
                    {
                        var txTypes = await _context.AccountTransactions.AsNoTracking()
                            .Where(t => (t.CompanyId == companyId || t.CompanyId == null) && vCodes.Contains(t.VoucherNo) && !string.IsNullOrEmpty(t.Type))
                            .Select(t => new { t.VoucherNo, t.Type })
                            .Distinct()
                            .ToListAsync();

                        var typeMap = txTypes
                            .Where(t => !string.Equals(t.Type, "General Payment", StringComparison.OrdinalIgnoreCase) &&
                                        !string.Equals(t.Type, "General Receipt", StringComparison.OrdinalIgnoreCase) &&
                                        !string.Equals(t.Type, "Supplier", StringComparison.OrdinalIgnoreCase) &&
                                        !string.Equals(t.Type, "Customer", StringComparison.OrdinalIgnoreCase) &&
                                        !string.Equals(t.Type, "Expense", StringComparison.OrdinalIgnoreCase) &&
                                        !string.Equals(t.Type, "Commission", StringComparison.OrdinalIgnoreCase) &&
                                        !string.Equals(t.Type, "Labour Expense", StringComparison.OrdinalIgnoreCase) &&
                                        !string.Equals(t.Type, "Ice Expense", StringComparison.OrdinalIgnoreCase) &&
                                        !string.Equals(t.Type, "Journal Voucher", StringComparison.OrdinalIgnoreCase))
                            .GroupBy(t => t.VoucherNo)
                            .ToDictionary(g => g.Key, g => g.First().Type, StringComparer.OrdinalIgnoreCase);

                        foreach (var item in statementItems)
                        {
                            if (!string.IsNullOrEmpty(item.VoucherCode) &&
                                !item.VoucherCode.StartsWith("JV-", StringComparison.OrdinalIgnoreCase) &&
                                !item.VoucherCode.StartsWith("JV", StringComparison.OrdinalIgnoreCase) &&
                                typeMap.TryGetValue(item.VoucherCode, out var vType) && !string.IsNullOrWhiteSpace(vType))
                            {
                                if (!item.Description.Contains($"[{vType}]", StringComparison.OrdinalIgnoreCase) && !item.Description.StartsWith(vType, StringComparison.OrdinalIgnoreCase))
                                {
                                    item.Description = $"[{vType}] {item.Description}";
                                }
                            }
                        }
                    }
                }

                // Resolve Type property for each statement transaction
                foreach (var item in statementItems)
                {
                    if (!string.IsNullOrEmpty(item.VoucherCode) &&
                        (item.VoucherCode.StartsWith("JV-", StringComparison.OrdinalIgnoreCase) || item.VoucherCode.StartsWith("JV", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (item.Description.IndexOf("Catch Settlement", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            item.Description.IndexOf("Catch (Payable", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            item.Type = "Bill / Catch";
                        }
                        else if (item.Description.IndexOf("Martey / Deposit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ((item.Description.IndexOf("Martey", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              item.Description.IndexOf("Deposit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              item.Description.IndexOf("مارتی", StringComparison.OrdinalIgnoreCase) >= 0) &&
                             !item.Description.Contains("Martey: Rs 0", StringComparison.OrdinalIgnoreCase)))
                        {
                            item.Type = "Martey";
                        }
                        else if (item.Description.IndexOf("Labour", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            item.Type = "Labour Expense";
                        }
                        else if (item.Description.IndexOf("Ice", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            item.Type = "Ice Expense";
                        }
                        else
                        {
                            item.Type = "Bill / Catch";
                        }
                    }
                    else if (item.Description.StartsWith("[Advance]", StringComparison.OrdinalIgnoreCase) || item.Description.IndexOf("Advance", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        item.Type = "Advance";
                    }
                    else if (item.Description.StartsWith("[Martey]", StringComparison.OrdinalIgnoreCase) || item.Description.IndexOf("Martey", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        item.Type = "Martey";
                    }
                    else if (item.Description.StartsWith("[Loan]", StringComparison.OrdinalIgnoreCase) || item.Description.IndexOf("Loan", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        item.Type = "Loan";
                    }
                    else if (item.Description.IndexOf("Farmer Expenses", StringComparison.OrdinalIgnoreCase) >= 0 || item.Description.IndexOf("paid on behalf of", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        item.Type = "Farmer Expense";
                    }
                    else if (item.Description.IndexOf("Advance", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        item.Type = "Advance";
                    }
                    else if (item.Description.IndexOf("Martey", StringComparison.OrdinalIgnoreCase) >= 0 || item.Description.IndexOf("مارتی", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        item.Type = "Martey";
                    }
                    else if (item.Credit > 0)
                    {
                        item.Type = "Bill / Catch";
                    }
                    else if (item.Debit > 0)
                    {
                        item.Type = "Advance";
                    }
                    else
                    {
                        item.Type = item.Debit > 0 ? "General Payment" : (item.Credit > 0 ? "Bill / Catch" : "Adjustment");
                    }
                }

                // If SP returned negative balance because of Debit - Credit convention,
                // normalize to positive payable convention expected by the view:
                if (openingBal < 0 && statementItems.Any() && statementItems.All(s => s.Debit >= s.Credit))
                {
                    openingBal = Math.Abs(openingBal);
                }
                else if (openingBal < 0 && statementItems.Sum(s => s.Credit) >= statementItems.Sum(s => s.Debit))
                {
                    openingBal = Math.Abs(openingBal);
                }
                else if (outstandingBal < 0 && (statementItems.Count == 0 || statementItems.Sum(s => s.Credit) >= statementItems.Sum(s => s.Debit)))
                {
                    if (openingBal < 0) openingBal = Math.Abs(openingBal);
                    outstandingBal = Math.Abs(outstandingBal);
                    foreach (var item in statementItems)
                    {
                        if (item.Balance < 0 && item.Credit >= item.Debit)
                            item.Balance = Math.Abs(item.Balance);
                    }
                }

                if (!string.IsNullOrEmpty(supName)) model.SelectedSupplierName = supName;
                model.OpeningBalance = openingBal;
                model.OutstandingBalance = outstandingBal;
                model.TotalPayableSum = outstandingBal;
                model.TotalPaid = statementItems.Sum(s => s.Debit);
                model.TotalPurchased = statementItems.Sum(s => s.Credit);
                if (!string.IsNullOrEmpty(compName)) model.CompanyName = compName;
                if (!string.IsNullOrEmpty(compAddress)) model.CompanyAddress = compAddress;
                if (!string.IsNullOrEmpty(logo)) model.Logo = logo;
                model.StatementTransactions = statementItems;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "sp_GetPartyLedger execution failed for SupplierId: {SupplierId}", supplierId);
            }
        }

        private static bool MatchesTransactionType(string? txType, string? narration, string? voucherCode, string filterType)
        {
            if (string.IsNullOrWhiteSpace(filterType) || string.Equals(filterType, "All", StringComparison.OrdinalIgnoreCase))
                return true;

            string cleanType = filterType.Trim();
            string desc = narration ?? "";
            string code = voucherCode ?? "";
            string t = txType ?? "";

            if (string.Equals(cleanType, "Martey", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(t, "Martey", StringComparison.OrdinalIgnoreCase) ||
                       desc.IndexOf("Martey", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       desc.IndexOf("Deposit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       desc.IndexOf("مارتی", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (string.Equals(cleanType, "Advance", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(t, "Advance", StringComparison.OrdinalIgnoreCase) ||
                       desc.IndexOf("Advance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       desc.IndexOf("ایڈوانس", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (string.Equals(cleanType, "Bill", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cleanType, "Bill / Catch", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cleanType, "Catch", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cleanType, "Purchase", StringComparison.OrdinalIgnoreCase))
            {
                // Specifically exclude other distinct transaction types (Martey, Labour, Ice, Advance, Loan, Farmer Expense)
                if (string.Equals(t, "Martey", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t, "Labour Expense", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t, "Ice Expense", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t, "Advance", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t, "Loan", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t, "Farmer Expense", StringComparison.OrdinalIgnoreCase) ||
                    desc.IndexOf("Martey", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    desc.IndexOf("Deposit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    desc.IndexOf("مارتی", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    desc.IndexOf("Labour", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    desc.IndexOf("Ice Expense", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    desc.IndexOf("Ice for", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    desc.IndexOf("Advance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    desc.IndexOf("Loan", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }

                return string.Equals(t, "Bill / Catch", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(t, "Bill", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(t, "Catch", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(t, "Supplier", StringComparison.OrdinalIgnoreCase) ||
                       desc.IndexOf("Catch Settlement", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       desc.IndexOf("Catch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       desc.IndexOf("Brokerage Bill", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       code.StartsWith("BL-", StringComparison.OrdinalIgnoreCase) ||
                       code.StartsWith("BL", StringComparison.OrdinalIgnoreCase) ||
                       code.StartsWith("JV-", StringComparison.OrdinalIgnoreCase) ||
                       code.StartsWith("JV", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(cleanType, "Farmer Expense", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cleanType, "Farmer Expenses", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(t, "Farmer Expense", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(t, "Farmer Expenses", StringComparison.OrdinalIgnoreCase) ||
                       desc.IndexOf("Farmer Expenses", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       desc.IndexOf("paid on behalf of", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (string.Equals(cleanType, "Loan", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(t, "Loan", StringComparison.OrdinalIgnoreCase) ||
                       desc.IndexOf("Loan", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (string.Equals(cleanType, "Labour Expense", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(t, "Labour Expense", StringComparison.OrdinalIgnoreCase) ||
                       desc.IndexOf("Labour", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (string.Equals(cleanType, "Ice Expense", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(t, "Ice Expense", StringComparison.OrdinalIgnoreCase) ||
                       desc.IndexOf("Ice", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return string.Equals(t, cleanType, StringComparison.OrdinalIgnoreCase) ||
                   desc.IndexOf(cleanType, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private async Task<List<SupplierPayableItemDto>> TryLoadSupplierPayablesSummaryFromSpAsync(
            int companyId,
            DateTime fromDate,
            DateTime toDate)
        {
            var list = new List<SupplierPayableItemDto>();
            try
            {
                var conn = _context.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                {
                    await conn.OpenAsync();
                }

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_GetSupplierPayablesSummary";
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

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new SupplierPayableItemDto
                    {
                        SupplierId = Convert.ToInt32(reader["SupplierId"]),
                        SupplierName = reader["SupplierName"]?.ToString() ?? "",
                        AccountCode = !reader.IsDBNull(reader.GetOrdinal("AccountCode")) ? reader["AccountCode"]?.ToString() : null,
                        VillageOrJetty = !reader.IsDBNull(reader.GetOrdinal("VillageOrJetty")) ? reader["VillageOrJetty"]?.ToString() ?? "-" : "-",
                        Phone = !reader.IsDBNull(reader.GetOrdinal("Phone")) ? reader["Phone"]?.ToString() ?? "-" : "-",
                        OpeningBalance = !reader.IsDBNull(reader.GetOrdinal("OpeningBalance")) ? Convert.ToDecimal(reader["OpeningBalance"]) : 0,
                        TotalSold = !reader.IsDBNull(reader.GetOrdinal("TotalSold")) ? Convert.ToDecimal(reader["TotalSold"]) : 0,
                        TotalPaid = !reader.IsDBNull(reader.GetOrdinal("TotalPaid")) ? Convert.ToDecimal(reader["TotalPaid"]) : 0,
                        PayableBalance = !reader.IsDBNull(reader.GetOrdinal("PayableBalance")) ? Convert.ToDecimal(reader["PayableBalance"]) : 0,
                        Status = !reader.IsDBNull(reader.GetOrdinal("Status")) ? reader["Status"]?.ToString() ?? "Clear" : "Clear"
                    });
                }

                // Fallback enrichment: If SP did not include voucher opening balance, enrich from OpeningBalanceDetails
                if (list.Any())
                {
                    var obDetails = await (from d in _context.OpeningBalanceDetails.AsNoTracking()
                                           join m in _context.OpeningBalances.AsNoTracking() on d.OBId equals m.Id
                                           where (m.CompanyId == companyId || m.CompanyId == 0) && (m.Active == null || m.Active == true)
                                           select new { d.COAId, d.OBDtlTranType, d.OBDtlOpenBlnc }).ToListAsync();

                    var vendorsWithCoa = await _context.Vendors.AsNoTracking()
                        .Where(v => v.VendorType == VendorType.Supplier && v.CompanyId == companyId && v.ChartOfAccountId != null)
                        .Select(v => new { v.Id, COAId = v.ChartOfAccountId!.Value })
                        .ToListAsync();

                    var vMap = vendorsWithCoa.ToDictionary(v => v.Id, v => v.COAId);

                    foreach (var item in list)
                    {
                        if (vMap.TryGetValue(item.SupplierId, out int coaId))
                        {
                            decimal vchrOb = obDetails.Where(d => d.COAId == coaId)
                                .Sum(d => string.Equals(d.OBDtlTranType, "CRE", StringComparison.OrdinalIgnoreCase) ? d.OBDtlOpenBlnc : -d.OBDtlOpenBlnc);
                            if (vchrOb != 0)
                            {
                                item.OpeningBalance += vchrOb;
                                item.PayableBalance += vchrOb;
                                item.Status = item.PayableBalance > 0 ? "Payable" : (item.PayableBalance < 0 ? "Advance" : "Clear");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "sp_GetSupplierPayablesSummary execution failed: {Message}", ex.Message);
            }
            return list;
        }

        private async Task LoadSupplierPayablesSummaryFromEfAsync(
            int companyId,
            DateTime fromDateTime,
            DateTime toDateTime,
            SupplierPayableViewModel model,
            string? typeFilter = null)
        {
            var allVendors = await _context.Vendors
                .AsNoTracking()
                .Include(v => v.ChartOfAccount)
                .Where(v => v.VendorType == VendorType.Supplier && v.CompanyId == companyId)
                .OrderBy(v => v.Name)
                .ToListAsync();

            var obDetails = await (from d in _context.OpeningBalanceDetails.AsNoTracking()
                                   join m in _context.OpeningBalances.AsNoTracking() on d.OBId equals m.Id
                                   where (m.CompanyId == companyId || m.CompanyId == 0) && (m.Active == null || m.Active == true)
                                   select new { d.COAId, d.OBDtlTranType, d.OBDtlOpenBlnc }).ToListAsync();

            var allTxs = await (from t in _context.AccountTransactions.AsNoTracking()
                                join v in _context.Vendors.AsNoTracking() on t.ChartofAccountId equals v.ChartOfAccountId
                                where v.VendorType == VendorType.Supplier && v.CompanyId == companyId
                                select t).ToListAsync();

            var summaries = new List<SupplierPayableItemDto>();
            bool hasTypeFilter = !string.IsNullOrWhiteSpace(typeFilter) && !string.Equals(typeFilter, "All", StringComparison.OrdinalIgnoreCase);

            foreach (var v in allVendors)
            {
                decimal vchrOb = 0;
                if (v.ChartOfAccountId.HasValue)
                {
                    vchrOb = obDetails.Where(d => d.COAId == v.ChartOfAccountId.Value)
                        .Sum(d => string.Equals(d.OBDtlTranType, "CRE", StringComparison.OrdinalIgnoreCase) ? d.OBDtlOpenBlnc : -d.OBDtlOpenBlnc);
                }

                decimal opBal = hasTypeFilter ? 0 : (v.OpeningBalance + vchrOb);
                decimal purchased = 0;
                decimal paid = 0;

                if (v.ChartOfAccountId.HasValue)
                {
                    var vTxs = allTxs.Where(t => t.ChartofAccountId == v.ChartOfAccountId.Value).ToList();
                    if (!hasTypeFilter)
                    {
                        opBal += vTxs.Where(t => t.ExtraDate < fromDateTime).Sum(t => t.Credit - t.Debit);
                    }
                    var periodTxs = vTxs.Where(t => t.ExtraDate >= fromDateTime && t.ExtraDate <= toDateTime)
                        .Where(t => !hasTypeFilter || MatchesTransactionType(t.Type, t.Narration, t.VoucherNo, typeFilter!))
                        .ToList();
                    purchased += periodTxs.Sum(t => t.Credit);
                    paid += periodTxs.Sum(t => t.Debit);
                }

                decimal currentBalance = opBal + purchased - paid;
                if (hasTypeFilter && purchased == 0 && paid == 0)
                {
                    continue; // Skip suppliers with no transactions of this type
                }

                string status = currentBalance > 0 ? "Payable" : (currentBalance < 0 ? "Advance" : "Clear");

                summaries.Add(new SupplierPayableItemDto
                {
                    SupplierId = v.Id,
                    SupplierName = v.Name,
                    AccountCode = v.ChartOfAccount?.AccountCode,
                    Phone = v.Phone ?? "-",
                    VillageOrJetty = v.City ?? "-",
                    OpeningBalance = opBal,
                    TotalSold = purchased,
                    TotalPaid = paid,
                    PayableBalance = currentBalance,
                    Status = status
                });
            }

            model.Payables = summaries.OrderByDescending(s => s.PayableBalance).ToList();
        }

        private async Task OrganizeSupplierPayablesHierarchyAsync(int companyId, SupplierPayableViewModel model)
        {
            if (model.Payables == null || !model.Payables.Any())
                return;

            var vendorHierarchy = await _context.Vendors
                .AsNoTracking()
                .Include(v => v.ParentSupplier)
                .Where(v => v.VendorType == VendorType.Supplier && v.CompanyId == companyId)
                .Select(v => new { v.Id, v.ParentSupplierId, ParentName = v.ParentSupplier != null ? v.ParentSupplier.Name : null })
                .ToListAsync();

            var parentMap = vendorHierarchy.ToDictionary(v => v.Id, v => v);

            foreach (var item in model.Payables)
            {
                if (parentMap.TryGetValue(item.SupplierId, out var pInfo))
                {
                    item.ParentSupplierId = pInfo.ParentSupplierId;
                    item.ParentSupplierName = pInfo.ParentName;
                }
            }

            // Calculate overall totals from ALL items before nesting
            model.OpeningBalance = model.Payables.Sum(p => p.OpeningBalance);
            model.TotalOpeningBalance = model.OpeningBalance;
            model.TotalPurchased = model.Payables.Sum(p => p.TotalSold);
            model.TotalPaid = model.Payables.Sum(p => p.TotalPaid);
            model.TotalPayableSum = model.Payables.Sum(p => p.PayableBalance);
            model.OutstandingBalance = model.TotalPayableSum;
            model.TotalAdvance = model.TotalPaid;
            model.TotalBillAmount = model.TotalPurchased;
            model.TotalMartey = 0;

            var itemDict = model.Payables.ToDictionary(p => p.SupplierId, p => p);
            var rootList = new List<SupplierPayableItemDto>();

            foreach (var item in model.Payables)
            {
                if (item.IsChild && itemDict.TryGetValue(item.ParentSupplierId!.Value, out var parentItem))
                {
                    parentItem.Children.Add(item);
                }
                else
                {
                    rootList.Add(item);
                }
            }

            model.Payables = rootList.OrderByDescending(s => s.PayableBalance).ToList();
        }
    }
}
