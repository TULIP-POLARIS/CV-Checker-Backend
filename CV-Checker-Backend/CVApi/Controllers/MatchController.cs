using Microsoft.AspNetCore.Mvc;

namespace CVApi.Controllers
{
    public class MatchController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
