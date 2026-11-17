using BlueBox.Web.Data;
using Microsoft.Data.SqlClient;

namespace BlueBox.Web.Services;

public sealed record WarehouseTotal(string Name, string Status, long Quantity);
public sealed record OverviewSummary(long Units, int ActiveItems, int ActiveLocations, long Movements);
public sealed record OverviewSnapshot(OverviewSummary Summary, List<WarehouseTotal> Warehouses, List<ActivityRow> Recent);

public sealed class OverviewQueries(SqlDatabase database)
{
    // READ COMMITTED, one round trip. Derive total units from the materialized warehouse rows;
    // counts/recent activity may advance independently without holding global range locks.
    public async Task<OverviewSnapshot> LoadAsync(CancellationToken ct = default)
    {
        await using var connection = await database.OpenConnectionAsync(ct);
        using var command = new SqlCommand("""
            SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
            SELECT l.location_name, l.location_status, COALESCE(SUM(CAST(b.quantity_on_hand AS bigint)), 0) AS units
            FROM dbo.LOCATION AS l LEFT JOIN dbo.CURRENT_INVENTORY AS b ON b.location_id=l.location_id
            GROUP BY l.location_id, l.location_name, l.location_status ORDER BY l.location_name, l.location_id;

            SELECT (SELECT COUNT(*) FROM dbo.ITEM WHERE item_status='ACTIVE') AS items,
                (SELECT COUNT(*) FROM dbo.LOCATION WHERE location_status='ACTIVE') AS locations,
                (SELECT COUNT_BIG(*) FROM dbo.TRANSACTION_LOG) AS movements;

            SELECT TOP (5) t.transaction_id, i.item_name, i.media_format, l.location_name, t.transaction_type,
                t.quantity_moved, e.first_name + ' ' + e.last_name AS employee_name, t.transaction_datetime
            FROM dbo.TRANSACTION_LOG AS t
            INNER JOIN dbo.ITEM AS i ON i.item_id=t.item_id
            INNER JOIN dbo.LOCATION AS l ON l.location_id=t.location_id
            INNER JOIN dbo.EMPLOYEE AS e ON e.employee_id=t.employee_id
            ORDER BY t.transaction_datetime DESC, t.transaction_id DESC;
            """, connection) { CommandTimeout = 30 };
        await using var reader = await command.ExecuteReaderAsync(ct);
        var warehouses = new List<WarehouseTotal>();
        while (await reader.ReadAsync(ct)) warehouses.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt64(2)));
        await reader.NextResultAsync(ct);
        await reader.ReadAsync(ct);
        var summary = new OverviewSummary(warehouses.Sum(w => w.Quantity), reader.GetInt32(0), reader.GetInt32(1), reader.GetInt64(2));
        await reader.NextResultAsync(ct);
        var recent = new List<ActivityRow>();
        while (await reader.ReadAsync(ct)) recent.Add(new()
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            Format = reader.GetString(2),
            Location = reader.GetString(3),
            Direction = reader.GetString(4),
            Quantity = reader.GetInt32(5),
            Employee = reader.GetString(6),
            Time = DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc)
        });
        return new OverviewSnapshot(summary, warehouses, recent);
    }
}
