using System.Data;
using BlueBox.Web.Data;
using BlueBox.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace BlueBox.Web.Services;

public static class ManagerBootstrap
{
    public static async Task CreateAsync(SqlDatabase database, IPasswordHasher<Employee> hasher, string username, string password)
    {
        username = username.Trim();
        if (username.Length is < 1 or > 50 || !PasswordPolicy.IsValidForSetup(password))
            throw new InvalidOperationException($"Choose a username of 1–50 characters and a password of {PasswordPolicy.MinimumSetupLength}–{PasswordPolicy.MaximumLength} characters.");
        await database.InTransactionAsync(async (connection, transaction, ct) =>
        {
            using var check = SqlDatabase.Command("""
                SELECT COUNT_BIG(*) FROM dbo.EMPLOYEE WITH (UPDLOCK, HOLDLOCK)
                WHERE (access_role = 'MANAGER' AND employment_status = 'ACTIVE') OR username = @username;
                """, connection, transaction, SqlDatabase.Parameter("@username", SqlDbType.VarChar, username, 50));
            if (Convert.ToInt64(await check.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) != 0)
                throw new InvalidOperationException("An active manager or that username already exists. No account was changed.");
            var employee = new Employee { FirstName = "BlueBox", LastName = "Manager", Username = username, AccessRole = "MANAGER" };
            using var insert = SqlDatabase.Command("""
                INSERT INTO dbo.EMPLOYEE (first_name, last_name, username, password_hash, access_role, employment_status)
                VALUES ('BlueBox', 'Manager', @username, @hash, 'MANAGER', 'ACTIVE');
                """, connection, transaction, SqlDatabase.Parameter("@username", SqlDbType.VarChar, username, 50),
                SqlDatabase.Parameter("@hash", SqlDbType.VarChar, hasher.HashPassword(employee, password), 255));
            await insert.ExecuteNonQueryAsync(ct);
            return true;
        });
    }
}
