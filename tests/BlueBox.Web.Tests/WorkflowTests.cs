using System.Net;
using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlueBox.Web.Tests;

public sealed class WorkflowTests
{
    [Fact]
    public async Task ErrorPageDoesNotNeedDatabaseValidationEvenWithASessionCookie()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(EmployeeCookieEvents.CreatePrincipal(new Employee
        { EmployeeId = 123, FirstName = "Test", LastName = "Employee" }), new AuthenticationProperties(), CookieAuthenticationDefaults.AuthenticationScheme);
        client.DefaultRequestHeaders.Add("Cookie", $"BlueBox.Session={options.TicketDataFormat.Protect(ticket)}");
        var response = await client.GetAsync("/Error");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Your request could not be completed", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AnonymousRoutesHeadersAndAntiForgeryFollowTheTemplate()
    {
        await using var app = new TestApp(); using var client = app.Browser();
        Assert.Equal("/bluebox", (await client.GetAsync("/")).Headers.Location?.OriginalString);
        Assert.Equal("/Account/Login", (await client.GetAsync("/bluebox")).Headers.Location?.OriginalString);
        foreach (var route in new[] { "/Inventory", "/Catalog", "/Inventory/Move", "/Activity", "/api/inventory/balance?itemId=1&locationId=1" })
            Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(route)).StatusCode);
        var login = await client.GetAsync("/Account/Login"); Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains("nosniff", login.Headers.GetValues("X-Content-Type-Options"));
        Assert.Contains("DENY", login.Headers.GetValues("X-Frame-Options"));
        Assert.DoesNotContain("unsafe-inline", string.Join("", login.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Username"] = "avery.chen", ["Password"] = TestApp.Password }))).StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, (await client.GetAsync("/Error")).StatusCode);
    }

    [Theory]
    [InlineData("STAFF", false)]
    [InlineData("MANAGER", true)]
    public async Task ManagerPolicyEnforcesTheCurrentRole(string role, bool expected)
    {
        await using var app = new TestApp(); await using var scope = app.Services.CreateAsyncScope();
        var employee = new Employee { EmployeeId = 1, FirstName = "Test", LastName = "Employee", AccessRole = role };
        var result = await scope.ServiceProvider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(EmployeeCookieEvents.CreatePrincipal(employee), null, "Manager");
        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public void ConnectionBuilderPreservesSpecialCharactersAndVerifiesCertificatesByDefault()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Server"] = "localhost",
            ["Database:Name"] = "Example",
            ["Database:Username"] = "example.user",
            ["Database:Password"] = "a;Password=not-a-separate-field"
        }).Build();
        var actual = new SqlConnectionStringBuilder(new SqlDatabase(config, NullLogger<SqlDatabase>.Instance).GetConnectionString());
        Assert.Equal("a;Password=not-a-separate-field", actual.Password); Assert.Equal("example.user", actual.UserID); Assert.False(actual.TrustServerCertificate);
    }

    [Fact]
    public async Task InvalidInputIsRejectedBeforeAnyConnectionIsOpened()
    {
        var database = new SqlDatabase(new ConfigurationBuilder().Build(), NullLogger<SqlDatabase>.Instance);
        var stock = new StockService(database);
        Assert.False((await stock.RecordAsync(new(1, 1, 1, "CHECK IN", 0, null))).Success);
        Assert.False((await stock.RecordAsync(new(1, 1, 1, "OTHER", 1, null))).Success);
        Assert.False((await stock.RecordAsync(new(1, 1, 1, "CHECK OUT", 1, new string('x', 256)))).Success);
        Assert.Throws<InvalidOperationException>(() => database.GetConnectionString());
    }

    [SqlServerFact]
    public async Task EmployeeCanSignInBrowseMoveStockAndSeeAttributedHistory()
    {
        await using var app = new TestApp(); await app.InitializeAsync(); using var client = app.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await TestApp.LoginAsync(client)).StatusCode);
        foreach (var page in new[] { "/Inventory", "/Catalog", "/Catalog/Details/1", "/Inventory/Move", "/Activity" })
        {
            var response = await client.GetAsync(page); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Avery Chen", await response.Content.ReadAsStringAsync());
        }
        Assert.Contains("Moonlight Signal", await client.GetStringAsync("/Catalog?Search=Moonlight&Type=MOVIE"));
        Assert.DoesNotContain("Neon Circuit", await client.GetStringAsync("/Catalog?Search=Moonlight&Type=MOVIE"));
        var token = await TestApp.TokenAsync(client, "/Inventory/Move?itemId=1&locationId=1");
        var post = await client.PostAsync("/Inventory/Move", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.ItemId"] = "1",
            ["Input.LocationId"] = "1",
            ["Input.Direction"] = "CHECK IN",
            ["Input.Quantity"] = "10",
            ["Input.Notes"] = "Workflow test shipment",
            ["EmployeeId"] = "9999",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Contains("Workflow test shipment", await client.GetStringAsync("/Activity"));
        Assert.Equal(2, await app.ScalarAsync("SELECT TOP (1) employee_id FROM dbo.TRANSACTION_LOG ORDER BY transaction_id DESC"));
        Assert.Equal(22, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=1 AND location_id=1"));
        Assert.Contains("22", await client.GetStringAsync("/api/inventory/balance?itemId=1&locationId=1"));
        var logout = await TestApp.TokenAsync(client, "/Inventory");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = logout }))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Activity")).StatusCode);
    }

    [SqlServerTheory]
    [InlineData("avery.chen", "wrong-password")]
    [InlineData("sam.rivera", TestApp.Password)]
    [InlineData("unknown", TestApp.Password)]
    public async Task InvalidAndInactiveAccountsCannotSignIn(string username, string password)
    {
        await using var app = new TestApp(); await app.InitializeAsync(); using var client = app.Browser();
        var response = await TestApp.LoginAsync(client, username, password); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Unable to sign in", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Inventory")).StatusCode);
    }

    [SqlServerFact]
    public async Task StatusAndRoleChangesTakeEffectOnExistingSessions()
    {
        await using var app = new TestApp(); await app.InitializeAsync(); using var client = app.Browser(); await TestApp.LoginAsync(client);
        await app.ExecuteAsync("UPDATE dbo.EMPLOYEE SET access_role='MANAGER' WHERE username='avery.chen'");
        Assert.Contains("<small>Manager</small>", await client.GetStringAsync("/Inventory"));
        await app.ExecuteAsync("UPDATE dbo.EMPLOYEE SET employment_status='TERMINATED' WHERE username='avery.chen'");
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Inventory")).StatusCode);
    }

    [SqlServerTheory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    [InlineData("1000")]
    public async Task InvalidPostedQuantityChangesNothing(string quantity)
    {
        await using var app = new TestApp(); await app.InitializeAsync(); using var client = app.Browser(); await TestApp.LoginAsync(client);
        var count = await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG");
        var token = await TestApp.TokenAsync(client, "/Inventory/Move");
        var response = await client.PostAsync("/Inventory/Move", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.ItemId"] = "1",
            ["Input.LocationId"] = "1",
            ["Input.Direction"] = "CHECK OUT",
            ["Input.Quantity"] = quantity,
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(count, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG"));
        Assert.Equal(12, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=1 AND location_id=1"));
    }

    [SqlServerFact]
    public async Task DemoAndManagerSetupRefuseToOverwriteExistingData()
    {
        await using var app = new TestApp(); await app.InitializeAsync(); await using var scope = app.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<SqlDatabase>(); var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Employee>>();
        var count = await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG");
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoSeeder.SeedAsync(database, hasher, TestApp.Password));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ManagerBootstrap.CreateAsync(database, hasher, "another.manager", TestApp.Password));
        Assert.Equal(count, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG")); Assert.Equal(3, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.EMPLOYEE"));
    }

    [SqlServerFact]
    public async Task ReturnUrlCannotRedirectToAnExternalSite()
    {
        await using var app = new TestApp(); await app.InitializeAsync(); using var client = app.Browser();
        Assert.Equal("/Overview", (await TestApp.LoginAsync(client, returnUrl: "https://example.com")).Headers.Location?.OriginalString);
    }
}
