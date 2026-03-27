using Microsoft.AspNetCore.Mvc;

namespace MailMonitor.Api.Controllers
{
    public class TriggersController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
