using System.Data;
using BlueBox.Web.Data;
using BlueBox.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace BlueBox.Web.Services;

public sealed record SaveResult(bool Success, string Message);
public sealed record EmployeeSummary(int Id, string FirstName, string LastName, string Username, string Role, string Status);

public sealed class AdministrationService(SqlDatabase database, IPasswordHasher<Employee> hasher)
{
    public async Task<List<EmployeeSummary>> EmployeesAsync(CancellationToken ct = default)
    {
        var table = await database.FillDataTableViaSqlAsync("SELECT employee_id, first_name, last_name, username, access_role, employment_status FROM dbo.EMPLOYEE ORDER BY last_name, first_name, employee_id;", ct);
        return table.AsEnumerable().Select(r => new EmployeeSummary(r.Field<int>("employee_id"), r.Field<string>("first_name")!,
            r.Field<string>("last_name")!, r.Field<string>("username")!, r.Field<string>("access_role")!, r.Field<string>("employment_status")!)).ToList();
    }

    internal static async Task<bool> IsManagerAsync(SqlConnection connection, SqlTransaction transaction, int actor, CancellationToken ct)
    {
        using var command = SqlDatabase.Command("SELECT COUNT(*) FROM dbo.EMPLOYEE WITH (HOLDLOCK) WHERE employee_id = @actor AND employment_status = 'ACTIVE' AND access_role = 'MANAGER';",
            connection, transaction, SqlDatabase.Parameter("@actor", SqlDbType.Int, actor));
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) == 1;
    }

    public Task<SaveResult> SaveItemAsync(int actor, int? id, ItemInput input, CancellationToken ct = default)
    {
        if (!InputValidation.IsValid(input) || id <= 0) return Task.FromResult(new SaveResult(false, "Check the required item fields."));
        return database.InTransactionAsync(async (connection, transaction, token) =>
        {
            if (!await IsManagerAsync(connection, transaction, actor, token)) return new SaveResult(false, "An active manager account is required.");
            if (id.HasValue)
            {
                using var item = SqlDatabase.Command("SELECT item_id FROM dbo.ITEM WITH (UPDLOCK, HOLDLOCK) WHERE item_id = @id;", connection, transaction,
                    SqlDatabase.Parameter("@id", SqlDbType.Int, id));
                if (await item.ExecuteScalarAsync(token) is null) return new SaveResult(false, "Item no longer exists.");
                if (input.Status == "ARCHIVED")
                {
                    using var stock = SqlDatabase.Command("SELECT COUNT(*) FROM dbo.CURRENT_INVENTORY WITH (HOLDLOCK) WHERE item_id = @id AND quantity_on_hand <> 0;", connection, transaction,
                        SqlDatabase.Parameter("@id", SqlDbType.Int, id));
                    if (Convert.ToInt32(await stock.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture) > 0)
                        return new SaveResult(false, "Remove all warehouse stock before archiving this item.");
                }
            }
            using var command = SqlDatabase.Command(id.HasValue ? """
                UPDATE dbo.ITEM SET item_name=@name, item_type=@type, media_format=@format, poster_url=@poster, age_rating=@rating, item_status=@status WHERE item_id=@id;
                """ : """
                INSERT INTO dbo.ITEM (item_name, item_type, media_format, poster_url, age_rating, item_status) VALUES (@name, @type, @format, @poster, @rating, @status);
                """, connection, transaction,
                SqlDatabase.Parameter("@id", SqlDbType.Int, id), SqlDatabase.Parameter("@name", SqlDbType.VarChar, input.Name.Trim(), 150),
                SqlDatabase.Parameter("@type", SqlDbType.VarChar, input.Type, 10), SqlDatabase.Parameter("@format", SqlDbType.VarChar, input.Format.Trim(), 30),
                SqlDatabase.Parameter("@poster", SqlDbType.NVarChar, InputValidation.Optional(input.PosterUrl), 2048),
                SqlDatabase.Parameter("@rating", SqlDbType.VarChar, InputValidation.Optional(input.AgeRating), 20), SqlDatabase.Parameter("@status", SqlDbType.VarChar, input.Status, 10));
            await command.ExecuteNonQueryAsync(token);
            return new SaveResult(true, "Catalog item saved.");
        }, ct);
    }

    public Task<SaveResult> SaveLocationAsync(int actor, int? id, LocationInput input, CancellationToken ct = default)
    {
        if (!InputValidation.IsValid(input) || id <= 0) return Task.FromResult(new SaveResult(false, "Check the required warehouse fields."));
        return database.InTransactionAsync(async (connection, transaction, token) =>
        {
            if (!await IsManagerAsync(connection, transaction, actor, token)) return new SaveResult(false, "An active manager account is required.");
            if (id.HasValue)
            {
                using var location = SqlDatabase.Command("SELECT location_id FROM dbo.LOCATION WITH (UPDLOCK, HOLDLOCK) WHERE location_id=@id;", connection, transaction,
                    SqlDatabase.Parameter("@id", SqlDbType.Int, id));
                if (await location.ExecuteScalarAsync(token) is null) return new SaveResult(false, "Warehouse no longer exists.");
                if (input.Status == "INACTIVE")
                {
                    using var stock = SqlDatabase.Command("SELECT COUNT(*) FROM dbo.CURRENT_INVENTORY WITH (HOLDLOCK) WHERE location_id=@id AND quantity_on_hand <> 0;", connection, transaction,
                        SqlDatabase.Parameter("@id", SqlDbType.Int, id));
                    if (Convert.ToInt32(await stock.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture) > 0)
                        return new SaveResult(false, "Remove all stock before deactivating this warehouse.");
                }
            }
            using var command = SqlDatabase.Command(id.HasValue ? """
                UPDATE dbo.LOCATION SET location_name=@name, address=@address, location_status=@status WHERE location_id=@id;
                """ : """
                INSERT INTO dbo.LOCATION (location_name, address, location_status) VALUES (@name, @address, @status);
                """, connection, transaction,
                SqlDatabase.Parameter("@id", SqlDbType.Int, id), SqlDatabase.Parameter("@name", SqlDbType.VarChar, input.Name.Trim(), 100),
                SqlDatabase.Parameter("@address", SqlDbType.VarChar, input.Address.Trim(), 255), SqlDatabase.Parameter("@status", SqlDbType.VarChar, input.Status, 10));
            await command.ExecuteNonQueryAsync(token);
            return new SaveResult(true, "Warehouse saved.");
        }, ct);
    }

    public async Task<SaveResult> SaveEmployeeAsync(int actor, int? id, EmployeeInput input, CancellationToken ct = default)
    {
        if (!InputValidation.IsValid(input) || id <= 0 || (!id.HasValue && !PasswordPolicy.IsValidForSetup(input.Password)))
            return new SaveResult(false, "Check account fields. New accounts require a password of 12–256 characters.");
        var hash = string.IsNullOrEmpty(input.Password) ? null : hasher.HashPassword(new Employee(), input.Password);
        try
        {
            return await database.InTransactionAsync(async (connection, transaction, token) =>
            {
                // Serialize account edits, including the active-manager count. Small internal employee directory.
                using var directory = SqlDatabase.Command("SELECT COUNT(*) FROM dbo.EMPLOYEE WITH (UPDLOCK, HOLDLOCK);", connection, transaction);
                await directory.ExecuteScalarAsync(token);
                if (!await IsManagerAsync(connection, transaction, actor, token)) return new SaveResult(false, "An active manager account is required.");
                if (id.HasValue)
                {
                    using var target = SqlDatabase.Command("SELECT COUNT(*) FROM dbo.EMPLOYEE WHERE employee_id=@id;", connection, transaction, SqlDatabase.Parameter("@id", SqlDbType.Int, id));
                    if (Convert.ToInt32(await target.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture) == 0)
                        return new SaveResult(false, "Employee no longer exists.");
                    using var managers = SqlDatabase.Command("SELECT COUNT(*) FROM dbo.EMPLOYEE WHERE employment_status='ACTIVE' AND access_role='MANAGER' AND employee_id<>@id;", connection, transaction, SqlDatabase.Parameter("@id", SqlDbType.Int, id));
                    if ((input.Role != "MANAGER" || input.Status != "ACTIVE") && Convert.ToInt32(await managers.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture) == 0)
                        return new SaveResult(false, "Keep at least one active manager.");
                }
                using var command = SqlDatabase.Command(id.HasValue ? """
                    UPDATE dbo.EMPLOYEE SET first_name=@first, last_name=@last, username=@username, access_role=@role,
                        employment_status=@status, password_hash=COALESCE(@hash, password_hash) WHERE employee_id=@id;
                    """ : """
                    INSERT INTO dbo.EMPLOYEE (first_name, last_name, username, access_role, employment_status, password_hash)
                    VALUES (@first, @last, @username, @role, @status, @hash);
                    """, connection, transaction,
                    SqlDatabase.Parameter("@id", SqlDbType.Int, id), SqlDatabase.Parameter("@first", SqlDbType.VarChar, input.FirstName.Trim(), 50),
                    SqlDatabase.Parameter("@last", SqlDbType.VarChar, input.LastName.Trim(), 50), SqlDatabase.Parameter("@username", SqlDbType.VarChar, input.Username.Trim(), 50),
                    SqlDatabase.Parameter("@role", SqlDbType.VarChar, input.Role, 20), SqlDatabase.Parameter("@status", SqlDbType.VarChar, input.Status, 20),
                    SqlDatabase.Parameter("@hash", SqlDbType.VarChar, hash, 255));
                await command.ExecuteNonQueryAsync(token);
                return new SaveResult(true, "Employee account saved.");
            }, ct);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627) { return new SaveResult(false, "That username is already in use."); }
    }
}
