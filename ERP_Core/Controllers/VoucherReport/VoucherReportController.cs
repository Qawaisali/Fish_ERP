using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ERP_Core.Data;
using ERP_Core.Dtos;
using ERP_Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP_Core.Controllers.VoucherReport
{
    [Authorize]
    [Route("VoucherReport")]
    public class VoucherReportController : Controller
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<VoucherReportController> _logger;
        private static bool _spDeploymentAttempted = false;

        public VoucherReportController(ERPDbContext context, ILogger<VoucherReportController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(
            string? fromDate,
            string? toDate,
            string? voucherType,
            string? voucherNo,
            int? accountId)
        {
            ViewData["Title"] = "Voucher Report";
            ViewData["SubTitle"] = "BP, CP, BR, CR aur JV vouchers ka tafseeli statement aur double-entry record.";

            int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;

            var todayStr = DateTime.Today.ToString("yyyy-MM-dd");
            var effectiveFromStr = string.IsNullOrWhiteSpace(fromDate) ? todayStr : fromDate;
            var effectiveToStr = string.IsNullOrWhiteSpace(toDate) ? todayStr : toDate;

            DateTime parsedFrom = DateTime.TryParse(effectiveFromStr, out var pf) ? pf : DateTime.Today;
            DateTime parsedTo = DateTime.TryParse(effectiveToStr, out var pt) ? pt : DateTime.Today;

            var model = new VoucherReportViewModel
            {
                FromDate = effectiveFromStr,
                ToDate = effectiveToStr,
                SelectedVoucherType = voucherType,
                SelectedVoucherNo = voucherNo,
                SelectedAccountId = accountId
            };

            try
            {
                // 1. Fetch Company details dynamically from Company Master (CCompanies)
                var company = await _context.CCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId);
                if (company == null)
                {
                    company = await _context.CCompanies.AsNoTracking().FirstOrDefaultAsync();
                }

                if (company != null)
                {
                    model.CompanyName = !string.IsNullOrWhiteSpace(company.CompanyName) ? company.CompanyName : "Zahid Hussain & Sons";
                    model.CompanyAddress = !string.IsNullOrWhiteSpace(company.Address) ? company.Address : "Fish Commission Agents · Sukkur Fish Harbour";
                    var phone = company.Phone?.Trim();
                    var mobile = company.Mobile?.Trim();
                    string phoneStr = "";
                    if (!string.IsNullOrEmpty(phone) && !string.IsNullOrEmpty(mobile) && phone != mobile)
                        phoneStr = $"{phone} | {mobile}";
                    else if (!string.IsNullOrEmpty(phone))
                        phoneStr = phone;
                    else if (!string.IsNullOrEmpty(mobile))
                        phoneStr = mobile;

                    model.CompanyPhone = !string.IsNullOrWhiteSpace(phoneStr) ? phoneStr : "";
                    model.Logo = company.Logo;
                }
                else
                {
                    model.CompanyName = "Zahid Hussain & Sons";
                    model.CompanyAddress = "Fish Commission Agents · Sukkur Fish Harbour";
                    model.CompanyPhone = "";
                }

                // 2. Fetch all transable Chart of Accounts for dropdown
                var accounts = await _context.ChartOfAccounts
                    .AsNoTracking()
                    .Include(c => c.NatureOfAccount)
                    .Where(c => c.Transable && (c.CompanyId == companyId || c.CompanyId == null))
                    .OrderBy(c => c.AccountCode)
                    .ThenBy(c => c.AccountName)
                    .Select(c => new AccountDropdownDto
                    {
                        Id = c.Id,
                        AccountCode = c.AccountCode ?? "",
                        AccountName = c.AccountName,
                        NatureName = c.NatureOfAccount != null ? c.NatureOfAccount.AccountName : null
                    })
                    .ToListAsync();

                model.AccountsList = accounts;

                if (accountId.HasValue && accountId.Value > 0)
                {
                    var selectedAcc = accounts.FirstOrDefault(a => a.Id == accountId.Value);
                    if (selectedAcc != null)
                    {
                        model.SelectedAccountCode = selectedAcc.AccountCode;
                        model.SelectedAccountName = selectedAcc.AccountName;
                    }
                }

                // 3. Populate Vouchers dropdown matching selected VoucherType and date range
                model.VouchersList = await GetVouchersLookupListAsync(companyId, parsedFrom, parsedTo, voucherType);

                // 4. Try loading report from Stored Procedure sp_GetVoucherReport
                bool spLoaded = await TryLoadVoucherReportFromSpAsync(companyId, parsedFrom, parsedTo, voucherType, voucherNo, accountId, model);

                // 5. Fallback to EF Core LINQ if Stored Procedure failed
                if (!spLoaded)
                {
                    await LoadVoucherReportFromEfAsync(companyId, parsedFrom, parsedTo, voucherType, voucherNo, accountId, model);
                }

                // 6. Group entries by Voucher so all multiple transaction lines appear together per voucher
                model.VoucherGroups = model.Items
                    .GroupBy(i => new { i.VoucherId, i.VoucherNo, i.VoucherDate, i.VoucherType })
                    .Select(g => new VoucherReportGroupDto
                    {
                        VoucherId = g.Key.VoucherId,
                        VoucherNo = g.Key.VoucherNo,
                        VoucherDate = g.Key.VoucherDate,
                        VoucherType = g.Key.VoucherType,
                        VoucherAmount = g.First().VoucherAmount,
                        AmountInWords = g.First().AmountInWords,
                        VoucherNarration = g.First().VoucherNarration,
                        ApprovalStatus = g.First().ApprovalStatus,
                        CreatedBy = g.First().CreatedBy,
                        CreatedOn = g.First().CreatedOn,
                        Lines = g.ToList()
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Voucher Report");
                TempData["ErrorMessage"] = "Failed to load Voucher Report: " + ex.Message;
            }

            return View(model);
        }

        // =========================================================================
        // AJAX: DEPENDENT DROPDOWN POPULATION
        // =========================================================================
        [HttpGet]
        [Route("GetVouchersByType")]
        public async Task<IActionResult> GetVouchersByType(string? voucherType, string? fromDate, string? toDate)
        {
            try
            {
                int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;
                DateTime parsedFrom = DateTime.TryParse(fromDate, out var pf) ? pf : DateTime.Today;
                DateTime parsedTo = DateTime.TryParse(toDate, out var pt) ? pt : DateTime.Today;

                var vouchers = await GetVouchersLookupListAsync(companyId, parsedFrom, parsedTo, voucherType);
                return Json(new { success = true, vouchers });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================================================================
        // AJAX: VOUCHER PRINT PREVIEW DATA
        // =========================================================================
        [HttpGet]
        [Route("GetVoucherPrintData")]
        public async Task<IActionResult> GetVoucherPrintData(int voucherId)
        {
            try
            {
                var v = await _context.Vouchers
                    .AsNoTracking()
                    .Include(x => x.AccountTransactions)
                    .FirstOrDefaultAsync(x => x.VoucherId == voucherId);

                if (v == null) return Json(new { success = false, message = "Voucher not found" });

                var lines = v.AccountTransactions.Select(t => new
                {
                    accountCode = t.Code ?? "",
                    accountName = t.AccountName ?? t.ChartofAccountName ?? "General Account",
                    debit = t.Debit,
                    credit = t.Credit,
                    narration = t.Narration ?? "",
                    chequeNo = t.ChequeNo ?? "",
                    chequeDate = t.ChequeDate.HasValue ? t.ChequeDate.Value.ToString("dd-MMM-yyyy") : ""
                }).ToList();

                int companyId = v.CompanyId ?? HttpContext.Session.GetInt32("CompanyId") ?? 1;
                var company = await _context.CCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId);
                if (company == null)
                {
                    company = await _context.CCompanies.AsNoTracking().FirstOrDefaultAsync();
                }
                
                string compName = (company != null && !string.IsNullOrWhiteSpace(company.CompanyName)) ? company.CompanyName : "Zahid Hussain & Sons";
                string compAddress = (company != null && !string.IsNullOrWhiteSpace(company.Address)) ? company.Address : "Fish Commission Agents · Sukkur Fish Harbour";
                string compPhone = "";
                if (company != null)
                {
                    var phone = company.Phone?.Trim();
                    var mobile = company.Mobile?.Trim();
                    if (!string.IsNullOrEmpty(phone) && !string.IsNullOrEmpty(mobile) && phone != mobile)
                        compPhone = $"{phone} | {mobile}";
                    else if (!string.IsNullOrEmpty(phone))
                        compPhone = phone;
                    else if (!string.IsNullOrEmpty(mobile))
                        compPhone = mobile;
                }

                return Json(new
                {
                    success = true,
                    voucher = new
                    {
                        voucherId = v.VoucherId,
                        voucherNo = v.VoucherNo,
                        voucherDate = v.VoucherDate.ToString("dd-MMM-yyyy"),
                        voucherType = v.VoucherType,
                        amount = v.Amount,
                        amountInWords = v.AmountInWords,
                        narration = v.Narration,
                        createdBy = v.CreatedBy,
                        companyName = compName,
                        companyAddress = compAddress,
                        companyPhone = compPhone,
                        companyLogo = company?.Logo,
                        lines = lines
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================================================================
        // EXPORT TO CSV
        // =========================================================================
        [HttpGet]
        [Route("ExportCsv")]
        public async Task<IActionResult> ExportCsv(
            string? fromDate,
            string? toDate,
            string? voucherType,
            string? voucherNo,
            int? accountId)
        {
            int companyId = HttpContext.Session.GetInt32("CompanyId") ?? 1;
            DateTime parsedFrom = DateTime.TryParse(fromDate, out var pf) ? pf : DateTime.Today;
            DateTime parsedTo = DateTime.TryParse(toDate, out var pt) ? pt : DateTime.Today;

            var model = new VoucherReportViewModel
            {
                FromDate = parsedFrom.ToString("yyyy-MM-dd"),
                ToDate = parsedTo.ToString("yyyy-MM-dd"),
                SelectedVoucherType = voucherType,
                SelectedVoucherNo = voucherNo,
                SelectedAccountId = accountId
            };

            bool spLoaded = await TryLoadVoucherReportFromSpAsync(companyId, parsedFrom, parsedTo, voucherType, voucherNo, accountId, model);
            if (!spLoaded)
            {
                await LoadVoucherReportFromEfAsync(companyId, parsedFrom, parsedTo, voucherType, voucherNo, accountId, model);
            }

            var sb = new StringBuilder();
            sb.AppendLine("Zahid Hussain & Sons - Voucher Report");
            sb.AppendLine($"Period: {parsedFrom:dd-MMM-yyyy} to {parsedTo:dd-MMM-yyyy}");
            if (!string.IsNullOrWhiteSpace(voucherType)) sb.AppendLine($"Voucher Type: {voucherType}");
            if (!string.IsNullOrWhiteSpace(voucherNo)) sb.AppendLine($"Voucher No: {voucherNo}");
            if (accountId.HasValue && accountId.Value > 0 && !string.IsNullOrWhiteSpace(model.SelectedAccountName))
                sb.AppendLine($"Account: {model.SelectedAccountCode} - {model.SelectedAccountName}");
            sb.AppendLine();

            sb.AppendLine("Voucher Date,Voucher #,Voucher Type,Account Code,Account Title,Narration,Cheque #,Cheque Date,Debit (Rs),Credit (Rs)");

            foreach (var item in model.Items)
            {
                var chqDate = item.ChequeDateFormatted ?? "";
                sb.AppendLine($"\"{item.VoucherDateFormatted}\",\"{item.VoucherNo}\",\"{item.VoucherType}\",\"{item.AccountCode}\",\"{item.AccountName.Replace("\"", "\"\"")}\",\"{item.Narration.Replace("\"", "\"\"")}\",\"{item.ChequeNo}\",\"{chqDate}\",{item.Debit},{item.Credit}");
            }

            sb.AppendLine();
            sb.AppendLine($",,,,TOTALS,,,{model.TotalDebitAmount},{model.TotalCreditAmount}");

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            var fileName = $"VoucherReport_{parsedFrom:yyyyMMdd}_to_{parsedTo:yyyyMMdd}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        // =========================================================================
        // PRIVATE HELPER: VOUCHERS LOOKUP
        // =========================================================================
        private async Task<List<VoucherLookupDto>> GetVouchersLookupListAsync(
            int companyId,
            DateTime fromDate,
            DateTime toDate,
            string? voucherType)
        {
            var fromDateTime = fromDate.Date;
            var toDateTime = toDate.Date.AddDays(1).AddTicks(-1);

            var query = _context.Vouchers
                .AsNoTracking()
                .Where(v => (v.CompanyId == companyId || v.CompanyId == null) &&
                            (v.DeleteStatus == 0 || v.DeleteStatus == null) &&
                            v.VoucherDate >= fromDateTime &&
                            v.VoucherDate <= toDateTime);

            if (!string.IsNullOrWhiteSpace(voucherType) && !voucherType.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                var vTypeUpper = voucherType.Trim().ToUpperInvariant();
                if (vTypeUpper == "BP")
                {
                    query = query.Where(v => v.VoucherType == "BP" || v.VoucherType == "BPV" || (v.VoucherNo != null && v.VoucherNo.StartsWith("BP")));
                }
                else if (vTypeUpper == "CP")
                {
                    query = query.Where(v => v.VoucherType == "CP" || v.VoucherType == "CPV" || (v.VoucherNo != null && v.VoucherNo.StartsWith("CP")));
                }
                else if (vTypeUpper == "BR")
                {
                    query = query.Where(v => v.VoucherType == "BR" || v.VoucherType == "BRV" || (v.VoucherNo != null && v.VoucherNo.StartsWith("BR")));
                }
                else if (vTypeUpper == "CR")
                {
                    query = query.Where(v => v.VoucherType == "CR" || v.VoucherType == "CRV" || (v.VoucherNo != null && v.VoucherNo.StartsWith("CR")));
                }
                else if (vTypeUpper == "JV")
                {
                    query = query.Where(v => v.VoucherType == "JV" || (v.VoucherNo != null && v.VoucherNo.StartsWith("JV")));
                }
                else
                {
                    query = query.Where(v => v.VoucherType == voucherType);
                }
            }

            return await query
                .OrderByDescending(v => v.VoucherDate)
                .ThenByDescending(v => v.VoucherId)
                .Select(v => new VoucherLookupDto
                {
                    VoucherId = v.VoucherId,
                    VoucherNo = v.VoucherNo,
                    VoucherType = v.VoucherType ?? "Voucher",
                    VoucherDate = v.VoucherDate,
                    Amount = v.Amount
                })
                .ToListAsync();
        }

        // =========================================================================
        // PRIVATE HELPER: STORED PROCEDURE EXECUTION
        // =========================================================================
        private async Task<bool> TryLoadVoucherReportFromSpAsync(
            int companyId,
            DateTime fromDate,
            DateTime toDate,
            string? voucherType,
            string? voucherNo,
            int? accountId,
            VoucherReportViewModel model)
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
                cmd.CommandText = "dbo.sp_GetVoucherReport";
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

                var pType = cmd.CreateParameter();
                pType.ParameterName = "@VoucherType";
                pType.Value = !string.IsNullOrWhiteSpace(voucherType) && !voucherType.Equals("ALL", StringComparison.OrdinalIgnoreCase) ? (object)voucherType : DBNull.Value;
                cmd.Parameters.Add(pType);

                var pNo = cmd.CreateParameter();
                pNo.ParameterName = "@VoucherNo";
                pNo.Value = !string.IsNullOrWhiteSpace(voucherNo) && !voucherNo.Equals("ALL", StringComparison.OrdinalIgnoreCase) ? (object)voucherNo : DBNull.Value;
                cmd.Parameters.Add(pNo);

                var pAcc = cmd.CreateParameter();
                pAcc.ParameterName = "@ChartOfAccountId";
                pAcc.Value = accountId.HasValue && accountId.Value > 0 ? (object)accountId.Value : DBNull.Value;
                cmd.Parameters.Add(pAcc);

                await using var reader = await cmd.ExecuteReaderAsync();

                // 1. Result Set 1: Overall Summary KPIs
                if (await reader.ReadAsync())
                {
                    model.TotalVouchersCount = !reader.IsDBNull(reader.GetOrdinal("TotalVouchers")) ? Convert.ToInt32(reader["TotalVouchers"]) : 0;
                    model.TotalDebitAmount = !reader.IsDBNull(reader.GetOrdinal("TotalDebit")) ? Convert.ToDecimal(reader["TotalDebit"]) : 0;
                    model.TotalCreditAmount = !reader.IsDBNull(reader.GetOrdinal("TotalCredit")) ? Convert.ToDecimal(reader["TotalCredit"]) : 0;
                    model.TotalLinesCount = !reader.IsDBNull(reader.GetOrdinal("TotalLines")) ? Convert.ToInt32(reader["TotalLines"]) : 0;
                }

                // 2. Result Set 2: Detail Lines
                if (await reader.NextResultAsync())
                {
                    var items = new List<VoucherReportItemDto>();
                    while (await reader.ReadAsync())
                    {
                        items.Add(new VoucherReportItemDto
                        {
                            VoucherId = Convert.ToInt32(reader["VoucherId"]),
                            VoucherNo = reader["VoucherNo"]?.ToString() ?? "",
                            VoucherDate = Convert.ToDateTime(reader["VoucherDate"]),
                            VoucherType = reader["VoucherType"]?.ToString() ?? "Voucher",
                            VoucherAmount = !reader.IsDBNull(reader.GetOrdinal("VoucherAmount")) ? Convert.ToDecimal(reader["VoucherAmount"]) : 0,
                            AmountInWords = !reader.IsDBNull(reader.GetOrdinal("AmountInWords")) ? reader["AmountInWords"]?.ToString() : null,
                            VoucherNarration = !reader.IsDBNull(reader.GetOrdinal("VoucherNarration")) ? reader["VoucherNarration"]?.ToString() : null,
                            ApprovalStatus = !reader.IsDBNull(reader.GetOrdinal("ApprovalStatus")) ? reader["ApprovalStatus"]?.ToString() : "Approved",
                            CreatedBy = !reader.IsDBNull(reader.GetOrdinal("CreatedBy")) ? reader["CreatedBy"]?.ToString() : null,
                            CreatedOn = !reader.IsDBNull(reader.GetOrdinal("CreatedOn")) ? Convert.ToDateTime(reader["CreatedOn"]) : null,
                            AccountTransactionId = !reader.IsDBNull(reader.GetOrdinal("AccountTransactionId")) ? Convert.ToInt32(reader["AccountTransactionId"]) : null,
                            ChartofAccountId = !reader.IsDBNull(reader.GetOrdinal("ChartofAccountId")) ? Convert.ToInt32(reader["ChartofAccountId"]) : null,
                            AccountCode = reader["AccountCode"]?.ToString() ?? "",
                            AccountName = reader["AccountName"]?.ToString() ?? "General Account",
                            Debit = !reader.IsDBNull(reader.GetOrdinal("Debit")) ? Convert.ToDecimal(reader["Debit"]) : 0,
                            Credit = !reader.IsDBNull(reader.GetOrdinal("Credit")) ? Convert.ToDecimal(reader["Credit"]) : 0,
                            Narration = reader["Narration"]?.ToString() ?? "",
                            ChequeNo = !reader.IsDBNull(reader.GetOrdinal("ChequeNo")) ? reader["ChequeNo"]?.ToString() : null,
                            ChequeDate = !reader.IsDBNull(reader.GetOrdinal("ChequeDate")) ? Convert.ToDateTime(reader["ChequeDate"]) : null,
                            InvoiceNo = !reader.IsDBNull(reader.GetOrdinal("InvoiceNo")) ? reader["InvoiceNo"]?.ToString() : null,
                            IsFilteredAccount = !reader.IsDBNull(reader.GetOrdinal("IsFilteredAccount")) && Convert.ToInt32(reader["IsFilteredAccount"]) == 1
                        });
                    }
                    model.Items = items;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "dbo.sp_GetVoucherReport failed or not deployed; falling back to EF Core LINQ query.");
                return false;
            }
        }

        // =========================================================================
        // PRIVATE HELPER: RESILIENT EF CORE FALLBACK
        // =========================================================================
        private async Task LoadVoucherReportFromEfAsync(
            int companyId,
            DateTime fromDate,
            DateTime toDate,
            string? voucherType,
            string? voucherNo,
            int? accountId,
            VoucherReportViewModel model)
        {
            var fromDateTime = fromDate.Date;
            var toDateTime = toDate.Date.AddDays(1).AddTicks(-1);

            var query = _context.Vouchers
                .AsNoTracking()
                .Include(v => v.AccountTransactions)
                .Where(v => (v.CompanyId == companyId || v.CompanyId == null) &&
                            (v.DeleteStatus == 0 || v.DeleteStatus == null) &&
                            v.VoucherDate >= fromDateTime &&
                            v.VoucherDate <= toDateTime);

            if (!string.IsNullOrWhiteSpace(voucherType) && !voucherType.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                var vTypeUpper = voucherType.Trim().ToUpperInvariant();
                if (vTypeUpper == "BP")
                {
                    query = query.Where(v => v.VoucherType == "BP" || v.VoucherType == "BPV" || (v.VoucherNo != null && v.VoucherNo.StartsWith("BP")));
                }
                else if (vTypeUpper == "CP")
                {
                    query = query.Where(v => v.VoucherType == "CP" || v.VoucherType == "CPV" || (v.VoucherNo != null && v.VoucherNo.StartsWith("CP")));
                }
                else if (vTypeUpper == "BR")
                {
                    query = query.Where(v => v.VoucherType == "BR" || v.VoucherType == "BRV" || (v.VoucherNo != null && v.VoucherNo.StartsWith("BR")));
                }
                else if (vTypeUpper == "CR")
                {
                    query = query.Where(v => v.VoucherType == "CR" || v.VoucherType == "CRV" || (v.VoucherNo != null && v.VoucherNo.StartsWith("CR")));
                }
                else if (vTypeUpper == "JV")
                {
                    query = query.Where(v => v.VoucherType == "JV" || (v.VoucherNo != null && v.VoucherNo.StartsWith("JV")));
                }
                else
                {
                    query = query.Where(v => v.VoucherType == voucherType);
                }
            }

            if (!string.IsNullOrWhiteSpace(voucherNo) && !voucherNo.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(v => v.VoucherNo == voucherNo);
            }

            if (accountId.HasValue && accountId.Value > 0)
            {
                query = query.Where(v => v.AccountTransactions.Any(t => t.ChartofAccountId == accountId.Value));
            }

            var vouchers = await query
                .OrderByDescending(v => v.VoucherDate)
                .ThenByDescending(v => v.VoucherId)
                .ToListAsync();

            var items = new List<VoucherReportItemDto>();
            decimal totalDebit = 0;
            decimal totalCredit = 0;

            foreach (var v in vouchers)
            {
                var normType = v.VoucherType ?? "Voucher";
                var vUpper = normType.ToUpperInvariant();
                if (vUpper.Contains("BP")) normType = "BP";
                else if (vUpper.Contains("CP")) normType = "CP";
                else if (vUpper.Contains("BR")) normType = "BR";
                else if (vUpper.Contains("CR")) normType = "CR";
                else if (vUpper.Contains("JV")) normType = "JV";

                if (v.AccountTransactions != null && v.AccountTransactions.Any())
                {
                    foreach (var tx in v.AccountTransactions.OrderByDescending(t => t.Debit).ThenBy(t => t.AccountTransactionId))
                    {
                        totalDebit += tx.Debit;
                        totalCredit += tx.Credit;
                        items.Add(new VoucherReportItemDto
                        {
                            VoucherId = v.VoucherId,
                            VoucherNo = v.VoucherNo,
                            VoucherDate = v.VoucherDate,
                            VoucherType = normType,
                            VoucherAmount = v.Amount,
                            AmountInWords = v.AmountInWords,
                            VoucherNarration = v.Narration,
                            ApprovalStatus = v.ApprovalStatus ?? "Approved",
                            CreatedBy = v.CreatedBy,
                            CreatedOn = v.CreatedOn,
                            AccountTransactionId = tx.AccountTransactionId,
                            ChartofAccountId = tx.ChartofAccountId,
                            AccountCode = tx.Code ?? "",
                            AccountName = tx.AccountName ?? tx.ChartofAccountName ?? "General Account",
                            Debit = tx.Debit,
                            Credit = tx.Credit,
                            Narration = !string.IsNullOrWhiteSpace(tx.Narration) ? tx.Narration : (v.Narration ?? ""),
                            ChequeNo = tx.ChequeNo,
                            ChequeDate = tx.ChequeDate,
                            InvoiceNo = tx.InvoiceNo,
                            IsFilteredAccount = accountId.HasValue && tx.ChartofAccountId == accountId.Value
                        });
                    }
                }
                else
                {
                    totalDebit += v.Amount;
                    items.Add(new VoucherReportItemDto
                    {
                        VoucherId = v.VoucherId,
                        VoucherNo = v.VoucherNo,
                        VoucherDate = v.VoucherDate,
                        VoucherType = normType,
                        VoucherAmount = v.Amount,
                        AmountInWords = v.AmountInWords,
                        VoucherNarration = v.Narration,
                        ApprovalStatus = v.ApprovalStatus ?? "Approved",
                        CreatedBy = v.CreatedBy,
                        CreatedOn = v.CreatedOn,
                        Debit = v.Amount,
                        Credit = 0,
                        AccountName = "Unassigned",
                        Narration = v.Narration ?? ""
                    });
                }
            }

            model.Items = items;
            model.TotalVouchersCount = vouchers.Count;
            model.TotalDebitAmount = totalDebit;
            model.TotalCreditAmount = totalCredit;
            model.TotalLinesCount = items.Count;
        }

        // =========================================================================
        // PRIVATE HELPER: AUTO-DEPLOY STORED PROCEDURE
        // =========================================================================
        private async Task EnsureStoredProcedureDeployedAsync()
        {
            try
            {
                const string sql = @"
CREATE OR ALTER PROCEDURE dbo.sp_GetVoucherReport
    @CompanyId INT = 1,
    @FromDate DATE = NULL,
    @ToDate DATE = NULL,
    @VoucherType VARCHAR(20) = NULL,
    @VoucherNo VARCHAR(50) = NULL,
    @ChartOfAccountId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FromDate IS NULL
        SET @FromDate = CAST(GETDATE() AS DATE);

    IF @ToDate IS NULL
        SET @ToDate = CAST(GETDATE() AS DATE);

    DECLARE @FromDateTime DATETIME = CAST(@FromDate AS DATETIME);
    DECLARE @ToDateTime DATETIME = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@ToDate AS DATETIME)));

    SET @VoucherType = NULLIF(LTRIM(RTRIM(@VoucherType)), '');
    IF @VoucherType = 'ALL' SET @VoucherType = NULL;

    SET @VoucherNo = NULLIF(LTRIM(RTRIM(@VoucherNo)), '');
    IF @VoucherNo = 'ALL' SET @VoucherNo = NULL;

    IF @ChartOfAccountId <= 0 SET @ChartOfAccountId = NULL;

    ;WITH FilteredVouchers AS (
        SELECT 
            v.VoucherId,
            v.VoucherNo,
            v.VoucherDate,
            v.VoucherType,
            v.Amount,
            v.AmountInWords,
            v.Narration AS VoucherNarration,
            v.ApprovalStatus,
            v.CreatedBy,
            v.CreatedOn,
            v.CompanyId
        FROM dbo.Voucher v
        WHERE (v.CompanyId = @CompanyId OR @CompanyId IS NULL)
          AND ISNULL(v.DeleteStatus, 0) = 0
          AND v.VoucherDate >= @FromDateTime
          AND v.VoucherDate <= @ToDateTime
          AND (
              @VoucherType IS NULL 
              OR (@VoucherType = 'BP' AND (v.VoucherType IN ('BP', 'BPV') OR v.VoucherNo LIKE 'BP%'))
              OR (@VoucherType = 'CP' AND (v.VoucherType IN ('CP', 'CPV') OR v.VoucherNo LIKE 'CP%'))
              OR (@VoucherType = 'BR' AND (v.VoucherType IN ('BR', 'BRV') OR v.VoucherNo LIKE 'BR%'))
              OR (@VoucherType = 'CR' AND (v.VoucherType IN ('CR', 'CRV') OR v.VoucherNo LIKE 'CR%'))
              OR (@VoucherType = 'JV' AND (v.VoucherType = 'JV' OR v.VoucherNo LIKE 'JV%'))
              OR (v.VoucherType = @VoucherType)
          )
          AND (@VoucherNo IS NULL OR v.VoucherNo = @VoucherNo)
          AND (
              @ChartOfAccountId IS NULL 
              OR EXISTS (
                  SELECT 1 
                  FROM dbo.AccountTransaction at_chk 
                  WHERE (at_chk.VoucherId = v.VoucherId OR at_chk.VoucherNo = v.VoucherNo)
                    AND at_chk.ChartofAccountId = @ChartOfAccountId
              )
          )
    )
    SELECT 
        COUNT(DISTINCT fv.VoucherId) AS TotalVouchers,
        ISNULL(SUM(at.Debit), 0.00) AS TotalDebit,
        ISNULL(SUM(at.Credit), 0.00) AS TotalCredit,
        COUNT(at.AccountTransactionId) AS TotalLines,
        ISNULL(SUM(CASE WHEN at.Debit > 0 THEN at.Debit ELSE 0 END), 0.00) AS TotalAmount
    FROM FilteredVouchers fv
    LEFT JOIN dbo.AccountTransaction at ON (at.VoucherId = fv.VoucherId OR (at.VoucherId IS NULL AND at.VoucherNo = fv.VoucherNo));

    ;WITH FilteredVouchers AS (
        SELECT 
            v.VoucherId,
            v.VoucherNo,
            v.VoucherDate,
            v.VoucherType,
            v.Amount,
            v.AmountInWords,
            v.Narration AS VoucherNarration,
            v.ApprovalStatus,
            v.CreatedBy,
            v.CreatedOn,
            v.CompanyId
        FROM dbo.Voucher v
        WHERE (v.CompanyId = @CompanyId OR @CompanyId IS NULL)
          AND ISNULL(v.DeleteStatus, 0) = 0
          AND v.VoucherDate >= @FromDateTime
          AND v.VoucherDate <= @ToDateTime
          AND (
              @VoucherType IS NULL 
              OR (@VoucherType = 'BP' AND (v.VoucherType IN ('BP', 'BPV') OR v.VoucherNo LIKE 'BP%'))
              OR (@VoucherType = 'CP' AND (v.VoucherType IN ('CP', 'CPV') OR v.VoucherNo LIKE 'CP%'))
              OR (@VoucherType = 'BR' AND (v.VoucherType IN ('BR', 'BRV') OR v.VoucherNo LIKE 'BR%'))
              OR (@VoucherType = 'CR' AND (v.VoucherType IN ('CR', 'CRV') OR v.VoucherNo LIKE 'CR%'))
              OR (@VoucherType = 'JV' AND (v.VoucherType = 'JV' OR v.VoucherNo LIKE 'JV%'))
              OR (v.VoucherType = @VoucherType)
          )
          AND (@VoucherNo IS NULL OR v.VoucherNo = @VoucherNo)
          AND (
              @ChartOfAccountId IS NULL 
              OR EXISTS (
                  SELECT 1 
                  FROM dbo.AccountTransaction at_chk 
                  WHERE (at_chk.VoucherId = v.VoucherId OR at_chk.VoucherNo = v.VoucherNo)
                    AND at_chk.ChartofAccountId = @ChartOfAccountId
              )
          )
    )
    SELECT 
        fv.VoucherId,
        fv.VoucherNo,
        fv.VoucherDate,
        UPPER(LTRIM(RTRIM(
            CASE 
                WHEN fv.VoucherType IN ('BP', 'BPV') OR fv.VoucherNo LIKE 'BP%' THEN 'BP'
                WHEN fv.VoucherType IN ('CP', 'CPV') OR fv.VoucherNo LIKE 'CP%' THEN 'CP'
                WHEN fv.VoucherType IN ('BR', 'BRV') OR fv.VoucherNo LIKE 'BR%' THEN 'BR'
                WHEN fv.VoucherType IN ('CR', 'CRV') OR fv.VoucherNo LIKE 'CR%' THEN 'CR'
                WHEN fv.VoucherType = 'JV' OR fv.VoucherNo LIKE 'JV%' THEN 'JV'
                ELSE ISNULL(fv.VoucherType, 'Voucher')
            END
        ))) AS VoucherType,
        ISNULL(fv.Amount, 0.00) AS VoucherAmount,
        fv.AmountInWords,
        fv.VoucherNarration,
        ISNULL(fv.ApprovalStatus, 'Approved') AS ApprovalStatus,
        fv.CreatedBy,
        fv.CreatedOn,
        at.AccountTransactionId,
        at.ChartofAccountId,
        COALESCE(at.Code, coa.AccountCode, '') AS AccountCode,
        COALESCE(at.AccountName, coa.AccountName, at.ChartofAccountName, 'General Account') AS AccountName,
        ISNULL(at.Debit, 0.00) AS Debit,
        ISNULL(at.Credit, 0.00) AS Credit,
        COALESCE(at.Narration, fv.VoucherNarration, '') AS Narration,
        at.ChequeNo,
        at.ChequeDate,
        at.InvoiceNo,
        CASE WHEN at.ChartofAccountId = @ChartOfAccountId THEN 1 ELSE 0 END AS IsFilteredAccount
    FROM FilteredVouchers fv
    LEFT JOIN dbo.AccountTransaction at ON (at.VoucherId = fv.VoucherId OR (at.VoucherId IS NULL AND at.VoucherNo = fv.VoucherNo))
    LEFT JOIN dbo.ChartOfAccounts coa ON coa.Id = at.ChartofAccountId
    ORDER BY fv.VoucherDate DESC, fv.VoucherId DESC, at.Debit DESC, at.AccountTransactionId ASC;
END";

                await _context.Database.ExecuteSqlRawAsync(sql);
                _logger.LogInformation("sp_GetVoucherReport deployed or updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not auto-deploy sp_GetVoucherReport: {Message}", ex.Message);
            }
        }
    }
}
