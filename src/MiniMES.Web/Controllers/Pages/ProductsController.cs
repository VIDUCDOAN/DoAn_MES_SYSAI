using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMES.Web.Security;

namespace MiniMES.Web.Controllers.Pages;

[Authorize]
[PermissionAuthorize(Permissions.ProductView)]
public sealed class ProductsController : Controller
{
    public IActionResult Index() => View();
}
