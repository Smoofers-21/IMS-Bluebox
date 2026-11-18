using BlueBox.Web.Data;
using System.Data;
using Microsoft.Data.SqlClient;
using BlueBox.Web.Models;

namespace BlueBox.Web.Services;

public sealed record CatalogRow
{
    public int ItemId { get; init; }
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string Format { get; init; } = "";
    public string? Rating { get; init; }
    public string Status { get; init; } = "";
    public long OnHand { get; init; }
}
public sealed record InventoryRow
{
    public int ItemId { get; init; }
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string Format { get; init; } = "";
    public string ItemStatus { get; init; } = "";
    public int LocationId { get; init; }
    public string Location { get; init; } = "";
    public string LocationStatus { get; init; } = "";
    public int OnHand { get; init; }
}
public sealed record ActivityRow
{
    public long Id { get; init; }
    public long? CorrectsId { get; init; }
    public long? CorrectionId { get; init; }
    public string Name { get; init; } = "";
    public string Format { get; init; } = "";
    public string Location { get; init; } = "";
    public string Direction { get; init; } = "";
    public int Quantity { get; init; }
    public string Employee { get; init; } = "";
    public DateTime Time { get; init; }
    public string? Notes { get; init; }
}

public sealed record QueryPage<T>(List<T> Rows, bool HasNextPage);

public sealed record ActivityPage(List<ActivityRow> Rows, bool HasNextPage);

public sealed class InventoryQueries(SqlDatabase database)
{
    public const int PageSize = 100;
    public const int ActivityPageSize = PageSize;
    public async Task<QueryPage<CatalogRow>> CatalogAsync(string? search, string? type, int pageNumber = 1, CancellationToken ct = default)
    {
        using var command = SearchCommand("""
            SELECT i.item_id, i.item_name, i.item_type, i.media_format,
                i.age_rating, i.item_status, COALESCE(SUM(CAST(b.quantity_on_hand AS bigint)), 0) AS on_hand
            FROM dbo.ITEM AS i
            LEFT JOIN dbo.CURRENT_INVENTORY AS b ON b.item_id = i.item_id
            WHERE (@search = '' OR CHARINDEX(@search, i.item_name) > 0 OR TRY_CONVERT(int, @search) = i.item_id)
                AND (@type = '' OR i.item_type = @type)
            GROUP BY i.item_id, i.item_name, i.item_type, i.media_format, i.age_rating, i.item_status
            ORDER BY i.item_name, i.media_format, i.item_id
            OFFSET @offset ROWS FETCH NEXT @take ROWS ONLY;
            """, search, type);
        AddPaging(command, pageNumber);
        var table = await database.FillDataTableViaCommandAsync(command, ct);
        var rows = table.AsEnumerable().Take(PageSize).Select(r => new CatalogRow
        {
            ItemId = r.Field<int>("item_id"),
            Name = r.Field<string>("item_name")!,
            Type = r.Field<string>("item_type")!,
            Format = r.Field<string>("media_format")!,
            Rating = r.Field<string?>("age_rating"),
            Status = r.Field<string>("item_status")!,
            OnHand = r.Field<long>("on_hand")
        }).ToList();
        return new QueryPage<CatalogRow>(rows, table.Rows.Count > PageSize);
    }

    public async Task<QueryPage<InventoryRow>> InventoryAsync(string? search, string? type, int? location, int? itemId = null, int pageNumber = 1, CancellationToken ct = default)
    {
        using var command = SearchCommand("""
            SELECT i.item_id, i.item_name, i.item_type, i.media_format, i.item_status,
                l.location_id, l.location_name, l.location_status, COALESCE(b.quantity_on_hand, 0) AS on_hand
            FROM dbo.ITEM AS i CROSS JOIN dbo.LOCATION AS l
            LEFT JOIN dbo.CURRENT_INVENTORY AS b ON b.item_id = i.item_id AND b.location_id = l.location_id
            WHERE (@search = '' OR CHARINDEX(@search, i.item_name) > 0 OR TRY_CONVERT(int, @search) = i.item_id)
                AND (@type = '' OR i.item_type = @type) AND (@location IS NULL OR l.location_id = @location)
                AND (@item IS NULL OR i.item_id = @item)
            ORDER BY i.item_name, l.location_name, i.media_format, i.item_id, l.location_id
            OFFSET @offset ROWS FETCH NEXT @take ROWS ONLY;
            """, search, type);
        command.Parameters.Add(SqlDatabase.Parameter("@location", SqlDbType.Int, location));
        command.Parameters.Add(SqlDatabase.Parameter("@item", SqlDbType.Int, itemId));
        AddPaging(command, pageNumber);
        var table = await database.FillDataTableViaCommandAsync(command, ct);
        var rows = table.AsEnumerable().Take(PageSize).Select(r => new InventoryRow
        {
            ItemId = r.Field<int>("item_id"),
            Name = r.Field<string>("item_name")!,
            Type = r.Field<string>("item_type")!,
            Format = r.Field<string>("media_format")!,
            ItemStatus = r.Field<string>("item_status")!,
            LocationId = r.Field<int>("location_id"),
            Location = r.Field<string>("location_name")!,
            LocationStatus = r.Field<string>("location_status")!,
            OnHand = r.Field<int>("on_hand")
        }).ToList();
        return new QueryPage<InventoryRow>(rows, table.Rows.Count > PageSize);
    }

