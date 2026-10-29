using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniMES.Web.Security;
using Xunit;

namespace MiniMES.Tests;

// Chỉ có trong test assembly: không có đường đăng nhập bằng header ở ứng dụng thật.
public sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Test-User")) return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "1"), new(ClaimTypes.Name, "test") };
        claims.AddRange(Request.Headers["X-Test-Fun"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => new Claim(Permissions.ClaimType, f)));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) { Response.StatusCode = 401; return Task.CompletedTask; }
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) { Response.StatusCode = 403; return Task.CompletedTask; }
}

public sealed class SecurityFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services => services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "Test";
            options.DefaultChallengeScheme = "Test";
            options.DefaultForbidScheme = "Test";
        }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { }));
    }
}

public sealed class SecurityTests(SecurityFactory factory) : IClassFixture<SecurityFactory>
{
    private HttpClient Client(string? functions = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (functions is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-User", "1");
            client.DefaultRequestHeaders.Add("X-Test-Fun", functions);
        }
        return client;
    }

    internal static async Task SetCsrfAsync(HttpClient client)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
    }

    [Fact]
    public async Task AnonymousCannotReadProducts()
    {
        using var client = Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/products")).StatusCode);
    }

    [Fact]
    public async Task MissingFunCannotReadApiOrOpenPage()
    {
        using var client = Client("");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Products")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/audit-logs")).StatusCode);
    }

    [Fact]
    public async Task ViewerPageDoesNotRenderMutationControlsOrAuditNavigation()
    {
        using var client = Client(Permissions.ProductView);
        var response = await client.GetAsync("/Products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("id=\"product-form\"", html);
        Assert.Contains("data-can-edit=\"false\"", html);
        Assert.Contains("data-can-delete=\"false\"", html);
        Assert.DoesNotContain("href=\"/AuditLogs\"", html);
    }

    [Fact]
    public async Task MissingCsrfPreventsMutationEvenWithFun()
    {
        using var client = Client(Permissions.ProductAdd);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/products",
            new { modelCode = "VALID01", modelName = "Sản phẩm thử" })).StatusCode);
    }

    [Fact]
    public async Task ViewerCannotCreateEvenWithCsrf()
    {
        using var client = Client(Permissions.ProductView);
        await SetCsrfAsync(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/products",
            new { modelCode = "VALID02", modelName = "Sản phẩm thử" })).StatusCode);
    }

    [Theory]
    [InlineData("", "Tên hợp lệ")]
    [InlineData("VALID03", "")]
    [InlineData("VALID03", "   ")]
    [InlineData("CODE';DROP TABLE X;--", "Tên hợp lệ")]
    [InlineData("lowercase", "Tên hợp lệ")]
    public async Task InvalidDtoReturns400BeforeDatabase(string modelCode, string modelName)
    {
        using var client = Client(Permissions.ProductAdd);
        await SetCsrfAsync(client);
        var response = await client.PostAsJsonAsync("/api/products", new { modelCode, modelName });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task MissingVersionPreventsUpdate()
    {
        using var client = Client(Permissions.ProductEdit);
        await SetCsrfAsync(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/products/1",
            new { modelCode = "VALID04", modelName = "Tên hợp lệ" })).StatusCode);
    }

    [Fact]
    public async Task LoginPageIsAvailableAndHasAntiforgeryAndSecurityHeaders()
    {
        using var client = Client();
        var response = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("__RequestVerificationToken", html);
        Assert.Contains("type=\"password\"", html);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }
}
