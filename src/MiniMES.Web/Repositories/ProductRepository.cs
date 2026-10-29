using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using MiniMES.Web.Dtos;
using MiniMES.Web.Models;

namespace MiniMES.Web.Repositories;

public sealed class ProductRepository(SqlConnectionFactory factory)
{
    public async Task<IReadOnlyList<Product>> ListAsync()
    {
        await using var connection = await factory.OpenAsync();
        return (await connection.QueryAsync<Product>("dbo.M_PRODUCT_List",
            commandType: CommandType.StoredProcedure)).AsList();
    }

    public Task<Product?> GetForUpdateAsync(SqlConnection connection, SqlTransaction transaction, int id) =>
        connection.QuerySingleOrDefaultAsync<Product>("dbo.M_PRODUCT_GetForUpdate", new { Id = id },
            transaction, commandType: CommandType.StoredProcedure);

    public Task<Product> CreateAsync(SqlConnection connection, SqlTransaction transaction, ProductCreateDto dto) =>
        connection.QuerySingleAsync<Product>("dbo.M_PRODUCT_Create",
            new { dto.ModelCode, dto.ModelName, dto.Description }, transaction, commandType: CommandType.StoredProcedure);

    public Task<Product> UpdateAsync(SqlConnection connection, SqlTransaction transaction, int id, ProductUpdateDto dto) =>
        connection.QuerySingleAsync<Product>("dbo.M_PRODUCT_Update",
            new { Id = id, dto.ModelCode, dto.ModelName, dto.Description, dto.Version },
            transaction, commandType: CommandType.StoredProcedure);

    public Task<Product> DeleteAsync(SqlConnection connection, SqlTransaction transaction, int id, int version) =>
        connection.QuerySingleAsync<Product>("dbo.M_PRODUCT_Delete", new { Id = id, Version = version },
            transaction, commandType: CommandType.StoredProcedure);
}
