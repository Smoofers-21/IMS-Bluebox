using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlueBox.Web.Tests;

public sealed class AzureSchemaTests
{
    [SqlServerFact]
    public async Task FreshInstallerSupportsStockAndRejectsRerunsWithoutChangingData()
    {
        await using var app = new TestApp();
        await app.InitializeAsync(schemaResource: "BlueBox.AzureSchema.sql");
        await AssertIndexesAsync(app);
        await using var scope = app.Services.CreateAsyncScope();
        var stock = scope.ServiceProvider.GetRequiredService<StockService>();
        Assert.True((await stock.RecordAsync(new(2, 1, 1, "CHECK IN", 3, "Portfolio installer regression"))).Success);
        Assert.Equal(15, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=1 AND location_id=1"));
        var before = await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG");
        var databaseName = (string)(await app.ScalarAsync("SELECT DB_NAME()"))!;
        var script = (await ReadInstallerAsync()).Replace("N'YourAzureDatabaseName';", $"N'{databaseName}';", StringComparison.Ordinal);
        var error = await Assert.ThrowsAsync<SqlException>(() => app.ExecuteAsync(script));
        Assert.Equal(51002, error.Number);
        Assert.Equal(before, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG"));
        Assert.Equal(15, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=1 AND location_id=1"));
    }

    [SqlServerFact]
    public async Task IndexUpgradePreservesDataSupportsCorrectionsAndCanBeRerun()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        var before = await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG");
        using var reader = new StreamReader(typeof(TestApp).Assembly.GetManifestResourceStream("BlueBox.AzureIndexes.sql")!);
        var databaseName = (string)(await app.ScalarAsync("SELECT DB_NAME()"))!;
        var script = (await reader.ReadToEndAsync()).Replace("N'YourAzureDatabaseName';", $"N'{databaseName}';", StringComparison.Ordinal);
        await app.ExecuteAsync(script);
        await app.ExecuteAsync(script);
        await AssertIndexesAsync(app);
        Assert.Equal(before, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG"));
        Assert.Equal(12, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=1 AND location_id=1"));
        var wrongTarget = script.Replace($"N'{databaseName}';", "N'WrongPortfolioDatabase';", StringComparison.Ordinal);
        Assert.Equal(51000, (await Assert.ThrowsAsync<SqlException>(() => app.ExecuteAsync(wrongTarget))).Number);
        await using var scope = app.Services.CreateAsyncScope();
        var stock = scope.ServiceProvider.GetRequiredService<StockService>();
        Assert.True((await stock.RecordAsync(new(2, 1, 1, "CHECK IN", 3, "Indexed movement"))).Success);
        var movement = (long)(await app.ScalarAsync("SELECT MAX(transaction_id) FROM dbo.TRANSACTION_LOG"))!;
        var corrections = await Task.WhenAll(stock.CorrectAsync(1, movement, "Indexed correction"), stock.CorrectAsync(1, movement, "Duplicate correction"));
        Assert.Single(corrections, result => result.Success);
        Assert.Equal(1, await app.ScalarAsync($"SELECT COUNT(*) FROM dbo.TRANSACTION_LOG WHERE corrects_transaction_id={movement}"));
        Assert.Equal(12, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=1 AND location_id=1"));
        var correction = (long)(await app.ScalarAsync($"SELECT transaction_id FROM dbo.TRANSACTION_LOG WHERE corrects_transaction_id={movement}"))!;
        Assert.True((await stock.CorrectAsync(1, correction, "Reverse indexed correction")).Success);
        Assert.Equal(15, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=1 AND location_id=1"));
    }

    private static async Task AssertIndexesAsync(TestApp app)
    {
        Assert.Equal(4, await app.ScalarAsync("""
            SELECT COUNT(*) FROM sys.indexes AS i
            INNER JOIN (VALUES
                (OBJECT_ID(N'dbo.TRANSACTION_LOG'), N'IX_TRANSACTION_LOG_corrects_transaction_id', N'corrects_transaction_id'),
                (OBJECT_ID(N'dbo.TRANSACTION_LOG'), N'IX_TRANSACTION_LOG_item_id', N'item_id'),
                (OBJECT_ID(N'dbo.TRANSACTION_LOG'), N'IX_TRANSACTION_LOG_location_id', N'location_id'),
                (OBJECT_ID(N'dbo.CURRENT_INVENTORY'), N'IX_CURRENT_INVENTORY_location', N'location_id')
            ) AS expected(object_id, name, key_column) ON i.object_id=expected.object_id AND i.name=expected.name
            INNER JOIN sys.index_columns AS ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1
            INNER JOIN sys.columns AS c ON c.object_id=ic.object_id AND c.column_id=ic.column_id AND c.name=expected.key_column
            WHERE i.is_disabled=0 AND i.is_unique=0;
            """));
        Assert.Equal(1, await app.ScalarAsync("""
            SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TRANSACTION_LOG')
                AND name=N'IX_TRANSACTION_LOG_corrects_transaction_id' AND has_filter=1
                AND filter_definition LIKE N'%corrects_transaction_id%IS NOT NULL%';
            """));
        Assert.Equal(1, await app.ScalarAsync("""
            SELECT COUNT(*) FROM sys.indexes AS i
            INNER JOIN sys.index_columns AS ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.is_included_column=1
            INNER JOIN sys.columns AS c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE i.object_id=OBJECT_ID(N'dbo.CURRENT_INVENTORY') AND i.name=N'IX_CURRENT_INVENTORY_location' AND c.name=N'quantity_on_hand';
            """));
    }

    [SqlServerFact]
    public async Task WrongTargetIsRejectedBeforeChangingExistingSchemaOrData()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        var before = await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG");
        var script = (await ReadInstallerAsync()).Replace("N'YourAzureDatabaseName';", "N'WrongPortfolioDatabase';", StringComparison.Ordinal);
        var error = await Assert.ThrowsAsync<SqlException>(() => app.ExecuteAsync(script));
        Assert.Equal(51000, error.Number);
        Assert.Equal(before, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG"));
        Assert.Equal(5, await app.ScalarAsync("SELECT COUNT(*) FROM sys.tables WHERE name IN ('EMPLOYEE','ITEM','LOCATION','CURRENT_INVENTORY','TRANSACTION_LOG')"));
    }

    private static async Task<string> ReadInstallerAsync()
    {
        using var reader = new StreamReader(typeof(TestApp).Assembly.GetManifestResourceStream("BlueBox.AzureSchema.sql")!);
        return await reader.ReadToEndAsync();
    }
}
