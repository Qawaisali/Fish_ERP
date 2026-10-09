using ERP_Core.Attributes;
using ERP_Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP_Core.Controllers.Settings
{
    [Authorize]
    [FormAccessAuthorize("Settings")]
    [Route("Settings")]
    public class SettingsController : Controller
    {
        [HttpGet]
        [Route("")]
        [Route("Index")]
        public IActionResult Index()
        {
            ViewData["Title"] = "Settings";
            ViewData["SubTitle"] = "System aur print preferences.";

            var model = new SettingsViewModel();
            return View(model);
        }

        [HttpPost]
        [Route("SaveSettings")]
        [ValidateAntiForgeryToken]
        public IActionResult SaveSettings(SettingsViewModel model)
        {
            TempData["SuccessMessage"] = "System preferences updated successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
