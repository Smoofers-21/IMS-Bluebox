using System.Net;
using BlueBox.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlueBox.Web.Tests;

public sealed class InventoryVisibilityTests
{
    [SqlServerTheory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task InventoryShowsOnlyPositiveBalancesForAllOrSelectedLocations(int? location)
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        await app.ExecuteAsync("""
            INSERT INTO dbo.ITEM (item_name,item_type,media_format,item_status) VALUES
                ('Stock filter movie','MOVIE','DVD','ACTIVE'),
                ('Stock filter game','GAME','PS5 disc','ACTIVE'),
                ('Stock filter other warehouse','MOVIE','DVD','ACTIVE'),
                ('Stock filter depleted','GAME','Switch cartridge','ACTIVE'),
                ('Stock filter never stocked','MOVIE','Blu-ray','ACTIVE');
            INSERT INTO dbo.CURRENT_INVENTORY (item_id,location_id,quantity_on_hand,last_updated_at)
            SELECT item_id,1,CASE WHEN item_name='Stock filter movie' THEN 3 ELSE 0 END,SYSUTCDATETIME()
            FROM dbo.ITEM WHERE item_name IN ('Stock filter movie','Stock filter depleted');
            INSERT INTO dbo.CURRENT_INVENTORY (item_id,location_id,quantity_on_hand,last_updated_at)
            SELECT item_id,1,2,SYSUTCDATETIME() FROM dbo.ITEM WHERE item_name='Stock filter game';
            INSERT INTO dbo.CURRENT_INVENTORY (item_id,location_id,quantity_on_hand,last_updated_at)
            SELECT item_id,2,4,SYSUTCDATETIME()
            FROM dbo.ITEM WHERE item_name IN ('Stock filter other warehouse','Stock filter depleted');
            """);
        await using var scope = app.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<InventoryQueries>();
        var page = await queries.InventoryAsync("Stock filter", null, location);
        Assert.False(page.HasNextPage);
        var expected = new[]
        {
            (Name: "Stock filter movie", Location: 1, Quantity: 3),
            (Name: "Stock filter game", Location: 1, Quantity: 2),
            (Name: "Stock filter other warehouse", Location: 2, Quantity: 4),
            (Name: "Stock filter depleted", Location: 2, Quantity: 4)
        }.Where(row => location is null || row.Location == location).OrderBy(row => row.Name).ToArray();
        Assert.Equal(expected, page.Rows.Select(row => (row.Name, row.LocationId, row.OnHand)).OrderBy(row => row.Name));
        Assert.All(page.Rows, row => Assert.True(row.OnHand > 0));
        var games = await queries.InventoryAsync("Stock filter", "GAME", location);
        Assert.Equal(expected.Where(row => row.Name is "Stock filter game" or "Stock filter depleted").Select(row => row.Name), games.Rows.Select(row => row.Name));
        var movieId = (int)(await app.ScalarAsync("SELECT item_id FROM dbo.ITEM WHERE item_name='Stock filter movie'"))!;
        var byId = await queries.InventoryAsync(movieId.ToString(System.Globalization.CultureInfo.InvariantCulture), "MOVIE", location);
        Assert.Equal(location == 2 ? 0 : 1, byId.Rows.Count);
        Assert.Empty((await queries.InventoryAsync("Stock filter", null, int.MaxValue)).Rows);

        using var client = app.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await TestApp.LoginAsync(client)).StatusCode);
        var html = await client.GetStringAsync($"/Inventory?Search=Stock%20filter&LocationId={location}");
        Assert.Contains("balances with stock on hand", html);
        Assert.DoesNotContain("including zero stock", html);
        Assert.DoesNotContain("Stock filter never stocked", html);
        foreach (var name in new[] { "Stock filter movie", "Stock filter game", "Stock filter other warehouse", "Stock filter depleted" })
            Assert.Equal(expected.Any(row => row.Name == name), html.Contains(name, StringComparison.Ordinal));
        var empty = await client.GetStringAsync("/Inventory?Search=Stock%20filter&LocationId=2147483647");
        Assert.Contains("No matching inventory", empty);
        // Reset/all locations keeps the same positive-stock rule.
        var reset = await client.GetStringAsync("/Inventory");
        Assert.DoesNotContain("Stock filter never stocked", reset);
        Assert.Contains("Stock filter other warehouse", reset);

        // Catalog browsing and item details still expose unstocked catalog entries/warehouses.
        Assert.Equal(5, (await queries.CatalogAsync("Stock filter", null)).Rows.Count);
        var neverId = (int)(await app.ScalarAsync("SELECT item_id FROM dbo.ITEM WHERE item_name='Stock filter never stocked'"))!;
        var details = await client.GetStringAsync($"/Catalog/Details/{neverId}");
        Assert.Contains("North Loop", details); Assert.Contains("West End", details);
        var balances = await queries.InventoryAsync(null, null, null, neverId, includeZeroStock: true);
        Assert.Equal(2, balances.Rows.Count); Assert.All(balances.Rows, row => Assert.Equal(0, row.OnHand));
        Assert.Empty(app.ServerErrors);
    }

    [SqlServerFact]
    public async Task FullyCheckedOutItemDisappearsAndReturnsAfterCheckIn()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<InventoryQueries>();
        var stock = scope.ServiceProvider.GetRequiredService<StockService>();
        Assert.True((await stock.RecordAsync(new(2, 1, 1, "CHECK OUT", 12, "Visibility regression"))).Success);
        Assert.Empty((await queries.InventoryAsync(null, null, 1, 1)).Rows);
        var elsewhere = await queries.InventoryAsync(null, null, null, 1);
        Assert.Equal(2, Assert.Single(elsewhere.Rows).LocationId);
        var details = await queries.InventoryAsync(null, null, null, 1, includeZeroStock: true);
        Assert.Contains(details.Rows, row => row.LocationId == 1 && row.OnHand == 0);
        Assert.True((await stock.RecordAsync(new(2, 1, 1, "CHECK IN", 1, "Visibility regression"))).Success);
        Assert.Equal(1, Assert.Single((await queries.InventoryAsync(null, null, 1, 1)).Rows).OnHand);
        Assert.Equal(2, (await queries.InventoryAsync(null, null, null, 1)).Rows.Count);
        Assert.Empty(app.ServerErrors);
    }
}
