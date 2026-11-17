using System.Data;
using BlueBox.Web.Data;
using Microsoft.Data.SqlClient;

namespace BlueBox.Web.Services;

public sealed record MovementRequest(int EmployeeId, int ItemId, int LocationId, string Direction, int Quantity, string? Notes);
public sealed record MovementResult(bool Success, string Message, int? Balance = null);

public sealed class StockService(SqlDatabase database)
{
    public Task<MovementResult> RecordAsync(MovementRequest request, CancellationToken ct = default)
    {
        if (request.Direction is not ("CHECK IN" or "CHECK OUT") || request.Quantity <= 0)
            return Task.FromResult(new MovementResult(false, "Choose a movement and enter a positive whole-number quantity."));
        if (request.Notes?.Length > 255)
            return Task.FromResult(new MovementResult(false, "Notes must be 255 characters or fewer."));

        return database.InTransactionAsync((connection, transaction, token) => RecordInTransactionAsync(request, null, connection, transaction, token), ct);
    }

    private static async Task<MovementResult> RecordInTransactionAsync(MovementRequest request, long? corrects, SqlConnection connection, SqlTransaction transaction, CancellationToken token)
    {
        using var access = SqlDatabase.Command("""
            SELECT CASE
                WHEN NOT EXISTS (SELECT 1 FROM dbo.EMPLOYEE WITH (HOLDLOCK)
                    WHERE employee_id = @employee AND employment_status = 'ACTIVE' AND access_role IN ('STAFF', 'MANAGER')) THEN 1
                WHEN NOT EXISTS (SELECT 1 FROM dbo.ITEM WITH (HOLDLOCK)
                    WHERE item_id = @item AND item_status = 'ACTIVE') THEN 2
                WHEN NOT EXISTS (SELECT 1 FROM dbo.LOCATION WITH (HOLDLOCK)
                    WHERE location_id = @location AND location_status = 'ACTIVE') THEN 3
                ELSE 0 END;
            """, connection, transaction,
            SqlDatabase.Parameter("@employee", SqlDbType.Int, request.EmployeeId),
            SqlDatabase.Parameter("@item", SqlDbType.Int, request.ItemId),
            SqlDatabase.Parameter("@location", SqlDbType.Int, request.LocationId));
        var rejected = Convert.ToInt32(await access.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture);
        if (rejected != 0) return new MovementResult(false, rejected switch
        {
            1 => "Your account is not permitted to record stock movements.",
            2 => corrects.HasValue ? "Reactivate this catalog item before correcting its movement." : "Select an active catalog item.",
            _ => corrects.HasValue ? "Reactivate this warehouse before correcting its movement." : "Select an active warehouse."
        });

        // Lock this indexed item/location range, including a missing balance.
        // UPDLOCK avoids two readers both trying to upgrade to a write lock.
        using var read = SqlDatabase.Command("""
            SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WITH (UPDLOCK, HOLDLOCK)
            WHERE item_id = @item AND location_id = @location;
            """, connection, transaction,
            SqlDatabase.Parameter("@item", SqlDbType.Int, request.ItemId), SqlDatabase.Parameter("@location", SqlDbType.Int, request.LocationId));
        var currentValue = await read.ExecuteScalarAsync(token);
        var exists = currentValue is not null;
        var current = exists ? Convert.ToInt32(currentValue, System.Globalization.CultureInfo.InvariantCulture) : 0;
        var next = (long)current + (request.Direction == "CHECK IN" ? request.Quantity : -request.Quantity);
        if (next < 0) return new MovementResult(false, $"Not enough stock at this warehouse. Available: {current}.");
        if (next > int.MaxValue) return new MovementResult(false, "This movement would exceed the supported inventory quantity.");

        using var balance = SqlDatabase.Command(exists ? """
            UPDATE dbo.CURRENT_INVENTORY SET quantity_on_hand = @next, last_updated_at = SYSUTCDATETIME()
            WHERE item_id = @item AND location_id = @location AND quantity_on_hand = @current;
            SELECT @@ROWCOUNT;
            """ : """
            INSERT INTO dbo.CURRENT_INVENTORY (item_id, location_id, quantity_on_hand, last_updated_at)
            VALUES (@item, @location, @next, SYSUTCDATETIME());
            SELECT @@ROWCOUNT;
            """, connection, transaction,
            SqlDatabase.Parameter("@item", SqlDbType.Int, request.ItemId), SqlDatabase.Parameter("@location", SqlDbType.Int, request.LocationId),
            SqlDatabase.Parameter("@next", SqlDbType.Int, (int)next), SqlDatabase.Parameter("@current", SqlDbType.Int, current));
        if (Convert.ToInt32(await balance.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture) != 1)
            throw new InvalidOperationException("Stock changed unexpectedly; transaction will roll back.");

        using var history = SqlDatabase.Command("""
            INSERT INTO dbo.TRANSACTION_LOG
                (employee_id, item_id, location_id, transaction_type, quantity_moved, transaction_datetime, notes, corrects_transaction_id)
            VALUES (@employee, @item, @location, @direction, @quantity, SYSUTCDATETIME(), @notes, @corrects);
            """, connection, transaction,
            SqlDatabase.Parameter("@employee", SqlDbType.Int, request.EmployeeId), SqlDatabase.Parameter("@item", SqlDbType.Int, request.ItemId),
            SqlDatabase.Parameter("@location", SqlDbType.Int, request.LocationId), SqlDatabase.Parameter("@direction", SqlDbType.VarChar, request.Direction, 10),
            SqlDatabase.Parameter("@quantity", SqlDbType.Int, request.Quantity),
            SqlDatabase.Parameter("@notes", SqlDbType.VarChar, string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(), 255),
            SqlDatabase.Parameter("@corrects", SqlDbType.BigInt, corrects));
        await history.ExecuteNonQueryAsync(token);
        return new MovementResult(true, "Stock movement recorded.", (int)next);
    }

