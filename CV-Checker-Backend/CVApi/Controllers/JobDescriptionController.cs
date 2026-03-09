using Microsoft.AspNetCore.Mvc;

namespace CVApi.Controllers
{
    public class JobDescriptionController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
