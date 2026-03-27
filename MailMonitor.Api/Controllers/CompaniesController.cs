using Microsoft.AspNetCore.Mvc;

namespace MailMonitor.Api.Controllers
{
    public class CompaniesController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
