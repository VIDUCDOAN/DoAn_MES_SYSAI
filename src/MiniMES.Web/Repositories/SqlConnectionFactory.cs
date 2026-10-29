using Microsoft.Data.SqlClient;

namespace MiniMES.Web.Repositories;

public sealed class SqlConnectionFactory(IConfiguration configuration)
{
    public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var value = configuration.GetConnectionString("MiniMes");
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Chưa cấu hình ConnectionStrings:MiniMes. Xem README.md.");
        var connection = new SqlConnection(value);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
