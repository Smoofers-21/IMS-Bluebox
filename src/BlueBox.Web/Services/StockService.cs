using System.Data;
using BlueBox.Web.Data;
using BlueBox.Web.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BlueBox.Web.Services;

public sealed record MovementRequest(int EmployeeId, int ItemId, int LocationId, string Direction, int Quantity, string? Notes);
public sealed record MovementResult(bool Success, string Message, int? Balance = null);

public sealed class StockService(BlueBoxDbContext db)
{
    public async Task<MovementResult> RecordAsync(MovementRequest request, CancellationToken ct = default)
    {
        if (request.Direction is not ("CHECK IN" or "CHECK OUT") || request.Quantity <= 0)
            return new(false, "Choose a movement and enter a positive whole-number quantity.");
        if (request.Notes?.Length > 255)
            return new(false, "Notes must be 255 characters or fewer.");

        // SQL Server deadlock victims are rolled back in full. Retry the entire
        // operation, never just an insert or a balance update on its own.
        for (var attempt = 0; ; attempt++)
        {
            try { return await RecordOnceAsync(request, ct); }
            catch (Exception ex) when (attempt < 2 && (ex is SqlException { Number: 1205 }
                || ex is DbUpdateException { InnerException: SqlException { Number: 1205 } }))
            {
                db.ChangeTracker.Clear();
                await Task.Delay(50 * (attempt + 1), ct);
            }
        }
    }

    private async Task<MovementResult> RecordOnceAsync(MovementRequest request, CancellationToken ct)
    {
        // Serializable protects status reads and absent item/location balances.
        // The conditional UPDATE also prevents stale previews from overselling.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.EmployeeId == request.EmployeeId, ct);
        if (employee is null || employee.EmploymentStatus != "ACTIVE" || employee.AccessRole is not ("STAFF" or "MANAGER"))
            return new(false, "Your account is not permitted to record stock movements.");
        if (!await db.Items.AnyAsync(i => i.ItemId == request.ItemId && i.ItemStatus == "ACTIVE", ct))
            return new(false, "Select an active catalog item.");
        if (!await db.Locations.AnyAsync(l => l.LocationId == request.LocationId && l.LocationStatus == "ACTIVE", ct))
            return new(false, "Select an active warehouse.");

        var balance = await db.Inventory.SingleOrDefaultAsync(i => i.ItemId == request.ItemId && i.LocationId == request.LocationId, ct);
        if (balance is null)
        {
            if (request.Direction == "CHECK OUT")
                return new(false, "Not enough stock at this warehouse. Available: 0.");
            balance = new InventoryBalance { ItemId = request.ItemId, LocationId = request.LocationId, LastUpdatedAt = DateTime.UtcNow };
            db.Inventory.Add(balance);
            await db.SaveChangesAsync(ct);
        }

        var delta = request.Direction == "CHECK IN" ? request.Quantity : -request.Quantity;
        var next = (long)balance.QuantityOnHand + delta;
        if (next < 0)
            return new(false, $"Not enough stock at this warehouse. Available: {balance.QuantityOnHand}.");
        if (next > int.MaxValue)
            return new(false, "This movement would exceed the supported inventory quantity.");

        var now = DateTime.UtcNow;
        var updated = await db.Inventory
            .Where(i => i.ItemId == request.ItemId && i.LocationId == request.LocationId && i.QuantityOnHand == balance.QuantityOnHand)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.QuantityOnHand, (int)next).SetProperty(i => i.LastUpdatedAt, now), ct);
        if (updated != 1)
            return new(false, "Stock changed while you were confirming. Review the balance and try again.");

        db.Transactions.Add(new StockTransaction
        {
            EmployeeId = employee.EmployeeId,
            ItemId = request.ItemId,
            LocationId = request.LocationId,
            TransactionType = request.Direction,
            QuantityMoved = request.Quantity,
            TransactionDatetime = now,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return new(true, "Stock movement recorded.", (int)next);
    }
}
