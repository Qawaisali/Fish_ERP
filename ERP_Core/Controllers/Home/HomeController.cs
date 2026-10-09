using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP_Core.Controllers.Home
{
    [Authorize]
    [Route("")]
    [Route("Home")]
    public class HomeController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<HomeController> _logger;
        private static DateTime _lastSpDeployTime = DateTime.MinValue;

        public HomeController(ERPDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(string? fromDate, string? toDate, string? duration = "today")
        {
            ViewData["Title"] = "Dashboard";
            ViewData["SubTitle"] = "Overview of today's mandi business";

            var parsedFrom = ParseDateFlexible(fromDate);
            var parsedTo = ParseDateFlexible(toDate);
            var (from, to) = ResolveDurationDates(duration, parsedFrom, parsedTo);

            var viewModel = new AarhatDashboardViewModel
            {
                Duration = duration ?? "today",
                SelectedFromDate = from?.ToString("yyyy-MM-dd"),
                SelectedToDate = to?.ToString("yyyy-MM-dd")
            };

            int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;
            var (receivables, payables, advances, commissions, weeklyTrends, monthlyTrends, recentBills, periodBillsCount, periodSaleAmount, periodCommissionAmount, bankBalances, marteyItems, loanItems, otherItems) = await LoadPayablesAndReceivablesSummaryAsync(companyId, from, to);
            PopulateFinancialSummaries(viewModel, receivables, payables, advances, commissions, weeklyTrends, monthlyTrends, recentBills, periodBillsCount, periodSaleAmount, periodCommissionAmount, bankBalances, marteyItems, loanItems, otherItems);
            await PopulateMarteyExpensesAndStaffAsync(viewModel, companyId, from, to);

            return View(viewModel);
        }

        [HttpGet]
        [Route("~/Home/GetDashboardSummaryData")]
        [Route("~/GetDashboardSummaryData")]
        [Route("Home/GetDashboardSummaryData")]
        [Route("GetDashboardSummaryData")]
        public async Task<IActionResult> GetDashboardSummaryData(string? fromDate, string? toDate, string? duration = "today")
        {
            int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;
            var parsedFrom = ParseDateFlexible(fromDate);
            var parsedTo = ParseDateFlexible(toDate);
            var (from, to) = ResolveDurationDates(duration, parsedFrom, parsedTo);

            var (receivables, payables, advances, commissions, weeklyTrends, monthlyTrends, recentBills, periodBillsCount, periodSaleAmount, periodCommissionAmount, bankBalances, marteyItems, loanItems, otherItems) = await LoadPayablesAndReceivablesSummaryAsync(companyId, from, to);

            var tempVm = new AarhatDashboardViewModel
            {
                Duration = duration ?? "today",
                SelectedFromDate = from?.ToString("yyyy-MM-dd"),
                SelectedToDate = to?.ToString("yyyy-MM-dd")
            };
            PopulateFinancialSummaries(tempVm, receivables, payables, advances, commissions, weeklyTrends, monthlyTrends, recentBills, periodBillsCount, periodSaleAmount, periodCommissionAmount, bankBalances, marteyItems, loanItems, otherItems);
            await PopulateMarteyExpensesAndStaffAsync(tempVm, companyId, from, to);

            return Json(new
            {
                success = true,
                duration = tempVm.Duration,
                fromDate = from?.ToString("yyyy-MM-dd"),
                toDate = to?.ToString("yyyy-MM-dd"),
                todayBillsCount = tempVm.TodayBillsCount,
                totalReceivable = tempVm.TotalReceivable,
                activeCustomersCount = tempVm.ActiveCustomersCount,
                totalPayable = tempVm.TotalPayable,
                activeSuppliersCount = tempVm.ActiveSuppliersCount,
                commissionEarned = tempVm.CommissionEarned,
                recentBills = tempVm.RecentBills,
                topPayables = tempVm.TopPayables,
                recentPayables = tempVm.RecentPayables,
                topPayablesCount = tempVm.TopPayablesCount,
                topPayablesTotal = tempVm.TopPayablesTotal,
                topReceivables = tempVm.TopReceivables,
                recentReceivables = tempVm.RecentReceivables,
                topReceivablesCount = tempVm.TopReceivablesCount,
                topReceivablesTotal = tempVm.TopReceivablesTotal,
                topAdvances = tempVm.TopAdvances,
                recentAdvances = tempVm.RecentAdvances,
                topAdvancesCount = tempVm.TopAdvancesCount,
                topAdvancesTotal = tempVm.TopAdvancesTotal,
                topMartey = tempVm.TopMartey,
                recentMartey = tempVm.RecentMartey,
                topMarteyCount = tempVm.TopMarteyCount,
                topMarteyTotal = tempVm.TopMarteyTotal,
                topLoans = tempVm.TopLoans,
                recentLoans = tempVm.RecentLoans,
                topLoansCount = tempVm.TopLoansCount,
                topLoansTotal = tempVm.TopLoansTotal,
                topOthers = tempVm.TopOthers,
                recentOthers = tempVm.RecentOthers,
                topOthersCount = tempVm.TopOthersCount,
                topOthersTotal = tempVm.TopOthersTotal,
                topCommissions = tempVm.TopCommissions,
                recentCommissions = tempVm.RecentCommissions,
                topCommissionsCount = tempVm.TopCommissionsCount,
                topCommissionsTotal = tempVm.TopCommissionsTotal,
                topExpenses = tempVm.TopExpenses,
                recentExpenses = tempVm.RecentExpenses,
                topExpensesCount = tempVm.TopExpensesCount,
                topExpensesTotal = tempVm.TopExpensesTotal,
                weeklyTrends = tempVm.WeeklyTrends,
                monthlyTrends = tempVm.MonthlyTrends,
                bankBalances = tempVm.BankBalances,
                totalLiquidFunds = tempVm.TotalLiquidFunds,
                totalBankFunds = tempVm.TotalBankFunds,
                totalCashInHand = tempVm.TotalCashInHand,
                staffMembers = tempVm.StaffMembers
            });
        }

        private static DateTime? ParseDateFlexible(string? dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr)) return null;
            string clean = dateStr.Trim();

            int spaceIdx = clean.IndexOf(' ');
            string dateOnly = spaceIdx > 0 ? clean.Substring(0, spaceIdx) : clean;

            string[] formats = {
                "yyyy-MM-dd",
                "yyyy-MM-dd HH:mm",
                "yyyy-MM-dd HH:mm:ss",
                "dd-MMM-yyyy",
                "dd-MMM-yyyy HH:mm",
                "d-MMM-yyyy",
                "dd-MM-yyyy",
                "d-MM-yyyy",
                "MM/dd/yyyy",
                "yyyy/MM/dd",
                "dd/MM/yyyy"
            };

            if (DateTime.TryParseExact(dateOnly, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedExact))
                return parsedExact.Date;

            if (DateTime.TryParse(clean, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedInv))
                return parsedInv.Date;

            if (DateTime.TryParse(clean, out var parsedDefault))
                return parsedDefault.Date;

            return null;
        }

        private (DateTime? FromDate, DateTime? ToDate) ResolveDurationDates(string? duration, DateTime? fromDate, DateTime? toDate)
        {
            // 1. Resolve reference 'now' (if DB has future/specific dates like 2026, align with max bill date)
            var now = DateTime.Today;
            try
            {
                var maxBillDate = _context.BillMasters.Max(b => (DateTime?)b.BillDate);
                if (maxBillDate.HasValue && maxBillDate.Value.Date > now)
                {
                    now = maxBillDate.Value.Date;
                }
            }
            catch
            {
                // fallback to system today
            }

            // 2. Custom Date mode or direct manual date range query
            if (string.Equals(duration, "custom_date", StringComparison.OrdinalIgnoreCase) ||
                (string.IsNullOrWhiteSpace(duration) && fromDate.HasValue && toDate.HasValue))
            {
                var f = fromDate ?? now.AddDays(-7);
                var t = toDate ?? now;
                if (f > t)
                {
                    (f, t) = (t, f);
                }
                return (f.Date, t.Date);
            }

            // 3. Predefined duration options
            return (duration?.ToLowerInvariant()) switch
            {
                "today" => (now, now),
                "past_three_days" => (now.AddDays(-3), now),
                "past_week" => (now.AddDays(-7), now),
                "14_days" => (now.AddDays(-14), now),
                "month" => (new DateTime(now.Year, now.Month, 1), now),
                _ => (now, now)
            };
        }

        private async Task<(List<SpReceivableItem> Receivables, List<SpPayableItem> Payables, List<SpAdvanceItem> Advances, List<SpCommissionItem> Commissions, List<SpWeeklyTrendItem> WeeklyTrends, List<SpMonthlyTrendItem> MonthlyTrends, List<BillSummaryDto> RecentBills, int PeriodBillsCount, decimal PeriodSaleAmount, decimal PeriodCommissionAmount, List<DashboardBankBalanceDto> BankBalances, List<SpMarteyItem> MarteyItems, List<SpLoanItem> LoanItems, List<SpOtherItem> OtherItems)> LoadPayablesAndReceivablesSummaryAsync(
            int companyId,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var receivables = new List<SpReceivableItem>();
            var payables = new List<SpPayableItem>();
            var advances = new List<SpAdvanceItem>();
            var commissions = new List<SpCommissionItem>();
            var weeklyTrends = new List<SpWeeklyTrendItem>();
            var monthlyTrends = new List<SpMonthlyTrendItem>();
            var recentBills = new List<BillSummaryDto>();
            var bankBalances = new List<DashboardBankBalanceDto>();
            var marteyItems = new List<SpMarteyItem>();
            var loanItems = new List<SpLoanItem>();
            var otherItems = new List<SpOtherItem>();
            int periodBillsCount = 0;
            decimal periodSaleAmount = 0;
            decimal periodCommissionAmount = 0;

            try
            {
                try
                {
                    var spFile = Path.Combine(Directory.GetCurrentDirectory(), "SQL_Scripts", "receandpay.sql");
                    if (System.IO.File.Exists(spFile))
                    {
                        var lastWrite = System.IO.File.GetLastWriteTimeUtc(spFile);
                        if (lastWrite > _lastSpDeployTime)
                        {
                            _lastSpDeployTime = lastWrite;
                            var spSql = await System.IO.File.ReadAllTextAsync(spFile);
                            if (!string.IsNullOrWhiteSpace(spSql))
                            {
                                await _context.Database.ExecuteSqlRawAsync(spSql);
                                _logger.LogInformation("sp_GetDashboardPayablesAndReceivablesSummary auto-deployed successfully from receandpay.sql.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not auto-deploy sp_GetDashboardPayablesAndReceivablesSummary: {Message}", ex.Message);
                }

                var conn = _context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                {
                    await conn.OpenAsync();
                }

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_GetDashboardPayablesAndReceivablesSummary";
                cmd.CommandType = CommandType.StoredProcedure;

                var pCompany = cmd.CreateParameter();
                pCompany.ParameterName = "@CompanyId";
                pCompany.Value = companyId;
                cmd.Parameters.Add(pCompany);

                var pFrom = cmd.CreateParameter();
                pFrom.ParameterName = "@FromDate";
                pFrom.Value = fromDate.HasValue ? (object)fromDate.Value.Date : DBNull.Value;
                cmd.Parameters.Add(pFrom);

                var pTo = cmd.CreateParameter();
                pTo.ParameterName = "@ToDate";
                pTo.Value = toDate.HasValue ? (object)toDate.Value.Date : DBNull.Value;
                cmd.Parameters.Add(pTo);

                await using var reader = await cmd.ExecuteReaderAsync();

                // 1. Result Set 1: Customer Receivables
                while (await reader.ReadAsync())
                {
                    receivables.Add(new SpReceivableItem
                    {
                        CustomerId = Convert.ToInt32(reader["CustomerId"]),
                        CustomerName = reader["CustomerName"]?.ToString() ?? "",
                        AccountCode = !reader.IsDBNull(reader.GetOrdinal("AccountCode")) ? reader["AccountCode"]?.ToString() : null,
                        City = !reader.IsDBNull(reader.GetOrdinal("City")) ? reader["City"]?.ToString() ?? "-" : "-",
                        Phone = !reader.IsDBNull(reader.GetOrdinal("Phone")) ? reader["Phone"]?.ToString() ?? "-" : "-",
                        OpeningBalance = !reader.IsDBNull(reader.GetOrdinal("OpeningBalance")) ? Convert.ToDecimal(reader["OpeningBalance"]) : 0,
                        TotalInvoiced = !reader.IsDBNull(reader.GetOrdinal("TotalInvoiced")) ? Convert.ToDecimal(reader["TotalInvoiced"]) : 0,
                        TotalPaid = !reader.IsDBNull(reader.GetOrdinal("TotalPaid")) ? Convert.ToDecimal(reader["TotalPaid"]) : 0,
                        ReceivableBalance = !reader.IsDBNull(reader.GetOrdinal("ReceivableBalance")) ? Convert.ToDecimal(reader["ReceivableBalance"]) : 0,
                        Status = !reader.IsDBNull(reader.GetOrdinal("Status")) ? reader["Status"]?.ToString() ?? "Clear" : "Clear",
                        LastTxDate = !reader.IsDBNull(reader.GetOrdinal("LastTxDate")) ? Convert.ToDateTime(reader["LastTxDate"]) : null
                    });
                }

                // 2. Result Set 2: Supplier Payables
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        payables.Add(new SpPayableItem
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
                            Status = !reader.IsDBNull(reader.GetOrdinal("Status")) ? reader["Status"]?.ToString() ?? "Clear" : "Clear",
                            LastTxDate = !reader.IsDBNull(reader.GetOrdinal("LastTxDate")) ? Convert.ToDateTime(reader["LastTxDate"]) : null
                        });
                    }
                }

                // 3. Result Set 3: Customer Advances
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        advances.Add(new SpAdvanceItem
                        {
                            CustomerId = Convert.ToInt32(reader["CustomerId"]),
                            CustomerName = reader["CustomerName"]?.ToString() ?? "",
                            AccountCode = !reader.IsDBNull(reader.GetOrdinal("AccountCode")) ? reader["AccountCode"]?.ToString() : null,
                            City = !reader.IsDBNull(reader.GetOrdinal("City")) ? reader["City"]?.ToString() ?? "-" : "-",
                            Phone = !reader.IsDBNull(reader.GetOrdinal("Phone")) ? reader["Phone"]?.ToString() ?? "-" : "-",
                            AdvanceAmount = !reader.IsDBNull(reader.GetOrdinal("AdvanceAmount")) ? Convert.ToDecimal(reader["AdvanceAmount"]) : 0,
                            VoucherCount = !reader.IsDBNull(reader.GetOrdinal("VoucherCount")) ? Convert.ToInt32(reader["VoucherCount"]) : 0,
                            LastTxDate = !reader.IsDBNull(reader.GetOrdinal("LastTxDate")) ? Convert.ToDateTime(reader["LastTxDate"]) : null,
                            LatestVoucherNo = !reader.IsDBNull(reader.GetOrdinal("LatestVoucherNo")) ? reader["LatestVoucherNo"]?.ToString() : null
                        });
                    }
                }

                // 4. Result Set 4: Commissions
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        commissions.Add(new SpCommissionItem
                        {
                            AccountTransactionId = Convert.ToInt64(reader["AccountTransactionId"]),
                            VoucherNo = reader["VoucherNo"]?.ToString() ?? "",
                            InvoiceNo = reader["InvoiceNo"]?.ToString() ?? "",
                            Narration = reader["Narration"]?.ToString() ?? "",
                            ChartofAccountName = reader["ChartofAccountName"]?.ToString() ?? "",
                            CommissionAmount = !reader.IsDBNull(reader.GetOrdinal("CommissionAmount")) ? Convert.ToDecimal(reader["CommissionAmount"]) : 0,
                            ExtraDate = !reader.IsDBNull(reader.GetOrdinal("ExtraDate")) ? Convert.ToDateTime(reader["ExtraDate"]) : null,
                            IsInPeriod = !reader.IsDBNull(reader.GetOrdinal("IsInPeriod")) ? Convert.ToInt32(reader["IsInPeriod"]) : 0
                        });
                    }
                }

                // 5. Result Set 5: Weekly Trend
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        weeklyTrends.Add(new SpWeeklyTrendItem
                        {
                            DayDate = Convert.ToDateTime(reader["DayDate"]),
                            DayName = reader["DayName"]?.ToString() ?? "",
                            DayCommission = !reader.IsDBNull(reader.GetOrdinal("DayCommission")) ? Convert.ToDecimal(reader["DayCommission"]) : 0,
                            IsToday = !reader.IsDBNull(reader.GetOrdinal("IsToday")) && Convert.ToBoolean(reader["IsToday"])
                        });
                    }
                }

                // 6. Result Set 6: Monthly Trend
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        monthlyTrends.Add(new SpMonthlyTrendItem
                        {
                            BucketSeq = Convert.ToInt32(reader["BucketSeq"]),
                            BucketLabel = reader["BucketLabel"]?.ToString() ?? "",
                            BucketCommission = !reader.IsDBNull(reader.GetOrdinal("BucketCommission")) ? Convert.ToDecimal(reader["BucketCommission"]) : 0,
                            IsCurrent = !reader.IsDBNull(reader.GetOrdinal("IsCurrent")) && Convert.ToBoolean(reader["IsCurrent"])
                        });
                    }
                }

                // 7. Result Set 7: Recent Bills
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var billDate = !reader.IsDBNull(reader.GetOrdinal("BillDate")) ? Convert.ToDateTime(reader["BillDate"]) : DateTime.Today;
                        if (fromDate.HasValue && billDate.Date < fromDate.Value.Date) continue;
                        if (toDate.HasValue && billDate.Date > toDate.Value.Date) continue;

                        recentBills.Add(new BillSummaryDto
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            BillNo = reader["BillNo"]?.ToString() ?? "",
                            Date = billDate.ToString("dd-MMM-yyyy"),
                            SupplierName = reader["SupplierName"]?.ToString() ?? "Walk-in Supplier",
                            LotsCount = !reader.IsDBNull(reader.GetOrdinal("LotsCount")) ? Convert.ToInt32(reader["LotsCount"]) : 0,
                            TotalSale = !reader.IsDBNull(reader.GetOrdinal("TotalSale")) ? Convert.ToDecimal(reader["TotalSale"]) : 0,
                            Commission = !reader.IsDBNull(reader.GetOrdinal("Commission")) ? Convert.ToDecimal(reader["Commission"]) : 0,
                            NetPayable = !reader.IsDBNull(reader.GetOrdinal("NetPayable")) ? Convert.ToDecimal(reader["NetPayable"]) : 0,
                            Status = reader["Status"]?.ToString() ?? "Due"
                        });
                    }
                }

                // 8. Result Set 8: Bill KPIs in Selected Duration
                if (await reader.NextResultAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        periodBillsCount = !reader.IsDBNull(reader.GetOrdinal("PeriodBillsCount")) ? Convert.ToInt32(reader["PeriodBillsCount"]) : 0;
                        periodSaleAmount = !reader.IsDBNull(reader.GetOrdinal("PeriodSaleAmount")) ? Convert.ToDecimal(reader["PeriodSaleAmount"]) : 0;
                        periodCommissionAmount = !reader.IsDBNull(reader.GetOrdinal("PeriodCommissionAmount")) ? Convert.ToDecimal(reader["PeriodCommissionAmount"]) : 0;
                    }
                }

                // 9. Result Set 9: Bank & Cash Accounts Liquidity
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        bankBalances.Add(new DashboardBankBalanceDto
                        {
                            BankId = Convert.ToInt32(reader["BankId"]),
                            BankName = reader["BankName"]?.ToString() ?? "",
                            AccountName = reader["AccountName"]?.ToString() ?? "",
                            AccountNo = reader["AccountNo"]?.ToString() ?? "-",
                            IbanNo = reader["IbanNo"]?.ToString() ?? "-",
                            BranchOrLocation = reader["BranchOrLocation"]?.ToString() ?? "",
                            IsCash = !reader.IsDBNull(reader.GetOrdinal("IsCash")) && Convert.ToBoolean(reader["IsCash"]),
                            CurrentBalance = !reader.IsDBNull(reader.GetOrdinal("CurrentBalance")) ? Convert.ToDecimal(reader["CurrentBalance"]) : 0,
                            InflowAmount = !reader.IsDBNull(reader.GetOrdinal("InflowAmount")) ? Convert.ToDecimal(reader["InflowAmount"]) : 0,
                            OutflowAmount = !reader.IsDBNull(reader.GetOrdinal("OutflowAmount")) ? Convert.ToDecimal(reader["OutflowAmount"]) : 0,
                            ChartOfAccountId = !reader.IsDBNull(reader.GetOrdinal("ChartOfAccountId")) ? Convert.ToInt32(reader["ChartOfAccountId"]) : null,
                            Status = reader["Status"]?.ToString() ?? "Active"
                        });
                    }
                }

                // 10. Result Set 10: Martey Accounts (Debit/Credit Net Balance)
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        marteyItems.Add(new SpMarteyItem
                        {
                            ChartofAccountId = Convert.ToInt32(reader["ChartofAccountId"]),
                            AccountTitle = reader["AccountTitle"]?.ToString() ?? "",
                            AccountCode = !reader.IsDBNull(reader.GetOrdinal("AccountCode")) ? reader["AccountCode"]?.ToString() : null,
                            Location = !reader.IsDBNull(reader.GetOrdinal("Location")) ? reader["Location"]?.ToString() ?? "-" : "-",
                            TotalDebit = !reader.IsDBNull(reader.GetOrdinal("TotalDebit")) ? Convert.ToDecimal(reader["TotalDebit"]) : 0,
                            TotalCredit = !reader.IsDBNull(reader.GetOrdinal("TotalCredit")) ? Convert.ToDecimal(reader["TotalCredit"]) : 0,
                            NetBalance = !reader.IsDBNull(reader.GetOrdinal("NetBalance")) ? Convert.ToDecimal(reader["NetBalance"]) : 0,
                            MarteyBalance = !reader.IsDBNull(reader.GetOrdinal("MarteyBalance")) ? Convert.ToDecimal(reader["MarteyBalance"]) : 0,
                            BalanceType = reader["BalanceType"]?.ToString() ?? "Net Credit",
                            VoucherCount = !reader.IsDBNull(reader.GetOrdinal("VoucherCount")) ? Convert.ToInt32(reader["VoucherCount"]) : 0,
                            LastTxDate = !reader.IsDBNull(reader.GetOrdinal("LastTxDate")) ? Convert.ToDateTime(reader["LastTxDate"]) : null,
                            LatestVoucherNo = !reader.IsDBNull(reader.GetOrdinal("LatestVoucherNo")) ? reader["LatestVoucherNo"]?.ToString() : null,
                            IsInPeriod = !reader.IsDBNull(reader.GetOrdinal("IsInPeriod")) ? Convert.ToInt32(reader["IsInPeriod"]) : 0
                        });
                    }
                }

                // 11. Result Set 11: Top Loans (Debit/Credit Net Balance)
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        loanItems.Add(new SpLoanItem
                        {
                            ChartofAccountId = Convert.ToInt32(reader["ChartofAccountId"]),
                            AccountTitle = reader["AccountTitle"]?.ToString() ?? "",
                            AccountCode = !reader.IsDBNull(reader.GetOrdinal("AccountCode")) ? reader["AccountCode"]?.ToString() : null,
                            Location = !reader.IsDBNull(reader.GetOrdinal("Location")) ? reader["Location"]?.ToString() ?? "-" : "-",
                            TotalDebit = !reader.IsDBNull(reader.GetOrdinal("TotalDebit")) ? Convert.ToDecimal(reader["TotalDebit"]) : 0,
                            TotalCredit = !reader.IsDBNull(reader.GetOrdinal("TotalCredit")) ? Convert.ToDecimal(reader["TotalCredit"]) : 0,
                            NetBalance = !reader.IsDBNull(reader.GetOrdinal("NetBalance")) ? Convert.ToDecimal(reader["NetBalance"]) : 0,
                            LoanBalance = !reader.IsDBNull(reader.GetOrdinal("LoanBalance")) ? Convert.ToDecimal(reader["LoanBalance"]) : 0,
                            BalanceType = reader["BalanceType"]?.ToString() ?? "Net Debit",
                            VoucherCount = !reader.IsDBNull(reader.GetOrdinal("VoucherCount")) ? Convert.ToInt32(reader["VoucherCount"]) : 0,
                            LastTxDate = !reader.IsDBNull(reader.GetOrdinal("LastTxDate")) ? Convert.ToDateTime(reader["LastTxDate"]) : null,
                            LatestVoucherNo = !reader.IsDBNull(reader.GetOrdinal("LatestVoucherNo")) ? reader["LatestVoucherNo"]?.ToString() : null,
                            IsInPeriod = !reader.IsDBNull(reader.GetOrdinal("IsInPeriod")) ? Convert.ToInt32(reader["IsInPeriod"]) : 0
                        });
                    }
                }

                // 12. Result Set 12: Top Other (Debit/Credit Net Balance)
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        otherItems.Add(new SpOtherItem
                        {
                            ChartofAccountId = Convert.ToInt32(reader["ChartofAccountId"]),
                            AccountTitle = reader["AccountTitle"]?.ToString() ?? "",
                            AccountCode = !reader.IsDBNull(reader.GetOrdinal("AccountCode")) ? reader["AccountCode"]?.ToString() : null,
                            Location = !reader.IsDBNull(reader.GetOrdinal("Location")) ? reader["Location"]?.ToString() ?? "-" : "-",
                            TotalDebit = !reader.IsDBNull(reader.GetOrdinal("TotalDebit")) ? Convert.ToDecimal(reader["TotalDebit"]) : 0,
                            TotalCredit = !reader.IsDBNull(reader.GetOrdinal("TotalCredit")) ? Convert.ToDecimal(reader["TotalCredit"]) : 0,
                            NetBalance = !reader.IsDBNull(reader.GetOrdinal("NetBalance")) ? Convert.ToDecimal(reader["NetBalance"]) : 0,
                            OtherBalance = !reader.IsDBNull(reader.GetOrdinal("OtherBalance")) ? Convert.ToDecimal(reader["OtherBalance"]) : 0,
                            BalanceType = reader["BalanceType"]?.ToString() ?? "Net Debit",
                            VoucherCount = !reader.IsDBNull(reader.GetOrdinal("VoucherCount")) ? Convert.ToInt32(reader["VoucherCount"]) : 0,
                            LastTxDate = !reader.IsDBNull(reader.GetOrdinal("LastTxDate")) ? Convert.ToDateTime(reader["LastTxDate"]) : null,
                            LatestVoucherNo = !reader.IsDBNull(reader.GetOrdinal("LatestVoucherNo")) ? reader["LatestVoucherNo"]?.ToString() : null,
                            IsInPeriod = !reader.IsDBNull(reader.GetOrdinal("IsInPeriod")) ? Convert.ToInt32(reader["IsInPeriod"]) : 0
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error executing dbo.sp_GetDashboardPayablesAndReceivablesSummary: {Message}", ex.Message);
            }

            return (receivables, payables, advances, commissions, weeklyTrends, monthlyTrends, recentBills, periodBillsCount, periodSaleAmount, periodCommissionAmount, bankBalances, marteyItems, loanItems, otherItems);
        }

        private void PopulateFinancialSummaries(
            AarhatDashboardViewModel viewModel,
            List<SpReceivableItem> receivables,
            List<SpPayableItem> payables,
            List<SpAdvanceItem> advances,
            List<SpCommissionItem> commissions,
            List<SpWeeklyTrendItem> weeklyTrends,
            List<SpMonthlyTrendItem> monthlyTrends,
            List<BillSummaryDto> recentBills,
            int periodBillsCount,
            decimal periodSaleAmount,
            decimal periodCommissionAmount,
            List<DashboardBankBalanceDto>? bankBalances = null,
            List<SpMarteyItem>? marteyItems = null,
            List<SpLoanItem>? loanItems = null,
            List<SpOtherItem>? otherItems = null)
        {
            viewModel.TodayBillsCount = periodBillsCount;
            viewModel.TodaySaleAmount = periodSaleAmount;
            viewModel.RecentBills = recentBills ?? new();
            viewModel.CommissionEarned = periodCommissionAmount;
            viewModel.BankBalances = bankBalances ?? new();

            // Receivables
            var dueReceivables = (receivables ?? new()).Where(r => r.ReceivableBalance > 0).ToList();
            viewModel.TotalReceivable = dueReceivables.Sum(r => r.ReceivableBalance);
            viewModel.ActiveCustomersCount = dueReceivables.Count;
            viewModel.TopReceivablesCount = dueReceivables.Count;
            viewModel.TopReceivablesTotal = viewModel.TotalReceivable;
            viewModel.TopReceivables = dueReceivables
                .OrderByDescending(r => r.ReceivableBalance)
                .Take(5)
                .Select(r => new DashboardSummaryItemDto
                {
                    Title = r.CustomerName,
                    Subtitle = !string.IsNullOrWhiteSpace(r.City) && r.City != "-" ? r.City : "Customer",
                    Amount = r.ReceivableBalance
                }).ToList();

            viewModel.RecentReceivables = (receivables ?? new())
                .Where(r => r.ReceivableBalance != 0 || r.TotalInvoiced > 0 || r.TotalPaid > 0)
                .OrderByDescending(r => r.LastTxDate ?? DateTime.MinValue)
                .ThenByDescending(r => r.CustomerId)
                .Take(5)
                .Select(r => new DashboardSummaryItemDto
                {
                    Title = r.CustomerName,
                    Subtitle = r.LastTxDate.HasValue
                        ? $"{r.LastTxDate.Value:dd-MMM hh:mm tt}"
                        : (!string.IsNullOrWhiteSpace(r.City) && r.City != "-" ? r.City : "Customer"),
                    Amount = r.ReceivableBalance
                }).ToList();

            // Payables
            var duePayables = (payables ?? new()).Where(p => p.PayableBalance > 0).ToList();
            viewModel.TotalPayable = duePayables.Sum(p => p.PayableBalance);
            viewModel.ActiveSuppliersCount = duePayables.Count;
            viewModel.TopPayablesCount = duePayables.Count;
            viewModel.TopPayablesTotal = viewModel.TotalPayable;
            viewModel.TopPayables = duePayables
                .OrderByDescending(p => p.PayableBalance)
                .Take(5)
                .Select(p => new TopPayableDto
                {
                    SupplierName = p.SupplierName,
                    Location = !string.IsNullOrWhiteSpace(p.VillageOrJetty) && p.VillageOrJetty != "-" ? p.VillageOrJetty : "Supplier",
                    Amount = p.PayableBalance
                }).ToList();

            viewModel.RecentPayables = (payables ?? new())
                .Where(p => p.PayableBalance != 0 || p.TotalSold > 0 || p.TotalPaid > 0)
                .OrderByDescending(p => p.LastTxDate ?? DateTime.MinValue)
                .ThenByDescending(p => p.SupplierId)
                .Take(5)
                .Select(p => new TopPayableDto
                {
                    SupplierName = p.SupplierName,
                    Location = p.LastTxDate.HasValue
                        ? $"{p.LastTxDate.Value:dd-MMM hh:mm tt}"
                        : (!string.IsNullOrWhiteSpace(p.VillageOrJetty) && p.VillageOrJetty != "-" ? p.VillageOrJetty : "Supplier"),
                    Amount = p.PayableBalance
                }).ToList();

            // Advances
            var validAdvances = (advances ?? new()).Where(a => a.AdvanceAmount > 0).ToList();
            viewModel.TopAdvancesTotal = validAdvances.Sum(a => a.AdvanceAmount);
            viewModel.TopAdvancesCount = validAdvances.Count;
            viewModel.TopAdvances = validAdvances
                .OrderByDescending(a => a.AdvanceAmount)
                .Take(5)
                .Select(a => new DashboardSummaryItemDto
                {
                    Title = a.CustomerName,
                    Subtitle = !string.IsNullOrWhiteSpace(a.LatestVoucherNo)
                        ? a.LatestVoucherNo
                        : (!string.IsNullOrWhiteSpace(a.City) && a.City != "-" ? a.City : "Customer Advance"),
                    Amount = a.AdvanceAmount
                }).ToList();

            viewModel.RecentAdvances = validAdvances
                .OrderByDescending(a => a.LastTxDate ?? DateTime.MinValue)
                .ThenByDescending(a => a.CustomerId)
                .Take(5)
                .Select(a => new DashboardSummaryItemDto
                {
                    Title = a.CustomerName,
                    Subtitle = a.LastTxDate.HasValue
                        ? $"{a.LastTxDate.Value:dd-MMM hh:mm tt}"
                        : (!string.IsNullOrWhiteSpace(a.LatestVoucherNo) ? a.LatestVoucherNo : "Customer Advance"),
                    Amount = a.AdvanceAmount
                }).ToList();

            // Commissions
            var periodItems = (commissions ?? new()).Where(c => c.IsInPeriod == 1).ToList();
            var allRecentItems = (commissions ?? new())
                .GroupBy(c => c.AccountTransactionId)
                .Select(g => g.First())
                .OrderByDescending(c => c.ExtraDate ?? DateTime.MinValue)
                .ThenByDescending(c => c.AccountTransactionId)
                .ToList();

            viewModel.TopCommissionsTotal = periodItems.Sum(c => c.CommissionAmount);
            viewModel.TopCommissionsCount = periodItems.Count;
            if (periodCommissionAmount == 0 && viewModel.TopCommissionsTotal > 0)
            {
                viewModel.CommissionEarned = viewModel.TopCommissionsTotal;
            }

            var topCommSource = periodItems.Any() ? periodItems : allRecentItems;
            viewModel.TopCommissions = topCommSource
                .OrderByDescending(c => c.CommissionAmount)
                .ThenByDescending(c => c.ExtraDate ?? DateTime.MinValue)
                .Take(5)
                .Select(c => new DashboardSummaryItemDto
                {
                    Title = !string.IsNullOrWhiteSpace(c.InvoiceNo) ? (c.InvoiceNo.StartsWith("BL-", StringComparison.OrdinalIgnoreCase) ? $"Bill # {c.InvoiceNo}" : c.InvoiceNo) : c.VoucherNo,
                    Subtitle = !string.IsNullOrWhiteSpace(c.Narration) ? c.Narration : c.ChartofAccountName,
                    Amount = c.CommissionAmount
                }).ToList();

            viewModel.RecentCommissions = allRecentItems
                .Take(5)
                .Select(c => new DashboardSummaryItemDto
                {
                    Title = !string.IsNullOrWhiteSpace(c.InvoiceNo) ? (c.InvoiceNo.StartsWith("BL-", StringComparison.OrdinalIgnoreCase) ? $"Bill # {c.InvoiceNo}" : c.InvoiceNo) : c.VoucherNo,
                    Subtitle = c.ExtraDate.HasValue
                        ? $"{c.ExtraDate.Value:dd-MMM} · {(!string.IsNullOrWhiteSpace(c.Narration) ? c.Narration : c.ChartofAccountName)}"
                        : (!string.IsNullOrWhiteSpace(c.Narration) ? c.Narration : c.ChartofAccountName),
                    Amount = c.CommissionAmount
                }).ToList();

            // Weekly & Monthly Trends
            if (weeklyTrends != null && weeklyTrends.Any())
            {
                decimal maxWeek = weeklyTrends.Max(w => w.DayCommission);
                viewModel.WeeklyTrends = weeklyTrends.Select(w => new WeeklyTrendDto
                {
                    Day = w.DayName,
                    Amount = w.DayCommission,
                    Percent = maxWeek > 0
                        ? Math.Max(w.DayCommission > 0 ? 15 : 6, (int)Math.Round((w.DayCommission / maxWeek) * 90m))
                        : 6,
                    IsToday = w.IsToday
                }).ToList();
            }
            else
            {
                viewModel.WeeklyTrends = new List<WeeklyTrendDto>();
            }

            if (monthlyTrends != null && monthlyTrends.Any())
            {
                decimal maxMonth = monthlyTrends.Max(m => m.BucketCommission);
                viewModel.MonthlyTrends = monthlyTrends.Select(m => new WeeklyTrendDto
                {
                    Day = m.BucketLabel,
                    Amount = m.BucketCommission,
                    Percent = maxMonth > 0
                        ? Math.Max(m.BucketCommission > 0 ? 15 : 6, (int)Math.Round((m.BucketCommission / maxMonth) * 90m))
                        : 6,
                    IsToday = m.IsCurrent
                }).ToList();
            }
            else
            {
                viewModel.MonthlyTrends = new List<WeeklyTrendDto>();
            }

            // Martey Accounts
            var validMartey = (marteyItems ?? new()).Where(m => m.MarteyBalance > 0).ToList();
            var periodMartey = validMartey.Where(m => m.IsInPeriod == 1).ToList();
            var activeMarteyList = periodMartey.Any() ? periodMartey : validMartey;

            viewModel.TopMarteyCount = activeMarteyList.Count;
            viewModel.TopMarteyTotal = activeMarteyList.Sum(m => m.MarteyBalance);

            viewModel.TopMartey = activeMarteyList
                .OrderByDescending(m => m.MarteyBalance)
                .Take(5)
                .Select(m => new DashboardSummaryItemDto
                {
                    Title = m.AccountTitle,
                    Subtitle = !string.IsNullOrWhiteSpace(m.Location) && m.Location != "-"
                        ? $"{m.Location} · Martey Due"
                        : (!string.IsNullOrWhiteSpace(m.LatestVoucherNo)
                            ? $"Voucher: {m.LatestVoucherNo}"
                            : (m.TotalCredit > 0 && m.TotalDebit > 0
                                ? $"Cr: Rs {m.TotalCredit:N0} · Dr: Rs {m.TotalDebit:N0}"
                                : (m.TotalCredit > 0 ? $"Deposit: Rs {m.TotalCredit:N0}" : $"Advance: Rs {m.TotalDebit:N0}"))),
                    Amount = m.MarteyBalance
                }).ToList();

            viewModel.RecentMartey = validMartey
                .OrderByDescending(m => m.LastTxDate ?? DateTime.MinValue)
                .ThenByDescending(m => m.ChartofAccountId)
                .Take(5)
                .Select(m => new DashboardSummaryItemDto
                {
                    Title = m.AccountTitle,
                    Subtitle = m.LastTxDate.HasValue
                        ? $"{m.LastTxDate.Value:dd-MMM} · {(!string.IsNullOrWhiteSpace(m.Location) && m.Location != "-" ? m.Location : (!string.IsNullOrWhiteSpace(m.LatestVoucherNo) ? m.LatestVoucherNo : "Martey Due"))}"
                        : (!string.IsNullOrWhiteSpace(m.Location) && m.Location != "-" ? m.Location : (!string.IsNullOrWhiteSpace(m.LatestVoucherNo) ? m.LatestVoucherNo : "Martey Due")),
                    Amount = m.MarteyBalance
                }).ToList();

            // Loan Accounts
            var validLoans = (loanItems ?? new()).Where(l => l.LoanBalance > 0).ToList();
            var periodLoans = validLoans.Where(l => l.IsInPeriod == 1).ToList();
            var activeLoansList = periodLoans.Any() ? periodLoans : validLoans;

            viewModel.TopLoansCount = activeLoansList.Count;
            viewModel.TopLoansTotal = activeLoansList.Sum(l => l.LoanBalance);

            viewModel.TopLoans = activeLoansList
                .OrderByDescending(l => l.LoanBalance)
                .Take(5)
                .Select(l => new DashboardSummaryItemDto
                {
                    Title = l.AccountTitle,
                    Subtitle = !string.IsNullOrWhiteSpace(l.Location) && l.Location != "-"
                        ? $"{l.Location} · Loan Outstanding"
                        : (!string.IsNullOrWhiteSpace(l.LatestVoucherNo)
                            ? $"Voucher: {l.LatestVoucherNo}"
                            : (l.TotalCredit > 0 && l.TotalDebit > 0
                                ? $"Dr: Rs {l.TotalDebit:N0} · Cr: Rs {l.TotalCredit:N0}"
                                : (l.TotalDebit > 0 ? $"Loan Given: Rs {l.TotalDebit:N0}" : $"Repaid: Rs {l.TotalCredit:N0}"))),
                    Amount = l.LoanBalance
                }).ToList();

            viewModel.RecentLoans = validLoans
                .OrderByDescending(l => l.LastTxDate ?? DateTime.MinValue)
                .ThenByDescending(l => l.ChartofAccountId)
                .Take(5)
                .Select(l => new DashboardSummaryItemDto
                {
                    Title = l.AccountTitle,
                    Subtitle = l.LastTxDate.HasValue
                        ? $"{l.LastTxDate.Value:dd-MMM} · {(!string.IsNullOrWhiteSpace(l.Location) && l.Location != "-" ? l.Location : (!string.IsNullOrWhiteSpace(l.LatestVoucherNo) ? l.LatestVoucherNo : "Loan Tx"))}"
                        : (!string.IsNullOrWhiteSpace(l.Location) && l.Location != "-" ? l.Location : (!string.IsNullOrWhiteSpace(l.LatestVoucherNo) ? l.LatestVoucherNo : "Loan Tx")),
                    Amount = l.LoanBalance
                }).ToList();

            // Other Accounts
            var validOthers = (otherItems ?? new()).Where(o => o.OtherBalance > 0).ToList();
            var periodOthers = validOthers.Where(o => o.IsInPeriod == 1).ToList();
            var activeOthersList = periodOthers.Any() ? periodOthers : validOthers;

            viewModel.TopOthersCount = activeOthersList.Count;
            viewModel.TopOthersTotal = activeOthersList.Sum(o => o.OtherBalance);

            viewModel.TopOthers = activeOthersList
                .OrderByDescending(o => o.OtherBalance)
                .Take(5)
                .Select(o => new DashboardSummaryItemDto
                {
                    Title = o.AccountTitle,
                    Subtitle = !string.IsNullOrWhiteSpace(o.Location) && o.Location != "-"
                        ? $"{o.Location} · Other Dues"
                        : (!string.IsNullOrWhiteSpace(o.LatestVoucherNo)
                            ? $"Voucher: {o.LatestVoucherNo}"
                            : (o.TotalCredit > 0 && o.TotalDebit > 0
                                ? $"Dr: Rs {o.TotalDebit:N0} · Cr: Rs {o.TotalCredit:N0}"
                                : (o.TotalDebit > 0 ? $"Dr: Rs {o.TotalDebit:N0}" : $"Cr: Rs {o.TotalCredit:N0}"))),
                    Amount = o.OtherBalance
                }).ToList();

            viewModel.RecentOthers = validOthers
                .OrderByDescending(o => o.LastTxDate ?? DateTime.MinValue)
                .ThenByDescending(o => o.ChartofAccountId)
                .Take(5)
                .Select(o => new DashboardSummaryItemDto
                {
                    Title = o.AccountTitle,
                    Subtitle = o.LastTxDate.HasValue
                        ? $"{o.LastTxDate.Value:dd-MMM} · {(!string.IsNullOrWhiteSpace(o.Location) && o.Location != "-" ? o.Location : (!string.IsNullOrWhiteSpace(o.LatestVoucherNo) ? o.LatestVoucherNo : "Other Tx"))}"
                        : (!string.IsNullOrWhiteSpace(o.Location) && o.Location != "-" ? o.Location : (!string.IsNullOrWhiteSpace(o.LatestVoucherNo) ? o.LatestVoucherNo : "Other Tx")),
                    Amount = o.OtherBalance
                }).ToList();
        }

        private async Task PopulateMarteyExpensesAndStaffAsync(
            AarhatDashboardViewModel viewModel,
            int companyId,
            DateTime? fromDate,
            DateTime? toDate)
        {
            try
            {
                var fromDt = fromDate.HasValue ? fromDate.Value.Date : (DateTime?)null;
                var toDt = toDate.HasValue ? toDate.Value.Date : (DateTime?)null;

                bool IsInPeriod(DateTime dt) =>
                    (!fromDt.HasValue || dt.Date >= fromDt.Value) &&
                    (!toDt.HasValue || dt.Date <= toDt.Value);

                bool IsInPeriodNullable(DateTime? dt) =>
                    dt.HasValue && IsInPeriod(dt.Value);

                bool WasCreatedInPeriod(DateTime? createdOn, DateTime fallbackDate) =>
                    IsInPeriod(createdOn ?? fallbackDate);

                bool WasModifiedInPeriod(DateTime? modifiedOn) =>
                    IsInPeriodNullable(modifiedOn);

                // 1. Staff Members & Activity from BillMasters and Vouchers
                var allBills = await _context.BillMasters
                    .Include(b => b.Supplier)
                    .Include(b => b.Details)
                    .Where(b => (b.CompanyId == null || b.CompanyId == companyId) && (b.Active == null || b.Active == true))
                    .OrderByDescending(b => b.BillDate)
                    .ThenByDescending(b => b.Id)
                    .ToListAsync();

                // Synchronize Period KPI Summary with actual bills in this period
                var kpiBills = allBills.Where(b => IsInPeriod(b.BillDate)).ToList();
                viewModel.TodayBillsCount = kpiBills.Count;
                viewModel.TodaySaleAmount = kpiBills.Sum(b => (decimal?)b.TotalGrossAmount) ?? 0m;
                viewModel.CommissionEarned = kpiBills.Sum(b => (decimal?)b.CommissionAmount) ?? 0m;

                // Synchronize RecentBills table with bills matching the period
                viewModel.RecentBills = kpiBills.Take(15).Select(b => new BillSummaryDto
                {
                    Id = b.Id,
                    BillNo = b.BillNo ?? $"BL-{b.Id}",
                    Date = b.BillDate.ToString("dd-MMM-yyyy"),
                    SupplierName = b.Supplier?.Name ?? "Walk-in Supplier",
                    LotsCount = b.Details?.Count ?? 0,
                    TotalSale = b.TotalGrossAmount,
                    Commission = b.CommissionAmount,
                    NetPayable = b.NetPayable,
                    Status = b.Status ?? "Due"
                }).ToList();

                var allVouchers = await _context.Vouchers
                    .AsNoTracking()
                    .Include(v => v.Vendor)
                    .Include(v => v.AccountTransactions)
                    .Where(v => (v.CompanyId == null || v.CompanyId == companyId) && (v.DeleteStatus == null || v.DeleteStatus == 0))
                    .OrderByDescending(v => v.VoucherDate)
                    .ThenByDescending(v => v.VoucherId)
                    .ToListAsync();

                // Map bills for fast lookup
                var billIdMap = allBills.ToDictionary(b => b.Id, b => b);
                var billNoMap = allBills
                    .Where(b => !string.IsNullOrWhiteSpace(b.BillNo))
                    .GroupBy(b => b.BillNo.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var linkedVouchersByBillId = new Dictionary<int, List<StaffVoucherActivityDto>>();
                var manualVouchers = new List<Voucher>();

                foreach (var v in allVouchers)
                {
                    BillMaster? matchedBill = null;

                    if (v.InvoiceId.HasValue && billIdMap.TryGetValue(v.InvoiceId.Value, out var bById))
                    {
                        matchedBill = bById;
                    }
                    else if (!string.IsNullOrWhiteSpace(v.InvoiceNo) && billNoMap.TryGetValue(v.InvoiceNo.Trim(), out var bByNo))
                    {
                        matchedBill = bByNo;
                    }
                    else if (v.AccountTransactions != null && v.AccountTransactions.Any())
                    {
                        var txMatch = v.AccountTransactions.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.InvoiceNo) && billNoMap.ContainsKey(t.InvoiceNo.Trim()));
                        if (txMatch != null && billNoMap.TryGetValue(txMatch.InvoiceNo!.Trim(), out var bByTx))
                        {
                            matchedBill = bByTx;
                        }
                    }

                    if (matchedBill != null)
                    {
                        if (!linkedVouchersByBillId.ContainsKey(matchedBill.Id))
                        {
                            linkedVouchersByBillId[matchedBill.Id] = new List<StaffVoucherActivityDto>();
                        }

                        linkedVouchersByBillId[matchedBill.Id].Add(new StaffVoucherActivityDto
                        {
                            VoucherId = v.VoucherId,
                            VoucherNo = !string.IsNullOrWhiteSpace(v.VoucherNo) ? v.VoucherNo : $"VC-{v.VoucherId}",
                            VoucherType = FormatVoucherTypeName(v.VoucherType, v.VoucherNo),
                            Date = v.VoucherDate.ToString("dd-MMM-yyyy"),
                            PartyOrAccount = ResolveVoucherParty(v),
                            Amount = v.Amount,
                            Narration = v.Narration ?? string.Empty,
                            InvoiceNo = matchedBill.BillNo,
                            ActivityType = (!string.IsNullOrWhiteSpace(v.ModifiedBy) && v.ModifiedBy != v.CreatedBy) ? "Modified" : "Created",
                            Timestamp = (v.ModifiedOn ?? v.CreatedOn ?? v.VoucherDate).ToString("dd-MMM-yyyy hh:mm tt"),
                            Status = !string.IsNullOrWhiteSpace(v.ApprovalStatus) ? v.ApprovalStatus : "Approved"
                        });
                    }
                    else
                    {
                        // Standalone manual voucher created without any bill
                        manualVouchers.Add(v);
                    }
                }

                // Filter bills and manual vouchers that have activity in the selected period
                var periodBills = allBills.Where(b => IsInPeriod(b.BillDate) || IsInPeriodNullable(b.CreatedOn) || IsInPeriodNullable(b.ModifiedOn)).ToList();
                var periodManualVouchers = manualVouchers.Where(v => IsInPeriod(v.VoucherDate) || IsInPeriodNullable(v.CreatedOn) || IsInPeriodNullable(v.ModifiedOn)).ToList();

                var allStaffNames = periodBills.Select(b => b.CreatedBy?.Trim())
                    .Concat(periodBills.Select(b => b.ModifiedBy?.Trim()))
                    .Concat(periodManualVouchers.Select(v => v.CreatedBy?.Trim()))
                    .Concat(periodManualVouchers.Select(v => v.ModifiedBy?.Trim()))
                    .Concat(periodManualVouchers.Select(v => v.Username?.Trim()))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                int idCounter = 1;
                var staffSummaryList = new List<StaffDashboardSummaryDto>();

                foreach (var staffName in allStaffNames)
                {
                    var staffCreatedBills = periodBills
                        .Where(b => string.Equals(b.CreatedBy?.Trim(), staffName, StringComparison.OrdinalIgnoreCase) && WasCreatedInPeriod(b.CreatedOn, b.BillDate))
                        .ToList();

                    var staffModifiedBills = periodBills
                        .Where(b => string.Equals(b.ModifiedBy?.Trim(), staffName, StringComparison.OrdinalIgnoreCase) 
                                 && WasModifiedInPeriod(b.ModifiedOn)
                                 && (!string.Equals(b.CreatedBy?.Trim(), staffName, StringComparison.OrdinalIgnoreCase) || !WasCreatedInPeriod(b.CreatedOn, b.BillDate)))
                        .ToList();

                    var staffCreatedManualVouchers = periodManualVouchers
                        .Where(v => (string.Equals(v.CreatedBy?.Trim(), staffName, StringComparison.OrdinalIgnoreCase) || (string.IsNullOrWhiteSpace(v.CreatedBy) && string.Equals(v.Username?.Trim(), staffName, StringComparison.OrdinalIgnoreCase)))
                                 && WasCreatedInPeriod(v.CreatedOn, v.VoucherDate))
                        .ToList();

                    var staffModifiedManualVouchers = periodManualVouchers
                        .Where(v => string.Equals(v.ModifiedBy?.Trim(), staffName, StringComparison.OrdinalIgnoreCase)
                                 && WasModifiedInPeriod(v.ModifiedOn)
                                 && (!string.Equals(v.CreatedBy?.Trim(), staffName, StringComparison.OrdinalIgnoreCase) || !WasCreatedInPeriod(v.CreatedOn, v.VoucherDate)))
                        .ToList();

                    if (!staffCreatedBills.Any() && !staffModifiedBills.Any() && !staffCreatedManualVouchers.Any() && !staffModifiedManualVouchers.Any())
                        continue;

                    // Determine most recent activity timestamp across bills & manual vouchers
                    DateTime latestDt = DateTime.MinValue;
                    foreach (var b in staffCreatedBills)
                    {
                        var dt = b.CreatedOn ?? b.BillDate;
                        if (dt > latestDt) latestDt = dt;
                    }
                    foreach (var b in staffModifiedBills)
                    {
                        var dt = b.ModifiedOn ?? b.BillDate;
                        if (dt > latestDt) latestDt = dt;
                    }
                    foreach (var v in staffCreatedManualVouchers)
                    {
                        var dt = v.CreatedOn ?? v.VoucherDate;
                        if (dt > latestDt) latestDt = dt;
                    }
                    foreach (var v in staffModifiedManualVouchers)
                    {
                        var dt = v.ModifiedOn ?? v.VoucherDate;
                        if (dt > latestDt) latestDt = dt;
                    }

                    string lastActStr = latestDt > DateTime.MinValue ? latestDt.ToString("dd-MMM hh:mm tt") : "-";

                    var billsWorkedOnDto = staffCreatedBills.Concat(staffModifiedBills)
                        .OrderByDescending(b => b.CreatedOn ?? b.BillDate)
                        .Take(15)
                        .Select(b =>
                        {
                            var isCreated = string.Equals(b.CreatedBy?.Trim(), staffName, StringComparison.OrdinalIgnoreCase);
                            return new StaffBillActivityDto
                            {
                                BillId = b.Id,
                                BillNo = b.BillNo,
                                Date = b.BillDate.ToString("dd-MMM-yyyy"),
                                SupplierName = b.Supplier != null ? b.Supplier.Name : "Walk-in Supplier",
                                LotsCount = b.Details != null ? b.Details.Count : 0,
                                TotalSale = b.TotalGrossAmount,
                                Commission = b.CommissionAmount,
                                NetPayable = b.NetPayable,
                                ActivityType = isCreated ? "Created" : "Modified",
                                Timestamp = ((isCreated ? b.CreatedOn : b.ModifiedOn) ?? b.BillDate).ToString("dd-MMM-yyyy hh:mm tt"),
                                Status = b.Status ?? "Due",
                                LinkedVouchers = linkedVouchersByBillId.GetValueOrDefault(b.Id, new List<StaffVoucherActivityDto>())
                            };
                        }).ToList();

                    var manualVouchersDto = staffCreatedManualVouchers.Concat(staffModifiedManualVouchers)
                        .OrderByDescending(v => v.CreatedOn ?? v.VoucherDate)
                        .Take(25)
                        .Select(v =>
                        {
                            var isCreated = string.Equals(v.CreatedBy?.Trim(), staffName, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(v.CreatedBy);
                            return new StaffVoucherActivityDto
                            {
                                VoucherId = v.VoucherId,
                                VoucherNo = !string.IsNullOrWhiteSpace(v.VoucherNo) ? v.VoucherNo : $"VC-{v.VoucherId}",
                                VoucherType = FormatVoucherTypeName(v.VoucherType, v.VoucherNo),
                                Date = v.VoucherDate.ToString("dd-MMM-yyyy"),
                                PartyOrAccount = ResolveVoucherParty(v),
                                Amount = v.Amount,
                                Narration = v.Narration ?? string.Empty,
                                InvoiceNo = null,
                                ActivityType = isCreated ? "Created" : "Modified",
                                Timestamp = ((isCreated ? v.CreatedOn : v.ModifiedOn) ?? v.VoucherDate).ToString("dd-MMM-yyyy hh:mm tt"),
                                Status = !string.IsNullOrWhiteSpace(v.ApprovalStatus) ? v.ApprovalStatus : "Approved"
                            };
                        }).ToList();

                    staffSummaryList.Add(new StaffDashboardSummaryDto
                    {
                        StaffId = idCounter++,
                        StaffName = staffName,
                        Username = staffName.ToLowerInvariant().Replace(" ", "_"),
                        Role = staffName.Equals("admin", StringComparison.OrdinalIgnoreCase) || staffName.Equals("Zahid Hussain", StringComparison.OrdinalIgnoreCase) ? "Owner / Administrator" : "Accountant / Operator",
                        BillsCreatedCount = staffCreatedBills.Count,
                        BillsModifiedCount = staffModifiedBills.Count,
                        VouchersCreatedCount = staffCreatedManualVouchers.Count,
                        VouchersModifiedCount = staffModifiedManualVouchers.Count,
                        TotalSalesVolume = staffCreatedBills.Sum(b => b.TotalGrossAmount),
                        TotalCommission = staffCreatedBills.Sum(b => b.CommissionAmount),
                        TotalVouchersVolume = staffCreatedManualVouchers.Sum(v => v.Amount),
                        LastActivity = lastActStr,
                        BillsWorkedOn = billsWorkedOnDto,
                        ManualVouchers = manualVouchersDto
                    });
                }

                viewModel.StaffMembers = staffSummaryList.OrderByDescending(s => s.TotalHandled).ToList();

                // 1b. Real Advances from AccountTransaction (Fallback grouping by User Account if SP was empty)
                if (viewModel.TopAdvances == null || !viewModel.TopAdvances.Any())
                {
                    var bankCoaIds = await _context.BankMasters.Where(b => b.ChartOfAccountId != null).Select(b => b.ChartOfAccountId!.Value).ToListAsync();

                    var advTxList = await _context.AccountTransactions
                        .Where(at => (at.CompanyId == null || at.CompanyId == companyId)
                                  && (at.Type == "Advance" || (at.Narration != null && at.Narration.Contains("Advance")) || (at.EntryTypeName != null && at.EntryTypeName.Contains("Advance")))
                                  && (at.Credit > 0 || at.Debit > 0 || (at.Amount ?? 0) > 0)
                                  && (at.ChartofAccountId == null || !bankCoaIds.Contains(at.ChartofAccountId.Value))
                                  && (at.AccountName == null || !at.AccountName.Contains("Cash"))
                                  && (at.ChartofAccountName == null || !at.ChartofAccountName.Contains("Cash")))
                        .ToListAsync();

                    if (advTxList.Any())
                    {
                        var groupedAdv = advTxList
                            .GroupBy(at => !string.IsNullOrWhiteSpace(at.AccountName) ? at.AccountName.Trim() : (!string.IsNullOrWhiteSpace(at.ChartofAccountName) ? at.ChartofAccountName.Trim() : (at.ChartofAccountId?.ToString() ?? "Advance")), StringComparer.OrdinalIgnoreCase)
                            .Select(g =>
                            {
                                var first = g.First();
                                decimal totCredit = g.Sum(x => x.Credit);
                                decimal totDebit = g.Sum(x => x.Debit);
                                decimal netBal = Math.Abs(totDebit - totCredit);
                                string name = g.Key;
                                var lastTx = g.Max(x => x.ExtraDate);
                                return new
                                {
                                    CustomerId = first.ChartofAccountId ?? 0,
                                    Title = name,
                                    Subtitle = totCredit > 0 ? $"Dr: Rs {totDebit:N0} | Cr: Rs {totCredit:N0}" : $"Advance Given: Rs {totDebit:N0}",
                                    Amount = netBal,
                                    ExtraDate = lastTx
                                };
                            })
                            .Where(x => x.Amount > 0)
                            .ToList();

                        viewModel.TopAdvancesCount = groupedAdv.Count;
                        viewModel.TopAdvancesTotal = groupedAdv.Sum(a => a.Amount);

                        viewModel.TopAdvances = groupedAdv
                            .OrderByDescending(a => a.Amount)
                            .Take(5)
                            .Select(a => new DashboardSummaryItemDto
                            {
                                Title = a.Title,
                                Subtitle = a.Subtitle,
                                Amount = a.Amount
                            }).ToList();

                        viewModel.RecentAdvances = groupedAdv
                            .OrderByDescending(a => a.ExtraDate ?? DateTime.MinValue)
                            .Take(5)
                            .Select(a => new DashboardSummaryItemDto
                            {
                                Title = a.Title,
                                Subtitle = a.ExtraDate.HasValue ? $"{a.ExtraDate.Value:dd-MMM} · {a.Subtitle}" : a.Subtitle,
                                Amount = a.Amount
                            }).ToList();
                    }
                }

                // 2. Real Martey from AccountTransaction (Fallback grouping by User Account if SP was empty)
                if (viewModel.TopMartey == null || !viewModel.TopMartey.Any())
                {
                    var bankCoaIds = await _context.BankMasters.Where(b => b.ChartOfAccountId != null).Select(b => b.ChartOfAccountId!.Value).ToListAsync();

                    var marteyTxList = await _context.AccountTransactions
                        .Where(at => (at.CompanyId == null || at.CompanyId == companyId)
                                  && (at.Type == "Martey" ||
                                      (at.Narration != null && at.Narration.Contains("Martey") && !at.Narration.Contains("Martey: Rs 0")) ||
                                      (at.EntryTypeName != null && at.EntryTypeName.Contains("Martey")))
                                  && (at.Credit > 0 || at.Debit > 0 || (at.Amount ?? 0) > 0)
                                  && (at.ChartofAccountId == null || !bankCoaIds.Contains(at.ChartofAccountId.Value))
                                  && (at.AccountName == null || !at.AccountName.Contains("Cash"))
                                  && (at.ChartofAccountName == null || !at.ChartofAccountName.Contains("Cash")))
                        .ToListAsync();

                    if (marteyTxList.Any())
                    {
                        var groupedMartey = marteyTxList
                            .GroupBy(at => at.ChartofAccountId ?? 0)
                            .Select(g =>
                            {
                                var first = g.First();
                                decimal totCredit = g.Sum(x => x.Credit);
                                decimal totDebit = g.Sum(x => x.Debit);
                                decimal netBal = Math.Abs(totCredit - totDebit);
                                string name = !string.IsNullOrWhiteSpace(first.AccountName) ? first.AccountName : (!string.IsNullOrWhiteSpace(first.ChartofAccountName) ? first.ChartofAccountName : "Martey Account");
                                var lastTx = g.Max(x => x.ExtraDate);
                                return new
                                {
                                    ChartofAccountId = g.Key,
                                    Title = name,
                                    Subtitle = $"Cr: Rs {totCredit:N0} | Dr: Rs {totDebit:N0}",
                                    Amount = netBal,
                                    ExtraDate = lastTx
                                };
                            })
                            .Where(x => x.Amount > 0)
                            .ToList();

                        viewModel.TopMarteyCount = groupedMartey.Count;
                        viewModel.TopMarteyTotal = groupedMartey.Sum(m => m.Amount);

                        viewModel.TopMartey = groupedMartey
                            .OrderByDescending(m => m.Amount)
                            .Take(5)
                            .Select(m => new DashboardSummaryItemDto
                            {
                                Title = m.Title,
                                Subtitle = m.Subtitle,
                                Amount = m.Amount
                            }).ToList();

                        viewModel.RecentMartey = groupedMartey
                            .OrderByDescending(m => m.ExtraDate ?? DateTime.MinValue)
                            .Take(5)
                            .Select(m => new DashboardSummaryItemDto
                            {
                                Title = m.Title,
                                Subtitle = m.ExtraDate.HasValue ? $"{m.ExtraDate.Value:dd-MMM} · {m.Subtitle}" : m.Subtitle,
                                Amount = m.Amount
                            }).ToList();
                    }
                    else
                    {
                        viewModel.TopMartey = new List<DashboardSummaryItemDto>();
                        viewModel.RecentMartey = new List<DashboardSummaryItemDto>();
                        viewModel.TopMarteyCount = 0;
                        viewModel.TopMarteyTotal = 0;
                    }
                }

                // 2b. Real Loans from AccountTransaction (Fallback grouping by User Account if SP was empty)
                if (viewModel.TopLoans == null || !viewModel.TopLoans.Any())
                {
                    var bankCoaIds = await _context.BankMasters.Where(b => b.ChartOfAccountId != null).Select(b => b.ChartOfAccountId!.Value).ToListAsync();

                    var loanTxList = await _context.AccountTransactions
                        .Where(at => (at.CompanyId == null || at.CompanyId == companyId)
                                  && (at.Type == "Loan" || (at.Narration != null && at.Narration.Contains("Loan")) || (at.EntryTypeName != null && at.EntryTypeName.Contains("Loan")) || (at.ChartofAccountName != null && at.ChartofAccountName.Contains("Loan")))
                                  && (at.Credit > 0 || at.Debit > 0 || (at.Amount ?? 0) > 0)
                                  && (at.ChartofAccountId == null || !bankCoaIds.Contains(at.ChartofAccountId.Value))
                                  && (at.AccountName == null || !at.AccountName.Contains("Cash"))
                                  && (at.ChartofAccountName == null || !at.ChartofAccountName.Contains("Cash")))
                        .ToListAsync();

                    if (loanTxList.Any())
                    {
                        var groupedLoans = loanTxList
                            .GroupBy(at => at.ChartofAccountId ?? 0)
                            .Select(g =>
                            {
                                var first = g.First();
                                decimal totCredit = g.Sum(x => x.Credit);
                                decimal totDebit = g.Sum(x => x.Debit);
                                decimal netBal = Math.Abs(totDebit - totCredit);
                                string name = !string.IsNullOrWhiteSpace(first.AccountName) ? first.AccountName : (!string.IsNullOrWhiteSpace(first.ChartofAccountName) ? first.ChartofAccountName : "Loan Account");
                                var lastTx = g.Max(x => x.ExtraDate);
                                return new
                                {
                                    ChartofAccountId = g.Key,
                                    Title = name,
                                    Subtitle = $"Dr: Rs {totDebit:N0} | Cr: Rs {totCredit:N0}",
                                    Amount = netBal,
                                    ExtraDate = lastTx
                                };
                            })
                            .Where(x => x.Amount > 0)
                            .ToList();

                        viewModel.TopLoansCount = groupedLoans.Count;
                        viewModel.TopLoansTotal = groupedLoans.Sum(l => l.Amount);

                        viewModel.TopLoans = groupedLoans
                            .OrderByDescending(l => l.Amount)
                            .Take(5)
                            .Select(l => new DashboardSummaryItemDto
                            {
                                Title = l.Title,
                                Subtitle = l.Subtitle,
                                Amount = l.Amount
                            }).ToList();

                        viewModel.RecentLoans = groupedLoans
                            .OrderByDescending(l => l.ExtraDate ?? DateTime.MinValue)
                            .Take(5)
                            .Select(l => new DashboardSummaryItemDto
                            {
                                Title = l.Title,
                                Subtitle = l.ExtraDate.HasValue ? $"{l.ExtraDate.Value:dd-MMM} · {l.Subtitle}" : l.Subtitle,
                                Amount = l.Amount
                            }).ToList();
                    }
                    else
                    {
                        viewModel.TopLoans = new List<DashboardSummaryItemDto>();
                        viewModel.RecentLoans = new List<DashboardSummaryItemDto>();
                        viewModel.TopLoansCount = 0;
                        viewModel.TopLoansTotal = 0;
                    }
                }

                // 2c. Real Others from AccountTransaction (Fallback grouping by User Account if SP was empty)
                if (viewModel.TopOthers == null || !viewModel.TopOthers.Any())
                {
                    var bankCoaIds = await _context.BankMasters.Where(b => b.ChartOfAccountId != null).Select(b => b.ChartOfAccountId!.Value).ToListAsync();

                    var otherTxList = await _context.AccountTransactions
                        .Where(at => (at.CompanyId == null || at.CompanyId == companyId)
                                  && (at.Type == "Other" || (at.Narration != null && at.Narration.Contains("Other")) || (at.EntryTypeName != null && at.EntryTypeName.Contains("Other")) || (at.ChartofAccountName != null && at.ChartofAccountName.Contains("Other")))
                                  && (at.Credit > 0 || at.Debit > 0 || (at.Amount ?? 0) > 0)
                                  && (at.ChartofAccountId == null || !bankCoaIds.Contains(at.ChartofAccountId.Value))
                                  && (at.AccountName == null || !at.AccountName.Contains("Cash"))
                                  && (at.ChartofAccountName == null || !at.ChartofAccountName.Contains("Cash")))
                        .ToListAsync();

                    if (otherTxList.Any())
                    {
                        var groupedOthers = otherTxList
                            .GroupBy(at => at.ChartofAccountId ?? 0)
                            .Select(g =>
                            {
                                var first = g.First();
                                decimal totCredit = g.Sum(x => x.Credit);
                                decimal totDebit = g.Sum(x => x.Debit);
                                decimal netBal = Math.Abs(totDebit - totCredit);
                                string name = !string.IsNullOrWhiteSpace(first.AccountName) ? first.AccountName : (!string.IsNullOrWhiteSpace(first.ChartofAccountName) ? first.ChartofAccountName : "Other Account");
                                var lastTx = g.Max(x => x.ExtraDate);
                                return new
                                {
                                    ChartofAccountId = g.Key,
                                    Title = name,
                                    Subtitle = $"Dr: Rs {totDebit:N0} | Cr: Rs {totCredit:N0}",
                                    Amount = netBal,
                                    ExtraDate = lastTx
                                };
                            })
                            .Where(x => x.Amount > 0)
                            .ToList();

                        viewModel.TopOthersCount = groupedOthers.Count;
                        viewModel.TopOthersTotal = groupedOthers.Sum(o => o.Amount);

                        viewModel.TopOthers = groupedOthers
                            .OrderByDescending(o => o.Amount)
                            .Take(5)
                            .Select(o => new DashboardSummaryItemDto
                            {
                                Title = o.Title,
                                Subtitle = o.Subtitle,
                                Amount = o.Amount
                            }).ToList();

                        viewModel.RecentOthers = groupedOthers
                            .OrderByDescending(o => o.ExtraDate ?? DateTime.MinValue)
                            .Take(5)
                            .Select(o => new DashboardSummaryItemDto
                            {
                                Title = o.Title,
                                Subtitle = o.ExtraDate.HasValue ? $"{o.ExtraDate.Value:dd-MMM} · {o.Subtitle}" : o.Subtitle,
                                Amount = o.Amount
                            }).ToList();
                    }
                    else
                    {
                        viewModel.TopOthers = new List<DashboardSummaryItemDto>();
                        viewModel.RecentOthers = new List<DashboardSummaryItemDto>();
                        viewModel.TopOthersCount = 0;
                        viewModel.TopOthersTotal = 0;
                    }
                }

                // 3. Real Expenses from AccountTransaction
                var expenseTxList = await _context.AccountTransactions
                    .Where(at => (at.CompanyId == null || at.CompanyId == companyId)
                              && (at.Type == "Expense" )
                              && (at.Debit > 0 || (at.Amount ?? 0) > 0))
                    .OrderByDescending(at => at.ExtraDate)
                    .Take(100)
                    .Select(at => new
                    {
                        at.AccountTransactionId,
                        Title = !string.IsNullOrWhiteSpace(at.ChartofAccountName) ? at.ChartofAccountName : (!string.IsNullOrWhiteSpace(at.AccountName) ? at.AccountName : "General Expense"),
                        Narration = at.Narration,
                        VoucherNo = at.VoucherNo,
                        Amount = at.Debit > 0 ? at.Debit : (at.Amount ?? 0),
                        ExtraDate = at.ExtraDate
                    })
                    .ToListAsync();

                var formattedExpenses = expenseTxList.Select(e =>
                {
                    string rawSub = !string.IsNullOrWhiteSpace(e.Narration) ? e.Narration : (!string.IsNullOrWhiteSpace(e.VoucherNo) ? e.VoucherNo : "Expense Voucher");
                    if (rawSub.Contains("Bill #", StringComparison.OrdinalIgnoreCase))
                    {
                        var bMatch = System.Text.RegularExpressions.Regex.Match(rawSub, @"Bill\s*#?\s*([A-Za-z0-9\-]+)");
                        if (bMatch.Success)
                        {
                            rawSub = $"Bill # {bMatch.Groups[1].Value} · Farmer Expenses";
                        }
                    }
                    if (rawSub.Length > 42)
                    {
                        rawSub = rawSub.Substring(0, 39) + "...";
                    }
                    return new
                    {
                        e.AccountTransactionId,
                        e.Title,
                        Subtitle = rawSub,
                        e.Amount,
                        e.ExtraDate
                    };
                }).ToList();

                if (formattedExpenses.Any())
                {
                    var periodExpenses = (fromDt.HasValue && toDt.HasValue)
                        ? formattedExpenses.Where(e => e.ExtraDate.HasValue && e.ExtraDate.Value.Date >= fromDt.Value.Date && e.ExtraDate.Value.Date <= toDt.Value.Date).ToList()
                        : formattedExpenses;

                    viewModel.TopExpensesCount = periodExpenses.Count;
                    viewModel.TopExpensesTotal = periodExpenses.Sum(e => e.Amount);

                    var topExpSource = periodExpenses.Any() ? periodExpenses : formattedExpenses;
                    viewModel.TopExpenses = topExpSource
                        .OrderByDescending(e => e.Amount)
                        .Take(5)
                        .Select(e => new DashboardSummaryItemDto
                        {
                            Title = e.Title,
                            Subtitle = e.Subtitle,
                            Amount = e.Amount
                        }).ToList();

                    viewModel.RecentExpenses = formattedExpenses
                        .OrderByDescending(e => e.ExtraDate ?? DateTime.MinValue)
                        .Take(5)
                        .Select(e => new DashboardSummaryItemDto
                        {
                            Title = e.Title,
                            Subtitle = e.ExtraDate.HasValue ? $"{e.ExtraDate.Value:dd-MMM} · {e.Subtitle}" : e.Subtitle,
                            Amount = e.Amount
                        }).ToList();
                }
                else
                {
                    viewModel.TopExpenses = new List<DashboardSummaryItemDto>();
                    viewModel.RecentExpenses = new List<DashboardSummaryItemDto>();
                    viewModel.TopExpensesCount = 0;
                    viewModel.TopExpensesTotal = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error populating Martey, Expenses, and Staff: {Message}", ex.Message);
            }
        }

        private static string ResolveVoucherParty(Voucher v)
        {
            if (v.Vendor != null && !string.IsNullOrWhiteSpace(v.Vendor.Name))
                return v.Vendor.Name;

            if (v.AccountTransactions != null && v.AccountTransactions.Any())
            {
                string vt = (v.VoucherType ?? string.Empty).ToUpperInvariant();
                string vn = (v.VoucherNo ?? string.Empty).ToUpperInvariant();

                // 1. Payment or Expense voucher: Debit is the recipient / party / account
                if (vt.Contains("CP") || vt.Contains("BP") || vt.Contains("PAYMENT") || vt.Contains("EXP") || vn.StartsWith("CP") || vn.StartsWith("BP"))
                {
                    var debitTarget = v.AccountTransactions.FirstOrDefault(t => t.Debit > 0 && !IsCashOrBank(t.AccountName) && !IsCashOrBank(t.ChartofAccountName))
                                      ?? v.AccountTransactions.FirstOrDefault(t => t.Debit > 0);
                    if (debitTarget != null)
                    {
                        var name = !string.IsNullOrWhiteSpace(debitTarget.AccountName) ? debitTarget.AccountName : debitTarget.ChartofAccountName;
                        if (!string.IsNullOrWhiteSpace(name)) return name;
                    }
                }
                // 2. Receipt voucher: Credit is the payer / customer / party
                else if (vt.Contains("CR") || vt.Contains("BR") || vt.Contains("RECEIPT") || vn.StartsWith("CR") || vn.StartsWith("BR"))
                {
                    var creditTarget = v.AccountTransactions.FirstOrDefault(t => t.Credit > 0 && !IsCashOrBank(t.AccountName) && !IsCashOrBank(t.ChartofAccountName))
                                       ?? v.AccountTransactions.FirstOrDefault(t => t.Credit > 0);
                    if (creditTarget != null)
                    {
                        var name = !string.IsNullOrWhiteSpace(creditTarget.AccountName) ? creditTarget.AccountName : creditTarget.ChartofAccountName;
                        if (!string.IsNullOrWhiteSpace(name)) return name;
                    }
                }

                // 3. Any transaction that is NOT cash or bank
                var nonCashBankTx = v.AccountTransactions.FirstOrDefault(t => !IsCashOrBank(t.AccountName) && !IsCashOrBank(t.ChartofAccountName));
                if (nonCashBankTx != null)
                {
                    var name = !string.IsNullOrWhiteSpace(nonCashBankTx.AccountName) ? nonCashBankTx.AccountName : nonCashBankTx.ChartofAccountName;
                    if (!string.IsNullOrWhiteSpace(name)) return name;
                }

                // 4. First transaction
                var firstTx = v.AccountTransactions.FirstOrDefault();
                if (firstTx != null)
                {
                    var name = !string.IsNullOrWhiteSpace(firstTx.AccountName) ? firstTx.AccountName : firstTx.ChartofAccountName;
                    if (!string.IsNullOrWhiteSpace(name)) return name;
                }
            }

            if (!string.IsNullOrWhiteSpace(v.Narration))
            {
                return v.Narration.Length > 45 ? v.Narration.Substring(0, 45) + "..." : v.Narration;
            }

            return "General / Accounts";
        }

        private static bool IsCashOrBank(string? accountName)
        {
            if (string.IsNullOrWhiteSpace(accountName)) return false;
            var name = accountName.ToLowerInvariant();
            return name.Contains("cash") || name.Contains("bank") || name.Contains("mbl") || name.Contains("hbl") || name.Contains("ubl") || name.Contains("meezan") || name.Contains("habib") || name.Contains("mcb");
        }

        private static string FormatVoucherTypeName(string? type, string? voucherNo)
        {
            string t = (type ?? string.Empty).Trim().ToUpperInvariant();
            string no = (voucherNo ?? string.Empty).Trim().ToUpperInvariant();

            if (t == "JV" || no.StartsWith("JV-") || t.Contains("JOURNAL")) return "Journal Voucher (JV)";
            if (t == "CP" || t == "CPV" || no.StartsWith("CP-") || no.StartsWith("CPV-") || t.Contains("CASH PAYMENT")) return "Cash Payment (CPV)";
            if (t == "CR" || t == "CRV" || no.StartsWith("CR-") || no.StartsWith("CRV-") || t.Contains("CASH RECEIPT")) return "Cash Receipt (CRV)";
            if (t == "BP" || t == "BPV" || no.StartsWith("BP-") || no.StartsWith("BPV-") || t.Contains("BANK PAYMENT")) return "Bank Payment (BPV)";
            if (t == "BR" || t == "BRV" || no.StartsWith("BR-") || no.StartsWith("BRV-") || t.Contains("BANK RECEIPT")) return "Bank Receipt (BRV)";
            if (t == "EV" || no.StartsWith("EV-") || no.StartsWith("EXP-") || t.Contains("EXPENSE")) return "Expense Voucher (EV)";
            if (!string.IsNullOrWhiteSpace(type)) return type;
            return "Voucher";
        }

        private class SpReceivableItem
        {
            public int CustomerId { get; set; }
            public string CustomerName { get; set; } = string.Empty;
            public string? AccountCode { get; set; }
            public string City { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public decimal OpeningBalance { get; set; }
            public decimal TotalInvoiced { get; set; }
            public decimal TotalPaid { get; set; }
            public decimal ReceivableBalance { get; set; }
            public string Status { get; set; } = "Clear";
            public DateTime? LastTxDate { get; set; }
        }

        private class SpPayableItem
        {
            public int SupplierId { get; set; }
            public string SupplierName { get; set; } = string.Empty;
            public string? AccountCode { get; set; }
            public string VillageOrJetty { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public decimal OpeningBalance { get; set; }
            public decimal TotalSold { get; set; }
            public decimal TotalPaid { get; set; }
            public decimal PayableBalance { get; set; }
            public string Status { get; set; } = "Clear";
            public DateTime? LastTxDate { get; set; }
        }

        private class SpAdvanceItem
        {
            public int CustomerId { get; set; }
            public string CustomerName { get; set; } = string.Empty;
            public string? AccountCode { get; set; }
            public string City { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public decimal AdvanceAmount { get; set; }
            public int VoucherCount { get; set; }
            public DateTime? LastTxDate { get; set; }
            public string? LatestVoucherNo { get; set; }
        }

        private class SpCommissionItem
        {
            public long AccountTransactionId { get; set; }
            public string VoucherNo { get; set; } = string.Empty;
            public string InvoiceNo { get; set; } = string.Empty;
            public string Narration { get; set; } = string.Empty;
            public string ChartofAccountName { get; set; } = string.Empty;
            public decimal CommissionAmount { get; set; }
            public DateTime? ExtraDate { get; set; }
            public int IsInPeriod { get; set; }
        }

        private class SpWeeklyTrendItem
        {
            public DateTime DayDate { get; set; }
            public string DayName { get; set; } = string.Empty;
            public decimal DayCommission { get; set; }
            public bool IsToday { get; set; }
        }

        private class SpMonthlyTrendItem
        {
            public int BucketSeq { get; set; }
            public string BucketLabel { get; set; } = string.Empty;
            public decimal BucketCommission { get; set; }
            public bool IsCurrent { get; set; }
        }

        private class SpMarteyItem
        {
            public int ChartofAccountId { get; set; }
            public string AccountTitle { get; set; } = string.Empty;
            public string? AccountCode { get; set; }
            public string Location { get; set; } = "-";
            public decimal TotalDebit { get; set; }
            public decimal TotalCredit { get; set; }
            public decimal NetBalance { get; set; }
            public decimal MarteyBalance { get; set; }
            public string BalanceType { get; set; } = "Net Credit";
            public int VoucherCount { get; set; }
            public DateTime? LastTxDate { get; set; }
            public string? LatestVoucherNo { get; set; }
            public int IsInPeriod { get; set; }
        }

        private class SpLoanItem
        {
            public int ChartofAccountId { get; set; }
            public string AccountTitle { get; set; } = string.Empty;
            public string? AccountCode { get; set; }
            public string Location { get; set; } = "-";
            public decimal TotalDebit { get; set; }
            public decimal TotalCredit { get; set; }
            public decimal NetBalance { get; set; }
            public decimal LoanBalance { get; set; }
            public string BalanceType { get; set; } = "Net Debit";
            public int VoucherCount { get; set; }
            public DateTime? LastTxDate { get; set; }
            public string? LatestVoucherNo { get; set; }
            public int IsInPeriod { get; set; }
        }

        private class SpOtherItem
        {
            public int ChartofAccountId { get; set; }
            public string AccountTitle { get; set; } = string.Empty;
            public string? AccountCode { get; set; }
            public string Location { get; set; } = "-";
            public decimal TotalDebit { get; set; }
            public decimal TotalCredit { get; set; }
            public decimal NetBalance { get; set; }
            public decimal OtherBalance { get; set; }
            public string BalanceType { get; set; } = "Net Debit";
            public int VoucherCount { get; set; }
            public DateTime? LastTxDate { get; set; }
            public string? LatestVoucherNo { get; set; }
            public int IsInPeriod { get; set; }
        }

        [AllowAnonymous]
        [Route("Home/AccessDenied")]
        public IActionResult AccessDenied()
        {
            return View("~/Views/Shared/AccessDenied.cshtml");
        }

        [AllowAnonymous]
        [Route("Home/HandleError/{statusCode:int}")]
        public IActionResult HandleError(int statusCode)
        {
            ViewBag.StatusCode = statusCode;
            ViewBag.ErrorMessage = statusCode switch
            {
                404 => "Page or resource not found.",
                403 => "Access denied. You do not have permission to view this resource.",
                401 => "Unauthorized. Please log in to proceed.",
                500 => "Internal server error.",
                _ => "An unexpected error occurred."
            };
            return View("~/Views/Shared/Error.cshtml", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("~/Views/Shared/Error.cshtml", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
