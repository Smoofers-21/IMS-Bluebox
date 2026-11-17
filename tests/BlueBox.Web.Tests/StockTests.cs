using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlueBox.Web.Tests;

public sealed class StockTests
{
    [Fact]
    public async Task CheckInAndOutPreserveBalanceHistoryAndEmployee()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BlueBoxDbContext>();
        var stock = scope.ServiceProvider.GetRequiredService<StockService>();
        var row = await db.Inventory.AsNoTracking().OrderBy(i => i.ItemId).ThenBy(i => i.LocationId).FirstAsync();
        var employee = await db.Employees.FirstAsync(e => e.Username == "avery.chen");
        var start = await db.Transactions.CountAsync();
        var before = row.LastUpdatedAt;
        var added = await stock.RecordAsync(new(employee.EmployeeId, row.ItemId, row.LocationId, "CHECK IN", 10, "Shipment"));
        Assert.True(added.Success); Assert.Equal(row.QuantityOnHand + 10, added.Balance);
        var removed = await stock.RecordAsync(new(employee.EmployeeId, row.ItemId, row.LocationId, "CHECK OUT", 4, null));
        Assert.True(removed.Success); Assert.Equal(row.QuantityOnHand + 6, removed.Balance);
        var current = await db.Inventory.AsNoTracking().SingleAsync(i => i.ItemId == row.ItemId && i.LocationId == row.LocationId);
        Assert.Equal(row.QuantityOnHand + 6, current.QuantityOnHand); Assert.True(current.LastUpdatedAt >= before);
        Assert.Equal(start + 2, await db.Transactions.CountAsync());
        Assert.All(await db.Transactions.OrderByDescending(t => t.TransactionId).Take(2).ToListAsync(), t => Assert.Equal(employee.EmployeeId, t.EmployeeId));
    }

    [Theory]
    [InlineData("CHECK OUT", 1000)]
    [InlineData("CHECK IN", 0)]
    [InlineData("CHECK IN", -2)]
    [InlineData("OTHER", 1)]
    [InlineData("CHECK IN", int.MaxValue)]
    public async Task InvalidMovementsChangeNeitherStockNorHistory(string direction, int quantity)
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BlueBoxDbContext>();
        var row = await db.Inventory.AsNoTracking().FirstAsync();
        var employee = await db.Employees.FirstAsync(e => e.Username == "avery.chen");
        var start = await db.Transactions.CountAsync();
        var result = await scope.ServiceProvider.GetRequiredService<StockService>().RecordAsync(new(employee.EmployeeId, row.ItemId, row.LocationId, direction, quantity, null));
        Assert.False(result.Success);
        Assert.Equal(row.QuantityOnHand, await db.Inventory.Where(i => i.ItemId == row.ItemId && i.LocationId == row.LocationId).Select(i => i.QuantityOnHand).SingleAsync());
        Assert.Equal(start, await db.Transactions.CountAsync());
    }

    [Fact]
    public async Task FirstCheckInCreatesOneBalanceAndExactCheckOutReachesZero()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<BlueBoxDbContext>();
        var item = new Item { ItemName = "New title", MediaFormat = "DVD" }; db.Items.Add(item); await db.SaveChangesAsync();
        var location = await db.Locations.FirstAsync(); var employee = await db.Employees.FirstAsync();
        var stock = scope.ServiceProvider.GetRequiredService<StockService>();
        Assert.True((await stock.RecordAsync(new(employee.EmployeeId, item.ItemId, location.LocationId, "CHECK IN", 3, null))).Success);
        Assert.True((await stock.RecordAsync(new(employee.EmployeeId, item.ItemId, location.LocationId, "CHECK OUT", 3, null))).Success);
        Assert.Equal(0, (await db.Inventory.AsNoTracking().SingleAsync(i => i.ItemId == item.ItemId)).QuantityOnHand);
        Assert.Equal(2, await db.Transactions.CountAsync(t => t.ItemId == item.ItemId));
    }

    [Fact]
    public async Task HistoryFailureRollsBackAnAlreadyUpdatedBalance()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<BlueBoxDbContext>();
        var row = await db.Inventory.AsNoTracking().FirstAsync(); var employee = await db.Employees.FirstAsync();
        var count = await db.Transactions.CountAsync(); app.Failures.FailHistory = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<StockService>().RecordAsync(new(employee.EmployeeId, row.ItemId, row.LocationId, "CHECK OUT", 1, null)));
        await using var verification = app.Services.CreateAsyncScope(); var fresh = verification.ServiceProvider.GetRequiredService<BlueBoxDbContext>();
        Assert.Equal(row.QuantityOnHand, await fresh.Inventory.Where(i => i.ItemId == row.ItemId && i.LocationId == row.LocationId).Select(i => i.QuantityOnHand).SingleAsync());
        Assert.Equal(count, await fresh.Transactions.CountAsync());
    }

    [Theory]
    [InlineData("employee")]
    [InlineData("item")]
    [InlineData("location")]
    public async Task InactiveRecordsCannotMoveStock(string record)
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<BlueBoxDbContext>();
        var row = await db.Inventory.FirstAsync(); var employee = await db.Employees.FirstAsync();
        if (record == "employee") employee.EmploymentStatus = "INACTIVE";
        if (record == "item") (await db.Items.FindAsync(row.ItemId))!.ItemStatus = "ARCHIVED";
        if (record == "location") (await db.Locations.FindAsync(row.LocationId))!.LocationStatus = "INACTIVE";
        await db.SaveChangesAsync(); var count = await db.Transactions.CountAsync();
        Assert.False((await scope.ServiceProvider.GetRequiredService<StockService>().RecordAsync(new(employee.EmployeeId, row.ItemId, row.LocationId, "CHECK IN", 1, null))).Success);
        Assert.Equal(count, await db.Transactions.CountAsync());
    }

    [Fact]
    public async Task CompetingCheckOutsCannotOversell()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        int itemId, locationId, employeeId, quantity, count;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlueBoxDbContext>(); var row = await db.Inventory.FirstAsync();
            itemId = row.ItemId; locationId = row.LocationId; quantity = row.QuantityOnHand; employeeId = (await db.Employees.FirstAsync()).EmployeeId; count = await db.Transactions.CountAsync();
        }
        async Task<MovementResult> Remove()
        {
            await using var scope = app.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<StockService>().RecordAsync(new(employeeId, itemId, locationId, "CHECK OUT", quantity, null));
        }
        var results = await Task.WhenAll(Task.Run(Remove), Task.Run(Remove));
        Assert.Single(results, r => r.Success);
        await using var verify = app.Services.CreateAsyncScope(); var fresh = verify.ServiceProvider.GetRequiredService<BlueBoxDbContext>();
        Assert.Equal(0, await fresh.Inventory.Where(i => i.ItemId == itemId && i.LocationId == locationId).Select(i => i.QuantityOnHand).SingleAsync());
        Assert.Equal(count + 1, await fresh.Transactions.CountAsync());
    }
}
