using System.Data;
using BlueBox.Web.Models;
using Microsoft.Data.SqlClient;

namespace BlueBox.Web.Data;

public sealed class EmployeeRepository(SqlDatabase database)
{
    public async Task<Employee?> FindByUsernameAsync(string username, CancellationToken ct = default)
    {
        using var command = new SqlCommand("SELECT employee_id, first_name, last_name, username, password_hash, access_role, employment_status FROM dbo.EMPLOYEE WHERE username = @username;");
        command.Parameters.Add(SqlDatabase.Parameter("@username", SqlDbType.VarChar, username, 50));
        return ReadOne(await database.FillDataTableViaCommandAsync(command, ct));
    }

    public async Task<Employee?> FindByIdAsync(int id, CancellationToken ct = default)
    {
        using var command = new SqlCommand("SELECT employee_id, first_name, last_name, username, password_hash, access_role, employment_status FROM dbo.EMPLOYEE WHERE employee_id = @id;");
        command.Parameters.Add(SqlDatabase.Parameter("@id", SqlDbType.Int, id));
        return ReadOne(await database.FillDataTableViaCommandAsync(command, ct));
    }

    public async Task<bool> TryUpgradeHashAsync(int id, string previousHash, string hash, CancellationToken ct = default)
    {
        using var command = new SqlCommand("""
            UPDATE dbo.EMPLOYEE SET password_hash = @hash
            WHERE employee_id = @id AND password_hash COLLATE Latin1_General_100_BIN2 = @previous;
            SELECT @@ROWCOUNT;
            """);
        command.Parameters.Add(SqlDatabase.Parameter("@id", SqlDbType.Int, id));
        command.Parameters.Add(SqlDatabase.Parameter("@hash", SqlDbType.VarChar, hash, 255));
        command.Parameters.Add(SqlDatabase.Parameter("@previous", SqlDbType.VarChar, previousHash, 255));
        await using var connection = await database.OpenConnectionAsync(ct);
        command.Connection = connection;
        command.CommandTimeout = 30;
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) == 1;
    }

    private static Employee? ReadOne(DataTable table)
    {
        if (table.Rows.Count == 0) return null;
        var row = table.Rows[0];
        return new Employee
        {
            EmployeeId = row.Field<int>("employee_id"),
            FirstName = row.Field<string>("first_name")!,
            LastName = row.Field<string>("last_name")!,
            Username = row.Field<string>("username")!,
            PasswordHash = row.Field<string>("password_hash")!,
            AccessRole = row.Field<string>("access_role")!,
            EmploymentStatus = row.Field<string>("employment_status")!
        };
    }
}
