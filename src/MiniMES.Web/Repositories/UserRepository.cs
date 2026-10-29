using System.Data;
using Dapper;
using MiniMES.Web.Models;

namespace MiniMES.Web.Repositories;

public sealed class UserRepository(SqlConnectionFactory factory)
{
    public async Task<UserAccount?> FindByUsernameAsync(string username)
    {
        await using var connection = await factory.OpenAsync();
        return await connection.QuerySingleOrDefaultAsync<UserAccount>(
            "dbo.SYS_USER_GetByUsername", new { Username = username }, commandType: CommandType.StoredProcedure);
    }

    public async Task<(UserAccount? User, string[] Functions)> GetSessionAsync(int userId)
    {
        await using var connection = await factory.OpenAsync();
        using var results = await connection.QueryMultipleAsync("dbo.SYS_USER_GetSession",
            new { UserId = userId }, commandType: CommandType.StoredProcedure);
        var user = await results.ReadSingleOrDefaultAsync<UserAccount>();
        var functions = (await results.ReadAsync<string>()).ToArray();
        return (user, functions);
    }
}
