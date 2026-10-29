using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using MiniMES.Web.Models;

namespace MiniMES.Web.Repositories;

public sealed class AuditRepository(SqlConnectionFactory factory)
{
    public Task WriteAsync(SqlConnection connection, SqlTransaction transaction, AuditEntry entry) =>
        connection.ExecuteAsync("dbo.SYS_AUDIT_LOG_Insert", new
        {
            entry.Who, entry.When, entry.WhereIp, entry.ActionType, entry.EntityName, entry.EntityId,
            entry.OldDataJson, entry.NewDataJson, entry.CorrelationId
        }, transaction, commandType: CommandType.StoredProcedure);

    public async Task<IReadOnlyList<AuditEntry>> ListAsync()
    {
        await using var connection = await factory.OpenAsync();
        return (await connection.QueryAsync<AuditEntry>("dbo.SYS_AUDIT_LOG_List",
            commandType: CommandType.StoredProcedure)).AsList();
    }
}
