using System;
using System.Collections.Generic;
using System.Data;
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

namespace ERP_Core.Controllers.CustomerReceivable
{
    [Authorize]
    [FormAccessAuthorize("Supplier Receivables")]
    [Route("CustomerReceivable")]
    public class CustomerReceivableController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<CustomerReceivableController> _logger;

        public CustomerReceivableController(ERPDbContext context, ILogger<CustomerReceivableController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, int? customerId, int? financialYearId = null, int? fyId = null)
        {
            ViewData["Title"] = "Customer Receivable";
            ViewData["SubTitle"] = "Customer wise bikri (sales), wasooli (receipts) aur baqaya balance report.";

            int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;

            var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var parsedFrom = fromDate ?? startOfMonth;
            var parsedTo = toDate ?? DateTime.Today;

            int effectiveFyId = await GetEffectiveFinancialYearIdAsync(companyId, parsedFrom, financialYearId ?? fyId);

            var model = new CustomerReceivableViewModel
            {
                FromDate = parsedFrom.ToString("yyyy-MM-dd"),
                ToDate = parsedTo.ToString("yyyy-MM-dd"),
                SelectedCustomerId = customerId
            };

            try
            {
                // 1. Fetch active customers for dropdown
                var customers = await _context.Vendors
                    .AsNoTracking()
                    .Where(v => v.VendorType == VendorType.Customer && v.CompanyId == companyId && (v.Active == null || v.Active == true))
                    .OrderBy(v => v.Name)
                    .Select(v => new PartyDropdownDto
                    {
                        Id = v.Id,
                        Name = v.Name,
                        Code = v.ChartOfAccount != null ? v.ChartOfAccount.AccountCode : null,
                        CityOrVillage = v.City,
                        Phone = v.Phone
                    })
                    .ToListAsync();

                model.CustomersList = customers;
                model.TotalCustomersCount = customers.Count;

                // Fetch Company Details for Report Header
                var company = await _context.CCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId);
                if (company != null)
                {
                    model.CompanyName = company.CompanyName;
                    model.CompanyAddress = company.Address;
                    model.Logo = company.Logo;
                }

                // 2. Load All Customers Master Summary via Stored Procedure
                model.Receivables = await LoadCustomerReceivablesSummaryFromSpAsync(companyId, parsedFrom, parsedTo);

                // 3. Single Customer Statement View for the "Customer Ledger Report" subtab
                if (customerId.HasValue && customerId.Value > 0)
                {
                    var selectedVendor = await _context.Vendors
                        .AsNoTracking()
                        .Include(v => v.ChartOfAccount)
                        .FirstOrDefaultAsync(v => v.Id == customerId.Value && v.CompanyId == companyId);

                    if (selectedVendor != null)
                    {
                        model.SelectedCustomerName = selectedVendor.Name;
                        model.SelectedCustomerPhone = selectedVendor.Phone ?? "-";
                        model.SelectedCustomerCity = selectedVendor.City ?? "-";
                        model.SelectedCustomerAccountCode = selectedVendor.ChartOfAccount?.AccountCode ?? "-";
                    }

                    // Execute dbo.sp_GetPartyLedger
                    await LoadCustomerLedgerFromSpAsync(companyId, customerId.Value, parsedFrom, parsedTo, effectiveFyId, model);
                }
                else
                {
                    // Overall totals for all customers summary view
                    model.OpeningBalance = model.Receivables.Sum(r => r.OpeningBalance);
                    model.TotalBilled = model.Receivables.Sum(r => r.TotalPurchased);
                    model.TotalReceived = model.Receivables.Sum(r => r.TotalReceived);
                    model.TotalReceivableSum = model.Receivables.Sum(r => r.ReceivableBalance);
                    model.OutstandingBalance = model.TotalReceivableSum;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Customer Receivable page");
                if (model.Receivables.Count == 0 && model.StatementTransactions.Count == 0)
                {
                    TempData["ErrorMessage"] = "Failed to load Customer Receivable: " + ex.Message;
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

            var today = DateTime.Today;
            var currentMatch = await _context.CFinancialYears
                .AsNoTracking()
                .Where(f => (f.CompanyID == companyId || f.CompanyID == 0) && f.StartDate <= today && f.EndDate >= today && (f.Active == null || f.Active == true))
                .Select(f => f.Id)
                .FirstOrDefaultAsync();

            return currentMatch > 0 ? currentMatch : 1;
        }

        private async Task LoadCustomerLedgerFromSpAsync(
            int companyId,
            int customerId,
            DateTime fromDate,
            DateTime toDate,
            int financialYearId,
            CustomerReceivableViewModel model)
        {
            try
            {
                var conn = _context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                {
                    await conn.OpenAsync();
                }

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_GetPartyLedger";
                cmd.CommandType = CommandType.StoredProcedure;

                var pCompany = cmd.CreateParameter();
                pCompany.ParameterName = "@CompanyId";
                pCompany.Value = companyId;
                cmd.Parameters.Add(pCompany);

                var pParty = cmd.CreateParameter();
                pParty.ParameterName = "@PartyId";
                pParty.Value = customerId;
                cmd.Parameters.Add(pParty);

                var pFrom = cmd.CreateParameter();
                pFrom.ParameterName = "@FromDate";
                pFrom.Value = fromDate.Date;
                cmd.Parameters.Add(pFrom);

                var pTo = cmd.CreateParameter();
                pTo.ParameterName = "@ToDate";
                pTo.Value = toDate.Date;
                cmd.Parameters.Add(pTo);

                var pFy = cmd.CreateParameter();
                pFy.ParameterName = "@FinancialYearId";
                pFy.Value = financialYearId;
                cmd.Parameters.Add(pFy);

                var statementItems = new List<PartyStatementItemDto>();
                decimal openingBal = 0;
                decimal outstandingBal = 0;
                string? custName = null;
                string? compName = null;
                string? compAddress = null;
                string? logo = null;

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (custName == null && !reader.IsDBNull(reader.GetOrdinal("Name")))
                        custName = reader["Name"]?.ToString();

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
                        TransactionType = debit > 0 ? "Sale / Bill" : (credit > 0 ? "Receipt / Wasooli" : "Adjustment")
                    });
                }

                // Enrich description with voucher type (e.g. [Advance], [Martey], [Loan], [Customer Recovery], etc.)
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

                if (!string.IsNullOrEmpty(custName)) model.SelectedCustomerName = custName;
                model.OpeningBalance = openingBal;
                model.OutstandingBalance = outstandingBal;
                model.TotalReceivableSum = outstandingBal;
                model.TotalBilled = statementItems.Sum(s => s.Debit);
                model.TotalReceived = statementItems.Sum(s => s.Credit);
                if (!string.IsNullOrEmpty(compName)) model.CompanyName = compName;
                if (!string.IsNullOrEmpty(compAddress)) model.CompanyAddress = compAddress;
                if (!string.IsNullOrEmpty(logo)) model.Logo = logo;
                model.StatementTransactions = statementItems;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "sp_GetPartyLedger execution failed for CustomerId: {CustomerId}", customerId);
            }
        }

        private async Task<List<CustomerReceivableItemDto>> LoadCustomerReceivablesSummaryFromSpAsync(
            int companyId,
            DateTime fromDate,
            DateTime toDate)
        {
            var list = new List<CustomerReceivableItemDto>();
            try
            {
                var conn = _context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                {
                    await conn.OpenAsync();
                }

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_GetCustomerReceivablesSummary";
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

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new CustomerReceivableItemDto
                    {
                        CustomerId = Convert.ToInt32(reader["CustomerId"]),
                        CustomerName = reader["CustomerName"]?.ToString() ?? "",
                        AccountCode = !reader.IsDBNull(reader.GetOrdinal("AccountCode")) ? reader["AccountCode"]?.ToString() : null,
                        City = !reader.IsDBNull(reader.GetOrdinal("City")) ? reader["City"]?.ToString() ?? "-" : "-",
                        Phone = !reader.IsDBNull(reader.GetOrdinal("Phone")) ? reader["Phone"]?.ToString() ?? "-" : "-",
                        OpeningBalance = !reader.IsDBNull(reader.GetOrdinal("OpeningBalance")) ? Convert.ToDecimal(reader["OpeningBalance"]) : 0,
                        TotalPurchased = !reader.IsDBNull(reader.GetOrdinal("TotalInvoiced")) ? Convert.ToDecimal(reader["TotalInvoiced"]) : 0,
                        TotalReceived = !reader.IsDBNull(reader.GetOrdinal("TotalPaid")) ? Convert.ToDecimal(reader["TotalPaid"]) : 0,
                        ReceivableBalance = !reader.IsDBNull(reader.GetOrdinal("ReceivableBalance")) ? Convert.ToDecimal(reader["ReceivableBalance"]) : 0,
                        Status = !reader.IsDBNull(reader.GetOrdinal("Status")) ? reader["Status"]?.ToString() ?? "Clear" : "Clear"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "sp_GetCustomerReceivablesSummary execution failed: {Message}", ex.Message);
            }
            return list;
        }
    }
}
