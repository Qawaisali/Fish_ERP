using ERP_Core.Attributes;
using ERP_Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP_Core.Controllers.ProfitLoss
{
    [Authorize]
    [FormAccessAuthorize("Profit & Loss")]
    [Route("ProfitLoss")]
    public class ProfitLossController : Controller
    {
        [HttpGet]
        [Route("")]
        [Route("Index")]
        public IActionResult Index(string? from, string? to)
        {
            ViewData["Title"] = "Profit & Loss";
            ViewData["SubTitle"] = "Mandi karobaar ka nafa aur nuqsaan statement.";

            var model = new ProfitLossViewModel
            {
                PeriodFrom = !string.IsNullOrEmpty(from) ? from : "2026-08-01",
                PeriodTo = !string.IsNullOrEmpty(to) ? to : "2026-08-31",
                CommissionEarned = 984600,
                ExpenseRecovery = 112300,
                LabourExpense = 68400,
                TransportExpense = 51200,
                IcePackingExpense = 22900,
                RentUtilitiesExpense = 35000
            };

            return View(model);
        }
    }
}
