using Microsoft.AspNetCore.Mvc;

namespace MailMonitor.Api.Controllers
{
    public class SettingsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
