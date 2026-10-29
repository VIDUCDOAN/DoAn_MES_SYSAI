using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using MiniMES.Web.Models;
using MiniMES.Web.Repositories;
using MiniMES.Web.Security;

namespace MiniMES.Web.Services;

public sealed class LoginTiming(PasswordHasher<UserAccount> hasher)
{
    public string DummyHash { get; } = hasher.HashPassword(new UserAccount(), Guid.NewGuid().ToString());
}

public sealed class AuthService(UserRepository users, PasswordHasher<UserAccount> hasher, LoginTiming timing)
{
    public async Task<ClaimsPrincipal?> LoginAsync(string username, string password)
    {
        var user = await users.FindByUsernameAsync(username.Trim());
        var verified = PasswordVerification.Verify(hasher, user ?? new UserAccount(), user?.PasswordHash ?? timing.DummyHash, password);
        if (user is null || !user.IsActive || verified == PasswordVerificationResult.Failed)
            return null;

        var session = await users.GetSessionAsync(user.Id);
        if (session.User is null || !session.User.IsActive)
            return null;
        return CreatePrincipal(session.User, session.Functions);
    }

    public static ClaimsPrincipal CreatePrincipal(UserAccount user, IEnumerable<string> functions)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.Username),
            new("FullName", user.FullName),
            new("SessionVersion", user.SessionVersion.ToString(CultureInfo.InvariantCulture))
        };
        claims.AddRange(functions.Select(f => new Claim(Permissions.ClaimType, f)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}

// Kiểm tra lại tài khoản/quyền trong DB mỗi request: thu hồi quyền có hiệu lực ngay.
public sealed class SessionCookieEvents(UserRepository users) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(id, out var userId))
        {
            context.RejectPrincipal();
            return;
        }
        var session = await users.GetSessionAsync(userId);
        if (session.User is null || !session.User.IsActive ||
            session.User.SessionVersion.ToString(CultureInfo.InvariantCulture) != context.Principal?.FindFirstValue("SessionVersion"))
        {
            context.RejectPrincipal();
            return;
        }
        context.ReplacePrincipal(AuthService.CreatePrincipal(session.User, session.Functions));
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = 401;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = 403;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }
}
