using System.Data;
using Microsoft.Data.SqlClient;

namespace BlueBox.Web.Data;

/// <summary>ADO.NET helpers adapted from the instructor's starter template.</summary>
public sealed class SqlDatabase(IConfiguration configuration, ILogger<SqlDatabase> logger)
{
    public string GetConnectionString()
    {
        var configured = configuration.GetConnectionString("BlueBox");
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        var server = configuration["Database:Server"];
        var database = configuration["Database:Name"];
        var username = configuration["Database:Username"];
        var password = configuration["Database:Password"];
        if (new[] { server, database, username, password }.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Configure the Database fields or ConnectionStrings:BlueBox privately. See docs/development.md.");
        return new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            UserID = username,
            Password = password,
            Encrypt = true,
            TrustServerCertificate = configuration.GetValue<bool>("Database:TrustServerCertificate")
        }.ConnectionString;
    }

    public async Task<SqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = new SqlConnection(GetConnectionString());
        try { await connection.OpenAsync(ct); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    public async Task<DataTable> FillDataTableViaSqlAsync(string sql, CancellationToken ct = default)
    {
        using var command = new SqlCommand(sql);
        return await FillDataTableViaCommandAsync(command, ct);
    }

    // Read-only retries: never replay a write on an ambiguous timeout/disconnect.
    public async Task<DataTable> FillDataTableViaCommandAsync(SqlCommand command, CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await using var connection = await OpenConnectionAsync(ct);
                command.Connection = connection;
                command.CommandTimeout = 30;
                await using var reader = await command.ExecuteReaderAsync(ct);
                var table = new DataTable();
                table.Load(reader); // Bounded SELECT results; no unnecessary Task.Run.
                return table;
            }
            catch (SqlException ex) when (ex.Number == 1205 && attempt < 2)
            {
                logger.LogWarning("Read deadlock; retry {Attempt}", attempt + 1);
                await Task.Delay(50 * (attempt + 1), ct);
            }
        }
    }

    public async Task<int> ExecSqlStringAsync(string sql, CancellationToken ct = default)
    {
        using var command = new SqlCommand(sql);
        return await ExecSqlCommandAsync(command, ct);
    }

    public async Task<int> ExecSqlCommandAsync(SqlCommand command, CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        command.Connection = connection;
        command.CommandTimeout = 30;
        return await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<T> InTransactionAsync<T>(Func<SqlConnection, SqlTransaction, CancellationToken, Task<T>> action,
        CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await using var connection = await OpenConnectionAsync(ct);
                await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                try
                {
                    var result = await action(connection, transaction, ct);
                    await transaction.CommitAsync(ct);
                    return result;
                }
                catch
                {
                    if (transaction.Connection is not null) await transaction.RollbackAsync(CancellationToken.None);
                    throw;
                }
            }
            catch (SqlException ex) when (ex.Number == 1205 && attempt < 2)
            {
                // A deadlock victim is rolled back by SQL Server. Retry the whole unit.
                logger.LogWarning("Transaction deadlock; retry {Attempt}", attempt + 1);
                await Task.Delay(50 * (attempt + 1), ct);
            }
        }
    }

    public static SqlParameter Parameter(string name, SqlDbType type, object? value, int size = 0)
    {
        var parameter = new SqlParameter(name, type) { Value = value ?? DBNull.Value };
        if (size > 0) parameter.Size = size;
        return parameter;
    }

    public static SqlCommand Command(string sql, SqlConnection connection, SqlTransaction transaction, params SqlParameter[] parameters)
    {
        var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 30 };
        command.Parameters.AddRange(parameters);
        return command;
    }
}
