using BlueBox.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlueBox.Web.Tests;

public sealed class StockTests
{
    [SqlServerFact]
    public async Task CheckInAndOutPreserveBalanceAndAttributedHistory()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        await using var scope = app.Services.CreateAsyncScope(); var stock = scope.ServiceProvider.GetRequiredService<StockService>();
        Assert.True((await stock.RecordAsync(new(2, 1, 1, "CHECK IN", 10, "Shipment"))).Success);
        var removed = await stock.RecordAsync(new(2, 1, 1, "CHECK OUT", 4, null)); Assert.True(removed.Success); Assert.Equal(18, removed.Balance);
        Assert.Equal(18, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=1 AND location_id=1"));
        Assert.Equal(2, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG WHERE employee_id=2"));
        Assert.Equal(18, await app.ScalarAsync("SELECT SUM(CASE WHEN transaction_type='CHECK IN' THEN quantity_moved ELSE -quantity_moved END) FROM dbo.TRANSACTION_LOG WHERE item_id=1 AND location_id=1"));
    }

    [SqlServerTheory]
    [InlineData("CHECK OUT", 1000)]
    [InlineData("CHECK IN", 0)]
    [InlineData("CHECK IN", -2)]
    [InlineData("OTHER", 1)]
    [InlineData("CHECK IN", int.MaxValue)]
    public async Task InvalidMovementsChangeNeitherStockNorHistory(string direction, int quantity)
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        var count = await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG");
        await using var scope = app.Services.CreateAsyncScope(); var result = await scope.ServiceProvider.GetRequiredService<StockService>().RecordAsync(new(2, 1, 1, direction, quantity, null));
        Assert.False(result.Success); Assert.Equal(12, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=1 AND location_id=1"));
        Assert.Equal(count, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG"));
    }

    [SqlServerFact]
    public async Task FirstCheckInAndExactCheckOutWorkWithoutAnExistingBalance()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        await app.ExecuteAsync("INSERT INTO dbo.ITEM (item_name,item_type,media_format,item_status) VALUES ('New title','MOVIE','DVD','ACTIVE')");
        await using var scope = app.Services.CreateAsyncScope(); var stock = scope.ServiceProvider.GetRequiredService<StockService>();
        Assert.True((await stock.RecordAsync(new(2, 7, 1, "CHECK IN", 3, null))).Success);
        Assert.True((await stock.RecordAsync(new(2, 7, 1, "CHECK OUT", 3, null))).Success);
        Assert.Equal(0, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=7 AND location_id=1"));
        Assert.Equal(2, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG WHERE item_id=7"));
    }

    [SqlServerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HistoryFailureRollsBackBalanceUpdatesAndFirstBalanceCreation(bool firstBalance)
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        if (firstBalance) await app.ExecuteAsync("INSERT INTO dbo.ITEM (item_name,item_type,media_format,item_status) VALUES ('New title','MOVIE','DVD','ACTIVE')");
        var count = await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG");
        await app.ExecuteAsync("CREATE TRIGGER dbo.FailHistoryForTest ON dbo.TRANSACTION_LOG AFTER INSERT AS BEGIN; THROW 51000, 'Injected history failure', 1; END;");
        await using var scope = app.Services.CreateAsyncScope();
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => scope.ServiceProvider.GetRequiredService<StockService>().RecordAsync(new(2, firstBalance ? 7 : 1, 1, "CHECK IN", 1, null)));
        Assert.Equal(count, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG"));
        Assert.Equal(firstBalance ? 0 : 12, await app.ScalarAsync(firstBalance ? "SELECT COUNT(*) FROM dbo.CURRENT_INVENTORY WHERE item_id=7" : "SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=1 AND location_id=1"));
    }

    [SqlServerTheory]
    [InlineData("UPDATE dbo.EMPLOYEE SET employment_status='INACTIVE' WHERE employee_id=2")]
    [InlineData("UPDATE dbo.ITEM SET item_status='ARCHIVED' WHERE item_id=1")]
    [InlineData("UPDATE dbo.LOCATION SET location_status='INACTIVE' WHERE location_id=1")]
    public async Task InactiveRecordsCannotMoveStock(string change)
    {
        await using var app = new TestApp(); await app.InitializeAsync(); await app.ExecuteAsync(change);
        var count = await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG");
        await using var scope = app.Services.CreateAsyncScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<StockService>().RecordAsync(new(2, 1, 1, "CHECK IN", 1, null))).Success);
        Assert.Equal(count, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG"));
    }

    [SqlServerFact]
    public async Task CompetingCheckOutsCannotOversell()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        var count = await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG");
        async Task<MovementResult> Remove()
        {
            await using var scope = app.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<StockService>().RecordAsync(new(2, 1, 1, "CHECK OUT", 12, null));
        }
        var results = await Task.WhenAll(Task.Run(Remove), Task.Run(Remove)); Assert.Single(results, r => r.Success);
        Assert.Equal(0, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=1 AND location_id=1"));
        Assert.Equal(Convert.ToInt32(count) + 1, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG"));
    }

    [SqlServerFact]
    public async Task SimultaneousFirstCheckInsCreateOneBalanceAndBothHistoryEntries()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        await app.ExecuteAsync("INSERT INTO dbo.ITEM (item_name,item_type,media_format,item_status) VALUES ('New title','MOVIE','DVD','ACTIVE')");
        async Task<MovementResult> Add()
        {
            await using var scope = app.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<StockService>().RecordAsync(new(2, 7, 1, "CHECK IN", 2, null));
        }
        Assert.All(await Task.WhenAll(Task.Run(Add), Task.Run(Add)), r => Assert.True(r.Success));
        Assert.Equal(4, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=7 AND location_id=1"));
        Assert.Equal(2, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG WHERE item_id=7"));
    }
}
