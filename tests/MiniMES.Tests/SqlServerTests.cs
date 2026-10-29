using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniMES.Web.Infrastructure;
using MiniMES.Web.Repositories;
using MiniMES.Web.Services;
using Xunit;

namespace MiniMES.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MINIMES_TEST_CONNECTION_STRING")))
            Skip = "Chưa có SQL Server: cần MINIMES_TEST_CONNECTION_STRING trỏ tới database MiniMES_Tests.";
    }
}

public sealed class SqlServerFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:MiniMes"] = connectionString }));
    }
}

// Không chạy song song với nhóm khác: ca fault injection tạo trigger trong DB test riêng.
[CollectionDefinition("SqlServer", DisableParallelization = true)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture> { }

public sealed class SqlServerFixture : IAsyncLifetime
{
    public SqlServerFactory Factory { get; private set; } = null!;
    public string ConnectionString { get; private set; } = "";
    public string AdminPassword { get; private set; } = "";
    public string ViewerPassword { get; private set; } = "";

    public async Task InitializeAsync()
    {
        ConnectionString = Environment.GetEnvironmentVariable("MINIMES_TEST_CONNECTION_STRING") ?? "";
        if (string.IsNullOrWhiteSpace(ConnectionString)) return;
        if (!string.Equals(new SqlConnectionStringBuilder(ConnectionString).InitialCatalog, "MiniMES_Tests", StringComparison.Ordinal))
            throw new InvalidOperationException("Chỉ chạy integration test trên database có tên MiniMES_Tests.");
        AdminPassword = Required("MINIMES_TEST_ADMIN_PASSWORD");
        ViewerPassword = Required("MINIMES_TEST_VIEWER_PASSWORD");
        Factory = new SqlServerFactory(ConnectionString);
        await using var scope = Factory.Services.CreateAsyncScope();
        await using (var actualConnection = await scope.ServiceProvider.GetRequiredService<SqlConnectionFactory>().OpenAsync())
        {
            if (!string.Equals(actualConnection.Database, "MiniMES_Tests", StringComparison.Ordinal))
                throw new InvalidOperationException("Cấu hình thực tế không trỏ tới MiniMES_Tests. Dừng để bảo vệ dữ liệu.");
        }
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync();
        await initializer.SeedDemoUsersAsync(AdminPassword, ViewerPassword);
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Thiếu {name}; không đưa mật khẩu vào mã nguồn.");

    public async Task<HttpClient> LoginAsync(string username = "admin")
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SecurityTests.SetCsrfAsync(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username, password = username == "admin" ? AdminPassword : ViewerPassword
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Token anonymous cũ không dùng lại sau khi đổi danh tính đăng nhập.
        await SecurityTests.SetCsrfAsync(client);
        return client;
    }
}

[Collection("SqlServer")]
[Trait("Category", "SqlServer")]
public sealed class SqlServerTests(SqlServerFixture fixture)
{
    private static string Code() => "T" + Guid.NewGuid().ToString("N").ToUpperInvariant();

    private static async Task<JsonElement> CreateAsync(HttpClient client, string? code = null, string? description = null)
    {
        var response = await client.PostAsJsonAsync("/api/products", new
        {
            modelCode = code ?? Code(), modelName = "Sản phẩm kiểm thử", description
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [SqlServerFact]
    public async Task RealCookieLoginAndLogoutAndReadiness()
    {
        using var client = await fixture.LoginAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("admin", me.GetProperty("username").GetString());
        Assert.False(me.TryGetProperty("passwordHash", out _));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/products")).StatusCode);
    }

    [SqlServerFact]
    public async Task InvalidCredentialsDoNotCreateSession()
    {
        using var client = fixture.Factory.CreateClient();
        await SecurityTests.SetCsrfAsync(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = Guid.NewGuid().ToString() });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/products")).StatusCode);
    }

    [SqlServerFact]
    public async Task ViewerIsForbiddenToWriteWithRealCookie()
    {
        using var client = await fixture.LoginAsync("viewer");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/products",
            new { modelCode = Code(), modelName = "Không được tạo" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/audit-logs")).StatusCode);
    }

    [SqlServerFact]
    public async Task ProductLifecycleWritesWhoWhenWhereAndBeforeAfter()
    {
        using var client = await fixture.LoginAsync();
        var product = await CreateAsync(client, description: "<script>alert('x')</script> ' OR 1=1 --");
        var id = product.GetProperty("id").GetInt32();
        var code = product.GetProperty("modelCode").GetString();
        var update = await client.PutAsJsonAsync($"/api/products/{id}", new
        {
            modelCode = code, modelName = "Tên sau cập nhật", version = 1, description = "Mô tả mới"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/products/{id}")
        {
            Content = JsonContent.Create(new { version = 2 })
        };
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(deleteRequest)).StatusCode);
        var logs = await client.GetFromJsonAsync<JsonElement>("/api/audit-logs");
        var entries = logs.EnumerateArray().Where(e => e.GetProperty("entityName").GetString() == "M_PRODUCT"
            && e.GetProperty("entityId").GetString() == id.ToString()).ToArray();
        Assert.Equal(3, entries.Length);
        var edit = entries.Single(e => e.GetProperty("actionType").GetString() == "UPDATE");
        Assert.NotEmpty(edit.GetProperty("who").GetString()!);
        Assert.NotEmpty(edit.GetProperty("whereIp").GetString()!);
        Assert.NotEmpty(edit.GetProperty("correlationId").GetString()!);
        Assert.True(edit.TryGetProperty("when", out _));
        var before = JsonDocument.Parse(edit.GetProperty("oldDataJson").GetString()!).RootElement;
        var after = JsonDocument.Parse(edit.GetProperty("newDataJson").GetString()!).RootElement;
        Assert.Equal("Sản phẩm kiểm thử", before.GetProperty("modelName").GetString());
        Assert.Equal("Tên sau cập nhật", after.GetProperty("modelName").GetString());
        Assert.Equal("<script>alert('x')</script> ' OR 1=1 --", before.GetProperty("description").GetString());
        var list = await client.GetFromJsonAsync<JsonElement>("/api/products");
        Assert.DoesNotContain(list.EnumerateArray(), p => p.GetProperty("id").GetInt32() == id);
    }

    [SqlServerFact]
    public async Task DuplicateCodeReturnsConflictWithoutExtraProductOrAudit()
    {
        using var client = await fixture.LoginAsync();
        var code = Code();
        var first = await CreateAsync(client, code);
        var response = await client.PostAsJsonAsync("/api/products", new { modelCode = code, modelName = "Bị trùng mã" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var connection = new SqlConnection(fixture.ConnectionString);
        Assert.Equal(1, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.M_PRODUCT WHERE ModelCode = @Code", new { Code = code }));
        Assert.Equal(1, await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM dbo.SYS_AUDIT_LOG WHERE EntityName = 'M_PRODUCT' AND EntityId = @Id",
            new { Id = first.GetProperty("id").GetInt32().ToString() }));
    }

    [SqlServerFact]
    public async Task ConcurrentEditsWithSameVersionHaveOneWinner()
    {
        using var client = await fixture.LoginAsync();
        var product = await CreateAsync(client);
        var id = product.GetProperty("id").GetInt32();
        var code = product.GetProperty("modelCode").GetString();
        var responses = await Task.WhenAll(Enumerable.Range(1, 2).Select(i => client.PutAsJsonAsync($"/api/products/{id}",
            new { modelCode = code, modelName = "Cập nhật " + i, version = 1 })));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        await using var connection = new SqlConnection(fixture.ConnectionString);
        Assert.Equal(2, await connection.QuerySingleAsync<int>("SELECT Version FROM dbo.M_PRODUCT WHERE Id = @Id", new { Id = id }));
        Assert.Equal(2, await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM dbo.SYS_AUDIT_LOG WHERE EntityName = 'M_PRODUCT' AND EntityId = @Id", new { Id = id.ToString() }));
    }

    [SqlServerFact]
    public async Task MissingProductReturns404()
    {
        using var client = await fixture.LoginAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/products/2147483647",
            new { modelCode = Code(), modelName = "Không tồn tại", version = 1 })).StatusCode);
    }

    [SqlServerFact]
    public async Task PasswordResetKeepsPermissionsRevokesOldCookieAndDoesNotLogCredentials()
    {
        using var client = await fixture.LoginAsync("viewer");
        await using var connection = new SqlConnection(fixture.ConnectionString);
        var before = (await connection.QueryAsync<string>(
            "SELECT FunctionCode FROM dbo.SYS_PERMISSION WHERE UserId = (SELECT Id FROM dbo.SYS_USER WHERE Username = @Username) ORDER BY FunctionCode",
            new { Username = "viewer" })).ToArray();
        var newPassword = Guid.NewGuid().ToString();
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        try
        {
            await initializer.ResetUserPasswordAsync("viewer", newPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
            var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
            Assert.NotNull(await auth.LoginAsync("viewer", newPassword));
            Assert.Null(await auth.LoginAsync("viewer", fixture.ViewerPassword));
            var after = (await connection.QueryAsync<string>(
                "SELECT FunctionCode FROM dbo.SYS_PERMISSION WHERE UserId = (SELECT Id FROM dbo.SYS_USER WHERE Username = @Username) ORDER BY FunctionCode",
                new { Username = "viewer" })).ToArray();
            Assert.Equal(before, after);
            var json = await connection.QuerySingleAsync<string>("""
                SELECT TOP (1) NewDataJson FROM dbo.SYS_AUDIT_LOG
                WHERE EntityName = 'SYS_USER' AND ActionType = 'PASSWORD_RESET'
                  AND EntityId = CONVERT(NVARCHAR(100), (SELECT Id FROM dbo.SYS_USER WHERE Username = @Username))
                ORDER BY Id DESC
                """, new { Username = "viewer" });
            Assert.DoesNotContain(newPassword, json);
            Assert.False(JsonDocument.Parse(json).RootElement.TryGetProperty("PasswordHash", out _));
        }
        finally { await initializer.ResetUserPasswordAsync("viewer", fixture.ViewerPassword); }
    }

    [SqlServerFact]
    public async Task AuditFailureRollsBackCreateUpdateAndDelete()
    {
        using var client = await fixture.LoginAsync();
        var existing = await CreateAsync(client, description: "FAIL_AUDIT_TEST");
        var id = existing.GetProperty("id").GetInt32();
        var code = Code();
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync("""
            CREATE OR ALTER TRIGGER dbo.MiniMES_Test_FailAudit ON dbo.SYS_AUDIT_LOG AFTER INSERT AS
            BEGIN
                IF EXISTS (SELECT 1 FROM inserted WHERE EntityName = 'M_PRODUCT' AND NewDataJson LIKE '%FAIL_AUDIT_TEST%')
                    THROW 51099, 'Intentional test failure: audit write.', 1;
            END;
            """);
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsJsonAsync("/api/products",
                new { modelCode = code, modelName = "FAIL_AUDIT_TEST" })).StatusCode);
            Assert.Equal(0, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.M_PRODUCT WHERE ModelCode = @Code", new { Code = code }));
            Assert.Equal(HttpStatusCode.InternalServerError, (await client.PutAsJsonAsync($"/api/products/{id}", new
            {
                modelCode = existing.GetProperty("modelCode").GetString(), modelName = "Đổi thất bại",
                description = "FAIL_AUDIT_TEST", version = 1
            })).StatusCode);
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/products/{id}") { Content = JsonContent.Create(new { version = 1 }) };
            Assert.Equal(HttpStatusCode.InternalServerError, (await client.SendAsync(request)).StatusCode);
            Assert.Equal(1, await connection.QuerySingleAsync<int>("SELECT Version FROM dbo.M_PRODUCT WHERE Id = @Id AND IsDeleted = 0", new { Id = id }));
            Assert.Equal(1, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.SYS_AUDIT_LOG WHERE EntityName = 'M_PRODUCT' AND EntityId = @Id", new { Id = id.ToString() }));
        }
        finally { await connection.ExecuteAsync("DROP TRIGGER IF EXISTS dbo.MiniMES_Test_FailAudit;"); }
    }
}