    public async Task<ActivityPage> ActivityAsync(string? search, string? direction, int? location, int pageNumber = 1, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        using var command = SearchCommand("""
            SELECT t.transaction_id, i.item_name, i.media_format, l.location_name,
                t.transaction_type, t.quantity_moved, e.first_name + ' ' + e.last_name AS employee_name,
                t.transaction_datetime, t.notes, t.corrects_transaction_id,
                (SELECT MIN(c.transaction_id) FROM dbo.TRANSACTION_LOG AS c WHERE c.corrects_transaction_id=t.transaction_id) AS correction_id
            FROM dbo.TRANSACTION_LOG AS t
            INNER JOIN dbo.ITEM AS i ON i.item_id = t.item_id
            INNER JOIN dbo.LOCATION AS l ON l.location_id = t.location_id
            INNER JOIN dbo.EMPLOYEE AS e ON e.employee_id = t.employee_id
            WHERE (@search = '' OR CHARINDEX(@search, i.item_name) > 0 OR TRY_CONVERT(int, @search) = i.item_id)
                AND (@direction = '' OR t.transaction_type = @direction) AND (@location IS NULL OR l.location_id = @location)
            ORDER BY t.transaction_datetime DESC, t.transaction_id DESC
            OFFSET @offset ROWS FETCH NEXT @take ROWS ONLY;
            """, search, null);
        command.Parameters.Add(SqlDatabase.Parameter("@direction", SqlDbType.VarChar, direction ?? "", 10));
        command.Parameters.Add(SqlDatabase.Parameter("@location", SqlDbType.Int, location));
        command.Parameters.Add(SqlDatabase.Parameter("@offset", SqlDbType.BigInt, ((long)pageNumber - 1) * ActivityPageSize));
        command.Parameters.Add(SqlDatabase.Parameter("@take", SqlDbType.Int, ActivityPageSize + 1));
        var table = await database.FillDataTableViaCommandAsync(command, ct);
        var rows = table.AsEnumerable().Take(ActivityPageSize).Select(r => new ActivityRow
        {
            Id = r.Field<long>("transaction_id"),
            CorrectsId = r.Field<long?>("corrects_transaction_id"),
            CorrectionId = r.Field<long?>("correction_id"),
            Name = r.Field<string>("item_name")!,
            Format = r.Field<string>("media_format")!,
            Location = r.Field<string>("location_name")!,
            Direction = r.Field<string>("transaction_type")!,
            Quantity = r.Field<int>("quantity_moved"),
            Employee = r.Field<string>("employee_name")!,
            Time = DateTime.SpecifyKind(r.Field<DateTime>("transaction_datetime"), DateTimeKind.Utc),
            Notes = r.Field<string?>("notes")
        }).ToList();
        return new ActivityPage(rows, table.Rows.Count > ActivityPageSize);
    }

    public async Task<ActivityRow?> MovementAsync(long id, CancellationToken ct = default)
    {
        using var command = new SqlCommand("""
            SELECT t.transaction_id, t.transaction_type, t.quantity_moved, t.transaction_datetime, t.notes,
                t.corrects_transaction_id, i.item_name, i.media_format, l.location_name,
                e.first_name + ' ' + e.last_name AS employee_name,
                (SELECT MIN(c.transaction_id) FROM dbo.TRANSACTION_LOG AS c WHERE c.corrects_transaction_id=t.transaction_id) AS correction_id
            FROM dbo.TRANSACTION_LOG AS t
            INNER JOIN dbo.ITEM AS i ON i.item_id=t.item_id
            INNER JOIN dbo.LOCATION AS l ON l.location_id=t.location_id
            INNER JOIN dbo.EMPLOYEE AS e ON e.employee_id=t.employee_id
            WHERE t.transaction_id=@id;
            """);
        command.Parameters.Add(SqlDatabase.Parameter("@id", SqlDbType.BigInt, id));
        var table = await database.FillDataTableViaCommandAsync(command, ct);
        if (table.Rows.Count == 0) return null;
        var r = table.Rows[0];
        return new ActivityRow
        {
            Id = r.Field<long>("transaction_id"),
            CorrectsId = r.Field<long?>("corrects_transaction_id"),
            CorrectionId = r.Field<long?>("correction_id"),
            Name = r.Field<string>("item_name")!,
            Format = r.Field<string>("media_format")!,
            Location = r.Field<string>("location_name")!,
            Direction = r.Field<string>("transaction_type")!,
            Quantity = r.Field<int>("quantity_moved"),
            Employee = r.Field<string>("employee_name")!,
            Time = DateTime.SpecifyKind(r.Field<DateTime>("transaction_datetime"), DateTimeKind.Utc),
            Notes = r.Field<string?>("notes")
        };
    }

