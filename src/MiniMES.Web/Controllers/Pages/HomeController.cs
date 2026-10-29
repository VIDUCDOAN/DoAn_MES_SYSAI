using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MiniMES.Web.Controllers.Pages;

[Authorize]
public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
