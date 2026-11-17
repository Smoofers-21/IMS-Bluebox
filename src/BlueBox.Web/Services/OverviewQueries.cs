using System.Data;
using BlueBox.Web.Data;

namespace BlueBox.Web.Services;

public sealed record WarehouseTotal(string Name, string Status, long Quantity);
public sealed record OverviewSummary(long Units, int ActiveItems, int ActiveLocations, long Movements);

public sealed class OverviewQueries(SqlDatabase database)
{
    public async Task<OverviewSummary> SummaryAsync(CancellationToken ct = default)
    {
        var table = await database.FillDataTableViaSqlAsync("""
            SELECT COALESCE((SELECT SUM(CAST(quantity_on_hand AS bigint)) FROM dbo.CURRENT_INVENTORY), 0) AS units,
                (SELECT COUNT(*) FROM dbo.ITEM WHERE item_status='ACTIVE') AS items,
                (SELECT COUNT(*) FROM dbo.LOCATION WHERE location_status='ACTIVE') AS locations,
                (SELECT COUNT_BIG(*) FROM dbo.TRANSACTION_LOG) AS movements;
            """, ct);
        var row = table.Rows[0];
        return new(row.Field<long>("units"), row.Field<int>("items"), row.Field<int>("locations"), row.Field<long>("movements"));
    }

    public async Task<List<WarehouseTotal>> WarehousesAsync(CancellationToken ct = default)
    {
        var table = await database.FillDataTableViaSqlAsync("""
            SELECT l.location_name, l.location_status, COALESCE(SUM(CAST(b.quantity_on_hand AS bigint)), 0) AS units
            FROM dbo.LOCATION AS l LEFT JOIN dbo.CURRENT_INVENTORY AS b ON b.location_id=l.location_id
            GROUP BY l.location_id, l.location_name, l.location_status ORDER BY l.location_name, l.location_id;
            """, ct);
        return table.AsEnumerable().Select(r => new WarehouseTotal(r.Field<string>("location_name")!, r.Field<string>("location_status")!, r.Field<long>("units"))).ToList();
    }
}
