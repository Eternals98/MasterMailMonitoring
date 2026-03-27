using Microsoft.AspNetCore.Mvc;

namespace MailMonitor.Api.Controllers
{
    public class EmailStatisticsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
