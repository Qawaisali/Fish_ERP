using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using ERP_Core.Attributes;
using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP_Core.Controllers.AccountLedger
{
    [Authorize]
    [FormAccessAuthorize("Account Ledger")]
    [Route("AccountLedger")]
    public class AccountLedgerController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<AccountLedgerController> _logger;

        public AccountLedgerController(ERPDbContext context, ILogger<AccountLedgerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(int? accountId, string? account, string? fromDate, string? toDate)
        {
            ViewData["Title"] = "Account Ledger";
            ViewData["SubTitle"] = "Khaata aur hisaab kitaab ka tafseeli statement.";

            // 1. Resolve Company and Financial Year from Session & Claims
            int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;
            var compIdClaim = User.FindFirst("CompanyId")?.Value;
            if (int.TryParse(compIdClaim, out int cid) && cid > 0) companyId = cid;

            int financialYearId = HttpContext.Session.GetInt32("FinancialYearId") ?? 1;
            var fyClaim = User.FindFirst("FinancialYearId")?.Value;
            if (financialYearId <= 1 && int.TryParse(fyClaim, out int fy) && fy > 0) financialYearId = fy;

            // 2. Date Range Handling (Default: First day of current month to Today)
            var today = DateTime.Today;
            var defaultFrom = new DateTime(today.Year, today.Month, 1);
            var parsedFrom = !string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var df) ? df.Date : defaultFrom;
            var parsedTo = !string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var dt) ? dt.Date : today;

            // Match active Financial Year by date if available
            var dateMatch = await _context.CFinancialYears
                .AsNoTracking()
                .Where(f => (f.CompanyID == companyId || f.CompanyID == 0) && f.StartDate <= parsedFrom && f.EndDate >= parsedFrom && (f.Active == null || f.Active == true))
                .Select(f => f.Id)
                .FirstOrDefaultAsync();

            if (dateMatch > 0)
                financialYearId = dateMatch;

            var model = new AccountLedgerViewModel
            {
                FromDate = parsedFrom.ToString("yyyy-MM-dd"),
                ToDate = parsedTo.ToString("yyyy-MM-dd")
            };

            try
            {
                // 3. Fetch Company Details for Header / Print
                var company = await _context.CCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId);
                if (company != null)
                {
                    model.CompanyName = company.CompanyName ?? "Fish Commission Agents";
                    model.CompanyAddress = company.Address ?? "Sukkur Fish Harbour";
                    model.Logo = company.Logo;
                }

                // 4. Fetch all Transable Accounts for the Searchable Dropdown
                var transableAccounts = await _context.ChartOfAccounts
                    .AsNoTracking()
                    .Include(c => c.NatureOfAccount)
                    .Where(c => (c.CompanyId == companyId || c.CompanyId == 0 || c.CompanyId == null) &&
                                c.Transable &&
                                (c.Active == null || c.Active == true))
                    .OrderBy(c => c.AccountCode)
                    .ThenBy(c => c.AccountName)
                    .Select(c => new AccountDropdownDto
                    {
                        Id = c.Id,
                        AccountCode = c.AccountCode ?? string.Empty,
                        AccountName = c.AccountName,
                        NatureName = c.NatureOfAccount != null ? c.NatureOfAccount.AccountName : null
                    })
                    .ToListAsync();

                model.AccountsList = transableAccounts;

                // Support string? account lookup if accountId is omitted
                if ((!accountId.HasValue || accountId.Value <= 0) && !string.IsNullOrWhiteSpace(account))
                {
                    var matched = transableAccounts.FirstOrDefault(a => 
                        a.AccountName.Equals(account, StringComparison.OrdinalIgnoreCase) ||
                        a.DisplayText.IndexOf(account, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (matched != null)
                    {
                        accountId = matched.Id;
                    }
                }

                // 5. If no account is selected, return initial view with empty table
                if (!accountId.HasValue || accountId.Value <= 0)
                {
                    return View(model);
                }

                // 6. Selected Account Verification
                var selectedAcc = transableAccounts.FirstOrDefault(a => a.Id == accountId.Value);
                if (selectedAcc == null)
                {
                    var fallbackAcc = await _context.ChartOfAccounts
                        .AsNoTracking()
                        .Include(c => c.NatureOfAccount)
                        .FirstOrDefaultAsync(c => c.Id == accountId.Value);

                    if (fallbackAcc != null)
                    {
                        selectedAcc = new AccountDropdownDto
                        {
                            Id = fallbackAcc.Id,
                            AccountCode = fallbackAcc.AccountCode ?? string.Empty,
                            AccountName = fallbackAcc.AccountName,
                            NatureName = fallbackAcc.NatureOfAccount?.AccountName
                        };
                    }
                }

                if (selectedAcc == null)
                {
                    TempData["ErrorMessage"] = "Muntakhib kardah account nahi mila.";
                    return View(model);
                }

                model.SelectedAccountId = selectedAcc.Id;
                model.SelectedAccountCode = selectedAcc.AccountCode;
                model.SelectedAccountName = selectedAcc.AccountName;
                model.AccountNature = selectedAcc.NatureName;
                model.SelectedAccount = selectedAcc.DisplayText;

                // 7. Execute Stored Procedure directly
                var conn = _context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                {
                    await conn.OpenAsync();
                }

                await LoadAccountLedgerFromSpAsync(conn, companyId, accountId.Value, parsedFrom, parsedTo, financialYearId, model);

                // Calculate Totals & Balances
                model.TotalDebit = model.Transactions.Sum(t => t.Debit);
                model.TotalCredit = model.Transactions.Sum(t => t.Credit);
                model.ClosingBalance = model.OpeningBalance + (model.TotalDebit - model.TotalCredit);
                model.OutstandingBalance = model.ClosingBalance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Account Ledger for AccountId: {AccountId}", accountId);
                TempData["ErrorMessage"] = "Ledger load karte waqt masla paish aaya: " + ex.Message;
            }

            return View(model);
        }

        #region Stored Procedure Execution
        private async Task LoadAccountLedgerFromSpAsync(
            DbConnection conn,
            int companyId,
            int accountId,
            DateTime fromDate,
            DateTime toDate,
            int financialYearId,
            AccountLedgerViewModel model)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "dbo.sp_GetAccountLedger";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = 60;

            var pComp = cmd.CreateParameter();
            pComp.ParameterName = "@CompanyId";
            pComp.Value = companyId;
            cmd.Parameters.Add(pComp);

            var pAcc = cmd.CreateParameter();
            pAcc.ParameterName = "@ChartOfAccountId";
            pAcc.Value = accountId;
            cmd.Parameters.Add(pAcc);

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

            var statementItems = new List<AccountLedgerRowViewModel>();
            decimal openingBal = 0;
            decimal outstandingBal = 0;

            await using var reader = await cmd.ExecuteReaderAsync();
            do
            {
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(reader.GetOrdinal("OpeningBalance")))
                        openingBal = Convert.ToDecimal(reader["OpeningBalance"]);

                    if (!reader.IsDBNull(reader.GetOrdinal("OutstandingBalance")))
                        outstandingBal = Convert.ToDecimal(reader["OutstandingBalance"]);

                    if (string.IsNullOrEmpty(model.SelectedAccountName) && !reader.IsDBNull(reader.GetOrdinal("AccountName")))
                        model.SelectedAccountName = reader["AccountName"]?.ToString();

                    if (string.IsNullOrEmpty(model.SelectedAccountCode) && !reader.IsDBNull(reader.GetOrdinal("AccountCode")))
                        model.SelectedAccountCode = reader["AccountCode"]?.ToString();

                    if (string.IsNullOrEmpty(model.AccountNature) && !reader.IsDBNull(reader.GetOrdinal("NatureName")))
                        model.AccountNature = reader["NatureName"]?.ToString();

                    DateTime txnDate = !reader.IsDBNull(reader.GetOrdinal("Date"))
                        ? Convert.ToDateTime(reader["Date"])
                        : fromDate;

                    string? vCode = !reader.IsDBNull(reader.GetOrdinal("VoucherNo")) ? reader["VoucherNo"]?.ToString() : null;
                    int? vId = !reader.IsDBNull(reader.GetOrdinal("VoucherId")) ? Convert.ToInt32(reader["VoucherId"]) : null;
                    string? invNo = !reader.IsDBNull(reader.GetOrdinal("InvoiceNo")) ? reader["InvoiceNo"]?.ToString() : null;
                    string vType = !reader.IsDBNull(reader.GetOrdinal("VoucherType")) ? reader["VoucherType"]?.ToString() ?? "Voucher" : "Voucher";
                    string remarks = !reader.IsDBNull(reader.GetOrdinal("Remarks")) ? reader["Remarks"]?.ToString() ?? "" : "";
                    string? chqNo = !reader.IsDBNull(reader.GetOrdinal("ChequeNo")) ? reader["ChequeNo"]?.ToString() : null;
                    DateTime? chqDate = !reader.IsDBNull(reader.GetOrdinal("ChequeDate")) ? Convert.ToDateTime(reader["ChequeDate"]) : null;

                    decimal debit = !reader.IsDBNull(reader.GetOrdinal("Debit")) ? Convert.ToDecimal(reader["Debit"]) : 0;
                    decimal credit = !reader.IsDBNull(reader.GetOrdinal("Credit")) ? Convert.ToDecimal(reader["Credit"]) : 0;
                    decimal bal = !reader.IsDBNull(reader.GetOrdinal("Balance")) ? Convert.ToDecimal(reader["Balance"]) : 0;
                    int txId = !reader.IsDBNull(reader.GetOrdinal("AccountTransactionId")) ? Convert.ToInt32(reader["AccountTransactionId"]) : 0;

                    // Skip placeholder row if no actual transactions existed
                    if (remarks == "No transactions in selected period" && debit == 0 && credit == 0 && string.IsNullOrWhiteSpace(vCode))
                    {
                        continue;
                    }

                    statementItems.Add(new AccountLedgerRowViewModel
                    {
                        AccountTransactionId = txId,
                        Date = txnDate,
                        VoucherNo = vCode,
                        VoucherId = vId,
                        InvoiceNo = invNo,
                        VoucherType = vType,
                        Description = remarks,
                        ChequeNo = chqNo,
                        ChequeDate = chqDate,
                        Debit = debit,
                        Credit = credit,
                        Balance = bal
                    });
                }
            } while (await reader.NextResultAsync());

            model.OpeningBalance = openingBal;
            model.OutstandingBalance = outstandingBal;
            model.Transactions = statementItems;
        }
        #endregion
    }
}
