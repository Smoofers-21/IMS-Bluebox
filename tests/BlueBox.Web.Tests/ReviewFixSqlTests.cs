using System.Net;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlueBox.Web.Tests;

public sealed class ReviewFixSqlTests
{
    [SqlServerTheory]
    [InlineData("Input.ItemId")]
    [InlineData("Input.LocationId")]
    [InlineData("Input.Direction")]
    [InlineData("Input.Quantity")]
    public async Task IncompleteMovementPostChangesNeitherStockNorHistory(string missing)
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        using var client = app.Browser(); await TestApp.LoginAsync(client);
        var count = await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG");
        var token = await TestApp.TokenAsync(client, "/Inventory/Move");
        var values = new Dictionary<string, string>
        {
            ["Input.ItemId"] = "1",
            ["Input.LocationId"] = "1",
            ["Input.Direction"] = "CHECK IN",
            ["Input.Quantity"] = "5",
            ["__RequestVerificationToken"] = token
        };
        values.Remove(missing);
        var response = await client.PostAsync("/Inventory/Move", new FormUrlEncodedContent(values));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("field is required", await response.Content.ReadAsStringAsync());
        Assert.Equal(count, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG"));
        Assert.Equal(12, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=1 AND location_id=1"));
    }

    [SqlServerFact]
    public async Task MaximumSetupPasswordCanSignIn()
    {
        var password = new string('x', PasswordPolicy.MaximumLength);
        await using var app = new TestApp(); await app.InitializeAsync(password);
        using var client = app.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await TestApp.LoginAsync(client, password: password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Inventory")).StatusCode);
    }

    [SqlServerFact]
    public async Task ActivityPagesReachOlderHistoryWithoutDuplicatesAndPreserveFilters()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        // Test-only history fixture; isolated database, not the teacher's stock.
        await app.ExecuteAsync("""
            INSERT INTO dbo.TRANSACTION_LOG (employee_id,item_id,location_id,transaction_type,quantity_moved,transaction_datetime,notes)
            SELECT TOP (201) 2,1,1,'CHECK OUT',1,CONVERT(datetime2,'2026-01-01'),
                CONCAT('Pagination ', ROW_NUMBER() OVER (ORDER BY object_id))
            FROM sys.all_objects ORDER BY object_id;
            """);
        await using var scope = app.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<InventoryQueries>();
        var first = await queries.ActivityAsync("Moonlight", "CHECK OUT", 1, 1);
        var second = await queries.ActivityAsync("Moonlight", "CHECK OUT", 1, 2);
        var last = await queries.ActivityAsync("Moonlight", "CHECK OUT", 1, 3);
        Assert.Equal(100, first.Rows.Count); Assert.True(first.HasNextPage);
        Assert.Equal(100, second.Rows.Count); Assert.True(second.HasNextPage);
        Assert.Single(last.Rows); Assert.False(last.HasNextPage);
        var all = first.Rows.Concat(second.Rows).Concat(last.Rows).ToList();
        Assert.Equal(201, all.Select(r => r.Id).Distinct().Count());
        Assert.Equal(all.OrderByDescending(r => r.Id).Select(r => r.Id), all.Select(r => r.Id));
        Assert.Equal(await app.ScalarAsync("SELECT MIN(transaction_id) FROM dbo.TRANSACTION_LOG WHERE employee_id=2"), last.Rows[0].Id);

        using var client = app.Browser(); await TestApp.LoginAsync(client);
        var html = await client.GetStringAsync("/Activity?Search=Moonlight&Direction=CHECK%20OUT&LocationId=1&PageNumber=2");
        Assert.Contains("Page 2", html); Assert.Contains("Newer", html); Assert.Contains("Older", html);
        Assert.Contains("PageNumber=1", html); Assert.Contains("PageNumber=3", html);
        Assert.Contains("Search=Moonlight", html); Assert.Contains("LocationId=1", html);
        Assert.Contains("Direction=CHECK%20OUT", html);
        var finalHtml = await client.GetStringAsync("/Activity?Search=Moonlight&Direction=CHECK%20OUT&LocationId=1&PageNumber=3");
        Assert.DoesNotContain("rel=\"next\"", finalHtml);
        var invalidPage = await client.GetStringAsync("/Activity?PageNumber=-1");
        Assert.Contains("Page 1", invalidPage);
    }
}
