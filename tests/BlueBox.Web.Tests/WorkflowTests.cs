using System.Net;
using BlueBox.Web.Data;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlueBox.Web.Tests;

public sealed class WorkflowTests
{
    [Fact]
    public async Task EmployeeCanSignInBrowseMoveStockAndSeeAttributedHistory()
    {
        await using var app = new TestApp(); await app.InitializeAsync(); using var client = app.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Inventory")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await TestApp.LoginAsync(client)).StatusCode);
        foreach (var page in new[] { "/Inventory", "/Catalog", "/Inventory/Move", "/Activity" })
        {
            var response = await client.GetAsync(page); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Avery Chen", await response.Content.ReadAsStringAsync());
        }
        int itemId, locationId, employeeId, balance;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlueBoxDbContext>(); var row = await db.Inventory.FirstAsync();
            itemId = row.ItemId; locationId = row.LocationId; balance = row.QuantityOnHand;
            employeeId = (await db.Employees.SingleAsync(e => e.Username == "avery.chen")).EmployeeId;
        }
        var details = await client.GetAsync($"/Catalog/Details/{itemId}"); Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var token = await TestApp.TokenAsync(client, $"/Inventory/Move?itemId={itemId}&locationId={locationId}");
        var responsePost = await client.PostAsync("/Inventory/Move", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.ItemId"] = itemId.ToString(),
            ["Input.LocationId"] = locationId.ToString(),
            ["Input.Direction"] = "CHECK IN",
            ["Input.Quantity"] = "10",
            ["Input.Notes"] = "Workflow test shipment",
            ["EmployeeId"] = "9999",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, responsePost.StatusCode);
        Assert.Contains("Workflow test shipment", await client.GetStringAsync("/Activity"));
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlueBoxDbContext>();
            var last = await db.Transactions.OrderByDescending(t => t.TransactionId).FirstAsync();
            Assert.Equal(employeeId, last.EmployeeId);
            Assert.Equal(balance + 10, await db.Inventory.Where(i => i.ItemId == itemId && i.LocationId == locationId).Select(i => i.QuantityOnHand).SingleAsync());
        }
        var logoutToken = await TestApp.TokenAsync(client, "/Inventory");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = logoutToken }))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Activity")).StatusCode);
    }

    [Theory]
    [InlineData("avery.chen", "wrong-password")]
    [InlineData("sam.rivera", TestApp.Password)]
    [InlineData("unknown", TestApp.Password)]
    public async Task InvalidAndInactiveAccountsCannotSignIn(string username, string password)
    {
        await using var app = new TestApp(); await app.InitializeAsync(); using var client = app.Browser();
        var response = await TestApp.LoginAsync(client, username, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Unable to sign in", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Inventory")).StatusCode);
    }

    [Fact]
    public async Task DeactivatedEmployeeLosesAnExistingSession()
    {
        await using var app = new TestApp(); await app.InitializeAsync(); using var client = app.Browser();
        await TestApp.LoginAsync(client);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlueBoxDbContext>();
            (await db.Employees.SingleAsync(e => e.Username == "avery.chen")).EmploymentStatus = "TERMINATED";
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Inventory")).StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    [InlineData("1000")]
    public async Task InvalidPostedQuantityChangesNothing(string quantity)
    {
        await using var app = new TestApp(); await app.InitializeAsync(); using var client = app.Browser();
        await TestApp.LoginAsync(client);
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<BlueBoxDbContext>();
        var row = await db.Inventory.AsNoTracking().FirstAsync(); var count = await db.Transactions.CountAsync();
        var token = await TestApp.TokenAsync(client, "/Inventory/Move");
        var response = await client.PostAsync("/Inventory/Move", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.ItemId"] = row.ItemId.ToString(),
            ["Input.LocationId"] = row.LocationId.ToString(),
            ["Input.Direction"] = "CHECK OUT",
            ["Input.Quantity"] = quantity,
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(count, await db.Transactions.CountAsync());
        Assert.Equal(row.QuantityOnHand, await db.Inventory.Where(i => i.ItemId == row.ItemId && i.LocationId == row.LocationId).Select(i => i.QuantityOnHand).SingleAsync());
    }

    [Fact]
    public async Task AntiForgeryAndLocalRedirectAreEnforced()
    {
        await using var app = new TestApp(); await app.InitializeAsync(); using var client = app.Browser();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Username"] = "avery.chen", ["Password"] = TestApp.Password }))).StatusCode);
        var response = await TestApp.LoginAsync(client, returnUrl: "https://example.com");
        Assert.Equal("/Inventory", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ManagerPolicyRejectsStaffAndAcceptsManager()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<BlueBoxDbContext>();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        foreach (var username in new[] { "avery.chen", "jordan.lee" })
        {
            var employee = await db.Employees.SingleAsync(e => e.Username == username);
            var result = await authorization.AuthorizeAsync(EmployeeCookieEvents.CreatePrincipal(employee), null, "Manager");
            Assert.Equal(username == "jordan.lee", result.Succeeded);
        }
    }

    [Fact]
    public async Task DemoSeederRefusesToOverwriteExistingData()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<BlueBoxDbContext>();
        var count = await db.Transactions.CountAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<BlueBox.Web.Models.Employee>>(), TestApp.Password));
        Assert.Equal(count, await db.Transactions.CountAsync());
    }

    [Fact]
    public async Task ManagerBootstrapDoesNotAddAnAccountWhenAManagerExists()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<BlueBoxDbContext>();
        var count = await db.Employees.CountAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => ManagerBootstrap.CreateAsync(db,
            scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<BlueBox.Web.Models.Employee>>(), "another.manager", TestApp.Password));
        Assert.Equal(count, await db.Employees.CountAsync());
    }

    [Fact]
    public void SqlServerReadQueriesTranslateWithoutConnecting()
    {
        using var db = new BlueBoxDbContext(new DbContextOptionsBuilder<BlueBoxDbContext>()
            .UseSqlServer("Server=localhost;Database=BlueBoxQueryCompilation;Integrated Security=True").Options);
        var queries = new InventoryQueries(db);
        Assert.Contains("ITEM", queries.Catalog("Moonlight", "MOVIE").OrderBy(i => i.Name).Take(100).ToQueryString());
        Assert.Contains("CURRENT_INVENTORY", queries.Inventory("Moonlight", "MOVIE", 1).Where(i => i.ItemId == 1).OrderBy(i => i.Name).Take(100).ToQueryString());
        Assert.Contains("TRANSACTION_LOG", queries.Activity("Moonlight", "CHECK IN", 1).OrderByDescending(i => i.Time).Take(100).ToQueryString());
    }
}
