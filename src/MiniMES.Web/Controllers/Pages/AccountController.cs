using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MiniMES.Web.Dtos;
using MiniMES.Web.Services;

namespace MiniMES.Web.Controllers.Pages;

public sealed class AccountController(AuthService auth) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login() => User.Identity?.IsAuthenticated == true
        ? RedirectToAction("Index", "Home") : View(new LoginDto());

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        if (!ModelState.IsValid) return View(dto);
        var principal = await auth.LoginAsync(dto.Username, dto.Password);
        if (principal is null)
        {
            ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng.");
            dto.Password = "";
            return View(dto);
        }
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = 403;
        return View();
    }
}