    public Task<MovementResult> CorrectAsync(int employeeId, long originalId, string? explanation, CancellationToken ct = default)
    {
        if (originalId <= 0 || string.IsNullOrWhiteSpace(explanation) || explanation.Length > 255)
            return Task.FromResult(new MovementResult(false, "Select a movement and enter an explanation of up to 255 characters."));
        return database.InTransactionAsync(async (connection, transaction, token) =>
        {
            if (!await AdministrationService.IsManagerAsync(connection, transaction, employeeId, token))
                return new MovementResult(false, "Only an active manager can correct a movement.");
            // Every node can have one reversal. Lock the selected node to serialize competing corrections.
            using var original = SqlDatabase.Command("""
                SELECT item_id, location_id, transaction_type, quantity_moved
                FROM dbo.TRANSACTION_LOG WITH (UPDLOCK, HOLDLOCK) WHERE transaction_id=@id;
                """, connection, transaction, SqlDatabase.Parameter("@id", SqlDbType.BigInt, originalId));
            int item, location, quantity; string direction;
            await using (var reader = await original.ExecuteReaderAsync(token))
            {
                if (!await reader.ReadAsync(token)) return new MovementResult(false, "Movement no longer exists.");
                item = reader.GetInt32(0); location = reader.GetInt32(1); direction = reader.GetString(2); quantity = reader.GetInt32(3);
            }
            using var reversed = SqlDatabase.Command("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG WITH (HOLDLOCK) WHERE corrects_transaction_id=@id;",
                connection, transaction, SqlDatabase.Parameter("@id", SqlDbType.BigInt, originalId));
            if (Convert.ToInt32(await reversed.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture) > 0)
                return new MovementResult(false, "This movement already has a correction. Open that correction to reverse it instead.");
            var result = await RecordInTransactionAsync(new(employeeId, item, location, direction == "CHECK IN" ? "CHECK OUT" : "CHECK IN", quantity, explanation.Trim()),
                originalId, connection, transaction, token);
            return result.Success ? result with { Message = "Correction recorded. The original movement is unchanged." } : result;
        }, ct);
    }
}
