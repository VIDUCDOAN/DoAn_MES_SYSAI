using System.Data;
using System.Threading.RateLimiting;
using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MiniMES.Web.Infrastructure;
using MiniMES.Web.Models;
using MiniMES.Web.Repositories;
using MiniMES.Web.Services;

string[] managementFlags = ["--initialize-database", "--seed-demo-users", "--reset-user-password"];
var builder = WebApplication.CreateBuilder(args.Where(a => !managementFlags.Contains(a)).ToArray());
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);
// Biến môi trường luôn được ưu tiên hơn file cấu hình local.
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "MiniMES.Csrf";
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "MiniMES.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = false;
    options.EventsType = typeof(SessionCookieEvents);
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
});
builder.Services.Configure<PasswordHasherOptions>(options => options.IterationCount = 200_000);
builder.Services.AddSingleton<PasswordHasher<UserAccount>>();
builder.Services.AddSingleton<LoginTiming>();
builder.Services.AddScoped<SqlConnectionFactory>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<ProductRepository>();
builder.Services.AddScoped<AuditRepository>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<SessionCookieEvents>();
builder.Services.AddScoped<DatabaseInitializer>();

var app = builder.Build();
if (args.Any(managementFlags.Contains))
{
    await using var scope = app.Services.CreateAsyncScope();
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    if (args.Contains("--initialize-database")) await initializer.InitializeAsync();
    if (args.Contains("--seed-demo-users")) await initializer.SeedDemoUsersAsync();
    if (args.Contains("--reset-user-password"))
        await initializer.ResetUserPasswordAsync(
            Environment.GetEnvironmentVariable("MINIMES_RESET_USERNAME") ?? "",
            Environment.GetEnvironmentVariable("MINIMES_RESET_PASSWORD") ?? "");
    return;
}

app.UseMiddleware<ExceptionMiddleware>();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    // Các trang quản trị và dữ liệu cá nhân không được cache trên máy dùng chung.
    if (!context.Request.Path.StartsWithSegments("/css") && !context.Request.Path.StartsWithSegments("/js"))
        context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapGet("/health/ready", async (SqlConnectionFactory factory, ILogger<Program> logger) =>
{
    try
    {
        await using var connection = await factory.OpenAsync();
        var count = await connection.QuerySingleAsync<int>("dbo.SYS_Health", commandType: CommandType.StoredProcedure);
        return count >= 5 ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Database readiness failed");
        return Results.StatusCode(503);
    }
});
app.MapControllers();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();

public partial class Program { }
