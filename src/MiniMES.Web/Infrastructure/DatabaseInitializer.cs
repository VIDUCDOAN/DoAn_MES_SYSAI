using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using MiniMES.Web.Models;
using MiniMES.Web.Repositories;
using MiniMES.Web.Services;

namespace MiniMES.Web.Infrastructure;

public sealed partial class DatabaseInitializer(SqlConnectionFactory factory, PasswordHasher<UserAccount> hasher,
    AuditRepository audits, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync()
    {
        await using var connection = await factory.OpenAsync();
        if (new[] { "master", "model", "msdb", "tempdb" }.Contains(connection.Database, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Không khởi tạo bảng ứng dụng trong database hệ thống. Hãy tạo database MiniMES riêng.");
        var directory = Path.Combine(AppContext.BaseDirectory, "Database");
        foreach (var file in Directory.GetFiles(directory, "*.sql").OrderBy(Path.GetFileName))
        {
            var script = await File.ReadAllTextAsync(file);
            foreach (var batch in GoSeparator().Split(script).Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                // Chỉ chạy script DDL cố định trong bản phát hành, không ghép SQL từ đầu vào người dùng.
                await connection.ExecuteAsync(batch, commandTimeout: 60);
            }
            logger.LogInformation("Applied database script {File}", Path.GetFileName(file));
        }
    }

    public async Task SeedDemoUsersAsync(string? adminPassword = null, string? viewerPassword = null)
    {
        // Chỉ thao tác CLI có chủ đích, không tự tạo tài khoản khi khởi động web.
        foreach (var (username, fullName, canManage, variable) in new[]
        {
            ("admin", "Quản trị viên", true, "MINIMES_ADMIN_PASSWORD"),
            ("viewer", "Người xem sản phẩm", false, "MINIMES_VIEWER_PASSWORD")
        })
        {
            var password = (canManage ? adminPassword : viewerPassword) ?? Environment.GetEnvironmentVariable(variable);
            if (password is null || password.Length < 12 || password.Length > 128)
                throw new InvalidOperationException($"Cần biến {variable} có độ dài 12–128 ký tự. Không nhập bí mật vào Git.");
            await using var connection = await factory.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
            try
            {
                var id = await connection.QuerySingleOrDefaultAsync<int?>("dbo.SYS_USER_Bootstrap", new
                {
                    Username = username, PasswordHash = hasher.HashPassword(new UserAccount(), password),
                    FullName = fullName, CanManage = canManage
                }, transaction, commandType: CommandType.StoredProcedure);
                if (id.HasValue)
                    await audits.WriteAsync(connection, transaction, new AuditEntry
                    {
                        Who = "System", When = DateTime.UtcNow, WhereIp = "Local", ActionType = "INSERT",
                        EntityName = "SYS_USER", EntityId = id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        NewDataJson = JsonSerializer.Serialize(new { username, fullName, permissionScope = canManage ? "AllInitialFunctions" : "MODEL_VIEW" }),
                        CorrelationId = "seed-demo-users"
                    });
                await transaction.CommitAsync();
                logger.LogInformation("Demo account {Username}: {Result}", username, id.HasValue ? "created" : "already exists, unchanged");
            }
            catch
            {
                if (transaction.Connection is not null) await transaction.RollbackAsync();
                throw;
            }
        }
    }

    public async Task ResetUserPasswordAsync(string username, string password)
    {
        username = username.Trim();
        if (username.Length is < 1 or > 50 || password.Length is < 12 or > 128 || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Cần MINIMES_RESET_USERNAME và MINIMES_RESET_PASSWORD (mật khẩu mới 12–128 ký tự).");

        await using var connection = await factory.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            var user = await connection.QuerySingleOrDefaultAsync<UserAccount>("dbo.SYS_USER_ResetPassword", new
            {
                Username = username,
                PasswordHash = hasher.HashPassword(new UserAccount(), password)
            }, transaction, commandType: CommandType.StoredProcedure);
            if (user is null) throw new BusinessException(404, "Không tìm thấy user để đặt lại mật khẩu.");

            await audits.WriteAsync(connection, transaction, new AuditEntry
            {
                Who = "System", When = DateTime.UtcNow, WhereIp = "Local", ActionType = "PASSWORD_RESET",
                EntityName = "SYS_USER", EntityId = user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                OldDataJson = JsonSerializer.Serialize(new { user.Username, SessionVersion = user.SessionVersion - 1 }),
                NewDataJson = JsonSerializer.Serialize(new { user.Username, user.SessionVersion, PasswordChanged = true }),
                CorrelationId = Guid.NewGuid().ToString()
            });
            await transaction.CommitAsync();
            logger.LogInformation("Password reset completed for user Id {UserId}; permissions unchanged", user.Id);
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    [GeneratedRegex(@"^\s*GO\s*\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();
}
