using Microsoft.AspNetCore.Mvc;

namespace CVApi.Controllers
{
    public class CVController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
