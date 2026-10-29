using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMES.Web.Security;

namespace MiniMES.Web.Controllers.Pages;

[Authorize]
[PermissionAuthorize(Permissions.AuditView)]
public sealed class AuditLogsController : Controller
{
    public IActionResult Index() => View();
}
