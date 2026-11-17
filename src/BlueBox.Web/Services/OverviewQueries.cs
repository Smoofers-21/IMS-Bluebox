using BlueBox.Web.Data;

namespace BlueBox.Web.Services;

public sealed record WarehouseTotal(string Name, string Status, long Quantity);
public sealed record OverviewSummary(long Units, int ActiveItems, int ActiveLocations, long Movements);
public sealed record OverviewSnapshot(OverviewSummary Summary, List<WarehouseTotal> Warehouses, List<ActivityRow> Recent);

public sealed class OverviewQueries(SqlDatabase database)
{
    // Read all dashboard sections in one serializable transaction, with one database round trip.
    public Task<OverviewSnapshot> LoadAsync(CancellationToken ct = default) => database.InTransactionAsync(async (connection, transaction, token) =>
    {
        using var command = SqlDatabase.Command("""
            SELECT COALESCE((SELECT SUM(CAST(quantity_on_hand AS bigint)) FROM dbo.CURRENT_INVENTORY), 0) AS units,
                (SELECT COUNT(*) FROM dbo.ITEM WHERE item_status='ACTIVE') AS items,
                (SELECT COUNT(*) FROM dbo.LOCATION WHERE location_status='ACTIVE') AS locations,
                (SELECT COUNT_BIG(*) FROM dbo.TRANSACTION_LOG) AS movements;

            SELECT l.location_name, l.location_status, COALESCE(SUM(CAST(b.quantity_on_hand AS bigint)), 0) AS units
            FROM dbo.LOCATION AS l LEFT JOIN dbo.CURRENT_INVENTORY AS b ON b.location_id=l.location_id
            GROUP BY l.location_id, l.location_name, l.location_status ORDER BY l.location_name, l.location_id;

            SELECT TOP (5) t.transaction_id, i.item_name, i.media_format, l.location_name, t.transaction_type,
                t.quantity_moved, e.first_name + ' ' + e.last_name AS employee_name, t.transaction_datetime
            FROM dbo.TRANSACTION_LOG AS t
            INNER JOIN dbo.ITEM AS i ON i.item_id=t.item_id
            INNER JOIN dbo.LOCATION AS l ON l.location_id=t.location_id
            INNER JOIN dbo.EMPLOYEE AS e ON e.employee_id=t.employee_id
            ORDER BY t.transaction_datetime DESC, t.transaction_id DESC;
            """, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(token);
        await reader.ReadAsync(token);
        var summary = new OverviewSummary(reader.GetInt64(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt64(3));
        await reader.NextResultAsync(token);
        var warehouses = new List<WarehouseTotal>();
        while (await reader.ReadAsync(token)) warehouses.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt64(2)));
        await reader.NextResultAsync(token);
        var recent = new List<ActivityRow>();
        while (await reader.ReadAsync(token)) recent.Add(new()
        {
            Id = reader.GetInt64(0), Name = reader.GetString(1), Format = reader.GetString(2), Location = reader.GetString(3),
            Direction = reader.GetString(4), Quantity = reader.GetInt32(5), Employee = reader.GetString(6),
            Time = DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc)
        });
        return new OverviewSnapshot(summary, warehouses, recent);
    }, ct);
}
