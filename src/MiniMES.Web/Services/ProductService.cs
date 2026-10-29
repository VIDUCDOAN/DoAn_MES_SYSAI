using System.Security.Claims;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MiniMES.Web.Dtos;
using MiniMES.Web.Models;
using MiniMES.Web.Repositories;

namespace MiniMES.Web.Services;

public sealed record AuditActor(string EmployeeId, string Ip, string CorrelationId)
{
    public static AuditActor From(HttpContext context) => new(
        context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new BusinessException(401, "Bạn cần đăng nhập."),
        context.Connection.RemoteIpAddress?.ToString() ?? "Unknown", context.TraceIdentifier);
}

public sealed class ProductService(SqlConnectionFactory factory, ProductRepository products, AuditRepository audits)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<Product>> ListAsync() => products.ListAsync();

    public async Task<Product> CreateAsync(ProductCreateDto dto, AuditActor actor)
    {
        Normalize(dto);
        await using var connection = await factory.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            var product = await products.CreateAsync(connection, transaction, dto);
            await WriteAuditAsync(connection, transaction, actor, "INSERT", null, product);
            await transaction.CommitAsync();
            return product;
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Product> UpdateAsync(int id, ProductUpdateDto dto, AuditActor actor)
    {
        Normalize(dto);
        await using var connection = await factory.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            var old = await GetCurrentAsync(connection, transaction, id, dto.Version);
            var updated = await products.UpdateAsync(connection, transaction, id, dto);
            await WriteAuditAsync(connection, transaction, actor, "UPDATE", old, updated);
            await transaction.CommitAsync();
            return updated;
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteAsync(int id, int version, AuditActor actor)
    {
        await using var connection = await factory.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            var old = await GetCurrentAsync(connection, transaction, id, version);
            var deleted = await products.DeleteAsync(connection, transaction, id, version);
            await WriteAuditAsync(connection, transaction, actor, "DELETE", old, deleted);
            await transaction.CommitAsync();
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<Product> GetCurrentAsync(SqlConnection connection, SqlTransaction transaction, int id, int version)
    {
        var product = await products.GetForUpdateAsync(connection, transaction, id);
        if (product is null || product.IsDeleted)
            throw new BusinessException(404, "Sản phẩm không tồn tại hoặc đã ngừng sử dụng.");
        if (product.Version != version)
            throw new BusinessException(409, "Dữ liệu đã được thay đổi. Hãy tải lại trước khi tiếp tục.");
        return product;
    }

    private Task WriteAuditAsync(SqlConnection connection, SqlTransaction transaction, AuditActor actor,
        string action, Product? old, Product current) => audits.WriteAsync(connection, transaction, new AuditEntry
        {
            Who = actor.EmployeeId,
            When = DateTime.UtcNow,
            WhereIp = actor.Ip,
            ActionType = action,
            EntityName = "M_PRODUCT",
            EntityId = current.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            OldDataJson = old is null ? null : JsonSerializer.Serialize(old, JsonOptions),
            NewDataJson = JsonSerializer.Serialize(current, JsonOptions),
            CorrelationId = actor.CorrelationId
        });

    private static void Normalize(ProductCreateDto dto)
    {
        dto.ModelCode = dto.ModelCode.Trim();
        dto.ModelName = dto.ModelName.Trim();
        dto.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        if (dto.ModelName.Length < 2)
            throw new BusinessException(400, "Tên sản phẩm phải có ít nhất 2 ký tự sau khi bỏ khoảng trắng.");
    }
}
