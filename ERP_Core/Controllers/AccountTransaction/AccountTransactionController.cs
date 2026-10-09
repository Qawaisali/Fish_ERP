using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using ERP_Core.Data;
using ERP_Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP_Core.Controllers.AccountTransactions
{
    [Authorize]
    [Route("AccountTransaction")]
    public class AccountTransactionController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<AccountTransactionController> _logger;

        public AccountTransactionController(ERPDbContext context, ILogger<AccountTransactionController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(
            string? type,
            string? fromDate,
            string? toDate,
            string? search,
            int? accountId)
        {
            ViewData["Title"] = "Account Summary";
            ViewData["SubTitle"] = "Advances, Martey, Loans, Commissions, Expenses aur Other dues ka mukammal party-wise summary record.";

            int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;

            var today = DateTime.Today;
            var firstOfMonth = new DateTime(today.Year, today.Month, 1);

            var effectiveFromStr = string.IsNullOrWhiteSpace(fromDate) ? firstOfMonth.ToString("yyyy-MM-dd") : fromDate;
            var effectiveToStr = string.IsNullOrWhiteSpace(toDate) ? today.ToString("yyyy-MM-dd") : toDate;

            DateTime parsedFrom = DateTime.TryParse(effectiveFromStr, out var pf) ? pf : firstOfMonth;
            DateTime parsedTo = DateTime.TryParse(effectiveToStr, out var pt) ? pt : today;

            var normalizedType = string.IsNullOrWhiteSpace(type) ? "All" : type.Trim();

            var model = new AccountTransactionViewModel
            {
                SelectedType = normalizedType,
                FromDate = effectiveFromStr,
                ToDate = effectiveToStr,
                SearchQuery = search?.Trim(),
                SelectedAccountId = accountId
            };

            // 1. Populate Types Dropdown
            model.AvailableTypes = new List<SelectListItem>
            {
                new SelectListItem { Text = "All Types (تمام اقسام)", Value = "All", Selected = normalizedType.Equals("All", StringComparison.OrdinalIgnoreCase) },
                new SelectListItem { Text = "Advances (پیشگی)", Value = "Advance", Selected = normalizedType.Equals("Advance", StringComparison.OrdinalIgnoreCase) },
                new SelectListItem { Text = "Martey (مارتی)", Value = "Martey", Selected = normalizedType.Equals("Martey", StringComparison.OrdinalIgnoreCase) },
                new SelectListItem { Text = "Loans (قرضہ)", Value = "Loan", Selected = normalizedType.Equals("Loan", StringComparison.OrdinalIgnoreCase) },
                new SelectListItem { Text = "Other Dues (دیگر بقایا)", Value = "Other", Selected = normalizedType.Equals("Other", StringComparison.OrdinalIgnoreCase) },
                new SelectListItem { Text = "Commissions (کمیشن)", Value = "Commission", Selected = normalizedType.Equals("Commission", StringComparison.OrdinalIgnoreCase) },
                new SelectListItem { Text = "Expenses (اخراجات)", Value = "Expense", Selected = normalizedType.Equals("Expense", StringComparison.OrdinalIgnoreCase) }
            };

            try
            {
                // 2. Fetch Company details for branding & print
                var company = await _context.CCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId)
                              ?? await _context.CCompanies.AsNoTracking().FirstOrDefaultAsync();
                if (company != null)
                {
                    model.CompanyName = !string.IsNullOrWhiteSpace(company.CompanyName) ? company.CompanyName : "Zahid Hussain & Sons";
                    model.CompanyAddress = !string.IsNullOrWhiteSpace(company.Address) ? company.Address : "Fish Commission Agents · Sukkur Fish Harbour";
                    var phone = company.Phone?.Trim();
                    var mobile = company.Mobile?.Trim();
                    if (!string.IsNullOrEmpty(phone) && !string.IsNullOrEmpty(mobile) && phone != mobile)
                        model.CompanyPhone = $"{phone} | {mobile}";
                    else if (!string.IsNullOrEmpty(phone))
                        model.CompanyPhone = phone;
                    else if (!string.IsNullOrEmpty(mobile))
                        model.CompanyPhone = mobile;
                }

                // 3. Populate Accounts Dropdown
                var accounts = await _context.ChartOfAccounts
                    .AsNoTracking()
                    .Where(a => a.CompanyId == null || a.CompanyId == companyId)
                    .OrderBy(a => a.AccountName)
                    .Select(a => new SelectListItem
                    {
                        Value = a.Id.ToString(),
                        Text = !string.IsNullOrWhiteSpace(a.AccountCode) ? $"{a.AccountCode} - {a.AccountName}" : a.AccountName,
                        Selected = accountId.HasValue && a.Id == accountId.Value
                    })
                    .ToListAsync();
                model.AvailableAccounts = accounts;

                // 4. Try loading via high-performance Stored Procedure first
                bool spLoaded = await TryLoadSummaryFromSpAsync(companyId, parsedFrom, parsedTo, normalizedType, search, accountId, model);

                // 5. Fallback to EF Core LINQ if Stored Procedure failed or is not yet installed in DB
                if (!spLoaded)
                {
                    await LoadSummaryFromEfAsync(companyId, parsedFrom, parsedTo, normalizedType, search, accountId, model);
                }

                // 6. Compute Aggregates
                model.TotalAccounts = model.AccountsSummary.Count;
                model.TotalVouchers = model.AccountsSummary.Sum(t => t.VoucherCount);
                model.TotalDebit = model.AccountsSummary.Sum(t => t.TotalDebit);
                model.TotalCredit = model.AccountsSummary.Sum(t => t.TotalCredit);
                model.NetAmount = model.AccountsSummary.Sum(t => t.NetBalance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Account Transactions Summary");
                TempData["ErrorMessage"] = "Failed to load summary: " + ex.Message;
            }

            return View(model);
        }

        // =========================================================================
        // HIGH PERFORMANCE STORED PROCEDURE LOADER (ONE ROW PER CHART OF ACCOUNT)
        // =========================================================================
        private async Task<bool> TryLoadSummaryFromSpAsync(
            int companyId,
            DateTime fromDate,
            DateTime toDate,
            string type,
            string? search,
            int? accountId,
            AccountTransactionViewModel model)
        {
            try
            {
                var conn = _context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_GetAccountTransactions";
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 30;

                var pComp = cmd.CreateParameter();
                pComp.ParameterName = "@CompanyId";
                pComp.Value = companyId;
                cmd.Parameters.Add(pComp);

                var pFrom = cmd.CreateParameter();
                pFrom.ParameterName = "@FromDate";
                pFrom.Value = fromDate.Date;
                cmd.Parameters.Add(pFrom);

                var pTo = cmd.CreateParameter();
                pTo.ParameterName = "@ToDate";
                pTo.Value = toDate.Date;
                cmd.Parameters.Add(pTo);

                var pType = cmd.CreateParameter();
                pType.ParameterName = "@Type";
                pType.Value = (object?)type ?? DBNull.Value;
                cmd.Parameters.Add(pType);

                var pSearch = cmd.CreateParameter();
                pSearch.ParameterName = "@Search";
                pSearch.Value = (object?)search ?? DBNull.Value;
                cmd.Parameters.Add(pSearch);

                var pAcc = cmd.CreateParameter();
                pAcc.ParameterName = "@ChartOfAccountId";
                pAcc.Value = (object?)accountId ?? DBNull.Value;
                cmd.Parameters.Add(pAcc);

                var items = new List<AccountSummaryItemDto>();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new AccountSummaryItemDto
                    {
                        ChartOfAccountId = reader.GetInt32(reader.GetOrdinal("ChartofAccountId")),
                        VendorId = reader.IsDBNull(reader.GetOrdinal("VendorId")) ? null : reader.GetInt32(reader.GetOrdinal("VendorId")),
                        AccountName = reader.IsDBNull(reader.GetOrdinal("AccountName")) ? "General Account" : reader.GetString(reader.GetOrdinal("AccountName")),
                        AccountCode = reader.IsDBNull(reader.GetOrdinal("AccountCode")) ? "" : reader.GetString(reader.GetOrdinal("AccountCode")),
                        City = reader.IsDBNull(reader.GetOrdinal("City")) ? "-" : reader.GetString(reader.GetOrdinal("City")),
                        Phone = reader.IsDBNull(reader.GetOrdinal("Phone")) ? "-" : reader.GetString(reader.GetOrdinal("Phone")),
                        TotalDebit = reader.IsDBNull(reader.GetOrdinal("TotalDebit")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("TotalDebit"))),
                        TotalCredit = reader.IsDBNull(reader.GetOrdinal("TotalCredit")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("TotalCredit"))),
                        NetBalance = reader.IsDBNull(reader.GetOrdinal("NetBalance")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("NetBalance"))),
                        VoucherCount = reader.IsDBNull(reader.GetOrdinal("VoucherCount")) ? 0 : reader.GetInt32(reader.GetOrdinal("VoucherCount")),
                        LastTxDate = reader.IsDBNull(reader.GetOrdinal("LastTxDate")) ? null : reader.GetDateTime(reader.GetOrdinal("LastTxDate")),
                        LatestVoucherNo = reader.IsDBNull(reader.GetOrdinal("LatestVoucherNo")) ? null : reader.GetString(reader.GetOrdinal("LatestVoucherNo"))
                    });
                }

                model.AccountsSummary = items;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("sp_GetAccountTransactions summary execution bypassed or failed: {Message}. Falling back to EF Core LINQ.", ex.Message);
                return false;
            }
        }

        // =========================================================================
        // EF CORE LINQ FALLBACK LOADER (ONE ROW PER CHART OF ACCOUNT)
        // =========================================================================
        private async Task LoadSummaryFromEfAsync(
            int companyId,
            DateTime fromDate,
            DateTime toDate,
            string type,
            string? search,
            int? accountId,
            AccountTransactionViewModel model)
        {
            var fromDateTime = fromDate.Date;
            var toDateTime = toDate.Date.AddDays(1).AddSeconds(-1);

            // Fetch Bank COA Ids to exclude balancing cash/bank legs for party queries
            var bankCoaIds = await _context.BankMasters
                .Where(b => b.ChartOfAccountId != null)
                .Select(b => b.ChartOfAccountId!.Value)
                .ToListAsync();

            var query = _context.AccountTransactions
                .AsNoTracking()
                .Where(at => (at.CompanyId == null || at.CompanyId == companyId)
                          && at.ChartofAccountId != null
                          && at.ExtraDate >= fromDateTime
                          && at.ExtraDate <= toDateTime);

            if (accountId.HasValue && accountId.Value > 0)
            {
                query = query.Where(at => at.ChartofAccountId == accountId.Value);
            }

            if (!string.Equals(type, "All", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(type))
            {
                if (string.Equals(type, "Advance", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(at => (at.Type == "Advance" || (at.Narration != null && at.Narration.Contains("Advance")) || (at.EntryTypeName != null && at.EntryTypeName.Contains("Advance")) || (at.ChartofAccountName != null && at.ChartofAccountName.Contains("Advance")))
                                           && (at.ChartofAccountId == null || !bankCoaIds.Contains(at.ChartofAccountId.Value))
                                           && (at.AccountName == null || !at.AccountName.Contains("Cash"))
                                           && (at.ChartofAccountName == null || !at.ChartofAccountName.Contains("Cash")));
                }
                else if (string.Equals(type, "Martey", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(at => (at.Type == "Martey" || (at.Narration != null && at.Narration.Contains("Martey") && !at.Narration.Contains("Martey: Rs 0")) || (at.EntryTypeName != null && at.EntryTypeName.Contains("Martey")))
                                           && (at.ChartofAccountId == null || !bankCoaIds.Contains(at.ChartofAccountId.Value))
                                           && (at.AccountName == null || !at.AccountName.Contains("Cash"))
                                           && (at.ChartofAccountName == null || !at.ChartofAccountName.Contains("Cash")));
                }
                else if (string.Equals(type, "Loan", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(at => (at.Type == "Loan" || (at.Narration != null && at.Narration.Contains("Loan")) || (at.EntryTypeName != null && at.EntryTypeName.Contains("Loan")) || (at.ChartofAccountName != null && at.ChartofAccountName.Contains("Loan")))
                                           && (at.ChartofAccountId == null || !bankCoaIds.Contains(at.ChartofAccountId.Value))
                                           && (at.AccountName == null || !at.AccountName.Contains("Cash"))
                                           && (at.ChartofAccountName == null || !at.ChartofAccountName.Contains("Cash")));
                }
                else if (string.Equals(type, "Other", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(at => (at.Type == "Other" || (at.Narration != null && at.Narration.Contains("Other")) || (at.EntryTypeName != null && at.EntryTypeName.Contains("Other")) || (at.ChartofAccountName != null && at.ChartofAccountName.Contains("Other")))
                                           && (at.ChartofAccountId == null || !bankCoaIds.Contains(at.ChartofAccountId.Value))
                                           && (at.AccountName == null || !at.AccountName.Contains("Cash"))
                                           && (at.ChartofAccountName == null || !at.ChartofAccountName.Contains("Cash")));
                }
                else if (string.Equals(type, "Commission", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(at => at.Type == "Commission" || (at.Narration != null && at.Narration.Contains("Commission")) || (at.ChartofAccountName != null && at.ChartofAccountName.Contains("Commission")));
                }
                else if (string.Equals(type, "Expense", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(at => at.Type == "Expense" || (at.Narration != null && at.Narration.Contains("Expense")) || (at.EntryTypeName != null && at.EntryTypeName.Contains("Expense")) || (at.ChartofAccountName != null && at.ChartofAccountName.Contains("Expense")) || (at.ChartofAccountName != null && at.ChartofAccountName.Contains("Kharch")));
                }
                else
                {
                    query = query.Where(at => at.Type == type);
                }
            }

            // Only consider transactions where Debit > 0 or Credit > 0 or Amount > 0
            query = query.Where(at => at.Debit > 0 || at.Credit > 0 || (at.Amount ?? 0) > 0);

            var rawTxs = await query
                .Select(t => new
                {
                    ChartofAccountId = t.ChartofAccountId!.Value,
                    t.AccountTransactionId,
                    t.Debit,
                    t.Credit,
                    t.Amount,
                    t.ExtraDate,
                    t.VoucherNo,
                    t.AccountName,
                    t.ChartofAccountName
                })
                .ToListAsync();

            if (!rawTxs.Any())
            {
                model.AccountsSummary = new List<AccountSummaryItemDto>();
                return;
            }

            var distinctCoaIds = rawTxs.Select(t => t.ChartofAccountId).Distinct().ToList();

            var coaMap = await _context.ChartOfAccounts
                .AsNoTracking()
                .Where(c => distinctCoaIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c);

            var vendors = await _context.Vendors
                .AsNoTracking()
                .Where(v => v.ChartOfAccountId != null && distinctCoaIds.Contains(v.ChartOfAccountId.Value) && (v.Active == null || v.Active == true))
                .ToListAsync();

            var vendorMap = vendors
                .GroupBy(v => v.ChartOfAccountId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            bool isMartey = string.Equals(type, "Martey", StringComparison.OrdinalIgnoreCase);
            bool isExpense = string.Equals(type, "Expense", StringComparison.OrdinalIgnoreCase);

            var grouped = rawTxs
                .GroupBy(t => t.ChartofAccountId)
                .Select(g =>
                {
                    int coaId = g.Key;
                    coaMap.TryGetValue(coaId, out var coa);
                    vendorMap.TryGetValue(coaId, out var vendor);

                    var firstTx = g.First();
                    string accName = !string.IsNullOrWhiteSpace(vendor?.Name)
                        ? vendor.Name
                        : (!string.IsNullOrWhiteSpace(coa?.AccountName)
                            ? coa.AccountName
                            : (!string.IsNullOrWhiteSpace(firstTx.AccountName)
                                ? firstTx.AccountName
                                : (!string.IsNullOrWhiteSpace(firstTx.ChartofAccountName)
                                    ? firstTx.ChartofAccountName
                                    : $"Account #{coaId}")));

                    string accCode = coa?.AccountCode ?? "";
                    string city = vendor?.City ?? "-";
                    string phone = vendor?.Phone ?? "-";

                    decimal totDebit = g.Sum(x => x.Debit);
                    decimal totCredit = g.Sum(x => x.Credit);
                    decimal netBal = isMartey
                        ? (totCredit - totDebit)
                        : (isExpense ? totDebit : (totDebit - totCredit));

                    var lastTx = g.Max(x => x.ExtraDate);
                    var latestVoucher = g.OrderByDescending(x => x.ExtraDate ?? DateTime.MinValue).Select(x => x.VoucherNo).FirstOrDefault();

                    return new AccountSummaryItemDto
                    {
                        ChartOfAccountId = coaId,
                        VendorId = vendor?.Id,
                        AccountName = accName,
                        AccountCode = accCode,
                        City = city,
                        Phone = phone,
                        TotalDebit = totDebit,
                        TotalCredit = totCredit,
                        NetBalance = netBal,
                        VoucherCount = g.Count(),
                        LastTxDate = lastTx,
                        LatestVoucherNo = latestVoucher
                    };
                })
                .Where(x => x.TotalDebit > 0 || x.TotalCredit > 0)
                .ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                grouped = grouped.Where(r =>
                    r.AccountName.Contains(s, StringComparison.OrdinalIgnoreCase)
                    || (r.AccountCode != null && r.AccountCode.Contains(s, StringComparison.OrdinalIgnoreCase))
                    || r.City.Contains(s, StringComparison.OrdinalIgnoreCase)
                    || r.Phone.Contains(s, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            model.AccountsSummary = grouped
                .OrderByDescending(r => r.NetBalance)
                .ThenByDescending(r => r.TotalDebit)
                .ThenBy(r => r.AccountName)
                .ToList();
        }
    }
}
