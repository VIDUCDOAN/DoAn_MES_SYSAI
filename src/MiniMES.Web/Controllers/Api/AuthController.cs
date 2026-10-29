using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MiniMES.Web.Dtos;
using MiniMES.Web.Security;
using MiniMES.Web.Services;

namespace MiniMES.Web.Controllers.Api;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult Csrf([FromServices] IAntiforgery antiforgery)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var principal = await auth.LoginAsync(dto.Username, dto.Password);
        if (principal is null) return Problem(statusCode: 401, title: "Tên đăng nhập hoặc mật khẩu không đúng.");
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return Ok(new { fullName = principal.FindFirstValue("FullName") });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new
    {
        employeeId = User.FindFirstValue(ClaimTypes.NameIdentifier),
        username = User.Identity?.Name,
        fullName = User.FindFirstValue("FullName"),
        functions = User.FindAll(Permissions.ClaimType).Select(c => c.Value)
    });
}
