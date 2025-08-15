using Microsoft.AspNetCore.Mvc;

namespace SwiftDeploy.Controllers
{
    public class NetlifyController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
