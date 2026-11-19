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
