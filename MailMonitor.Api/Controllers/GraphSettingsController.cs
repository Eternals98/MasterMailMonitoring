using Microsoft.AspNetCore.Mvc;

namespace MailMonitor.Api.Controllers
{
    public class GraphSettingsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