    public async Task<List<Location>> LocationsAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        using var command = new SqlCommand("SELECT location_id, location_name, address, location_status FROM dbo.LOCATION WHERE @active = 0 OR location_status = 'ACTIVE' ORDER BY location_name, location_id;");
        command.Parameters.Add(SqlDatabase.Parameter("@active", SqlDbType.Bit, activeOnly));
        return (await database.FillDataTableViaCommandAsync(command, ct)).AsEnumerable().Select(r => new Location
        {
            LocationId = r.Field<int>("location_id"),
            LocationName = r.Field<string>("location_name")!,
            Address = r.Field<string>("address")!,
            LocationStatus = r.Field<string>("location_status")!
        }).ToList();
    }

    public async Task<List<Item>> ActiveItemsAsync(CancellationToken ct = default)
    {
        var table = await database.FillDataTableViaSqlAsync("SELECT item_id, item_name, item_type, media_format, poster_url, age_rating, item_status FROM dbo.ITEM WHERE item_status = 'ACTIVE' ORDER BY item_name, media_format, item_id;", ct);
        return table.AsEnumerable().Select(ReadItem).ToList();
    }

    public async Task<bool> ItemHasHistoryAsync(int id, CancellationToken ct = default)
    {
        using var command = new SqlCommand("SELECT TOP (1) transaction_id FROM dbo.TRANSACTION_LOG WHERE item_id=@id;");
        command.Parameters.Add(SqlDatabase.Parameter("@id", SqlDbType.Int, id));
        return (await database.FillDataTableViaCommandAsync(command, ct)).Rows.Count > 0;
    }

    public async Task<Item?> ItemAsync(int id, CancellationToken ct = default)
    {
        using var command = new SqlCommand("SELECT item_id, item_name, item_type, media_format, poster_url, age_rating, item_status FROM dbo.ITEM WHERE item_id = @id;");
        command.Parameters.Add(SqlDatabase.Parameter("@id", SqlDbType.Int, id));
        var table = await database.FillDataTableViaCommandAsync(command, ct);
        return table.Rows.Count == 0 ? null : ReadItem(table.Rows[0]);
    }

    public async Task<int> BalanceAsync(int itemId, int locationId, CancellationToken ct = default)
    {
        using var command = new SqlCommand("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id = @item AND location_id = @location;");
        command.Parameters.Add(SqlDatabase.Parameter("@item", SqlDbType.Int, itemId));
        command.Parameters.Add(SqlDatabase.Parameter("@location", SqlDbType.Int, locationId));
        var table = await database.FillDataTableViaCommandAsync(command, ct);
        return table.Rows.Count == 0 ? 0 : table.Rows[0].Field<int>("quantity_on_hand");
    }

    private static void AddPaging(SqlCommand command, int pageNumber)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        command.Parameters.Add(SqlDatabase.Parameter("@offset", SqlDbType.BigInt, ((long)pageNumber - 1) * PageSize));
        command.Parameters.Add(SqlDatabase.Parameter("@take", SqlDbType.Int, PageSize + 1));
    }

    private static SqlCommand SearchCommand(string sql, string? search, string? type)
    {
        var command = new SqlCommand(sql);
        var term = search?.Trim() ?? "";
        if (term.Length > 150) term = term[..150];
        command.Parameters.Add(SqlDatabase.Parameter("@search", SqlDbType.VarChar, term, 150));
        command.Parameters.Add(SqlDatabase.Parameter("@type", SqlDbType.VarChar, type ?? "", 10));
        return command;
    }

    private static Item ReadItem(DataRow r) => new()
    {
        ItemId = r.Field<int>("item_id"),
        ItemName = r.Field<string>("item_name")!,
        ItemType = r.Field<string>("item_type")!,
        MediaFormat = r.Field<string>("media_format")!,
        PosterUrl = r.Field<string?>("poster_url"),
        AgeRating = r.Field<string?>("age_rating"),
        ItemStatus = r.Field<string>("item_status")!
    };
}
