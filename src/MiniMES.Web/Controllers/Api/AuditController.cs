using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMES.Web.Security;
using MiniMES.Web.Services;

namespace MiniMES.Web.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/audit-logs")]
[PermissionAuthorize(Permissions.AuditView)]
public sealed class AuditController(AuditService audits) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await audits.ListAsync());
}
