using ERP_Core.Attributes;
using ERP_Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP_Core.Controllers.BalanceSheet
{
    [Authorize]
    [FormAccessAuthorize("Balance Sheet")]
    [Route("BalanceSheet")]
    public class BalanceSheetController : Controller
    {
        [HttpGet]
        [Route("")]
        [Route("Index")]
        public IActionResult Index(string? asOnDate)
        {
            ViewData["Title"] = "Balance Sheet";
            ViewData["SubTitle"] = "Assets, liabilities aur equity ka balance hisaab.";

            var model = new BalanceSheetViewModel
            {
                AsOnDate = !string.IsNullOrEmpty(asOnDate) ? asOnDate : "2026-08-31",
                CashInHand = 340000,
                BankBalance = 680500,
                CustomerReceivables = 1280000,
                StockUnsoldLots = 115000,
                SupplierPayables = 940000,
                OutstandingExpenses = 24600,
                OwnersEquity = 1450900
            };

            return View(model);
        }
    }
}
