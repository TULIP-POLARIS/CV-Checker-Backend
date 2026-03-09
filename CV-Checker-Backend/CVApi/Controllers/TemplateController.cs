using Microsoft.AspNetCore.Mvc;

namespace CVApi.Controllers
{
    public class TemplateController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
