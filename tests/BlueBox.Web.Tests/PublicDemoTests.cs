using System.Net;
using System.Text.RegularExpressions;
using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlueBox.Web.Tests;

public sealed class PublicDemoTests
{
    private const string DemoUsername = "avery.chen";
    private const string DemoPassword = "DEMO.PASS123321";
    private static TestApp DemoApp() => new() { PublicDemoUsername = DemoUsername, PublicDemoPassword = DemoPassword };

    [Theory]
    [InlineData(null, null)]
    [InlineData("avery.chen", null)]
    [InlineData(null, "display-only-password")]
    [InlineData(" ", "display-only-password")]
    public async Task DemoPanelIsHiddenUnlessBothSettingsArePresent(string? username, string? password)
    {
        await using var app = new TestApp { PublicDemoUsername = username, PublicDemoPassword = password };
        using var client = app.Browser();
        var html = await client.GetStringAsync("/Account/Login");
        Assert.DoesNotContain("id=\"demo-access\"", html);
        Assert.DoesNotContain("/js/demo-login.js", html);
        Assert.DoesNotContain("display-only-password", html);
        Assert.Contains("name=\"Username\"", html);
        Assert.Contains("__RequestVerificationToken", html);
    }

    [Fact]
    public async Task ConfiguredValuesAreRenderedAsEncodedDisplayText()
    {
        const string password = "display-\"<&password>";
        await using var app = new TestApp { PublicDemoUsername = "other.staff", PublicDemoPassword = password };
        using var client = app.Browser();
        var html = await client.GetStringAsync("/Account/Login");
        Assert.Equal("other.staff", Value(html, "demo-username"));
        Assert.Equal(password, Value(html, "demo-password"));
        Assert.DoesNotContain(password, html);
        Assert.DoesNotContain(DemoPassword, html);
    }

    [Fact]
    public async Task AnonymousDemoDisclosurePreservesTheNormalLoginFormAndSecurityHeaders()
    {
        await using var app = DemoApp(); using var client = app.Browser();
        var response = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        foreach (var id in new[] { "demo-username", "demo-password" })
        {
            var tag = InputTag(html, id);
            Assert.Contains("readonly", tag);
            Assert.DoesNotMatch(@"\sname\s*=", tag); // Displayed credentials are not extra submitted fields.
            Assert.NotEmpty(Value(html, id));
        }
        Assert.Equal(DemoUsername, Value(html, "demo-username"));
        Assert.Equal(DemoPassword, Value(html, "demo-password"));
        Assert.Contains("Try the demo", html);
        Assert.Contains("Changes are saved and visible to other visitors", html);
        Assert.Contains("name=\"Username\"", html);
        Assert.Contains("name=\"Password\"", html);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.Contains("/js/demo-login.js?v=", html);
        Assert.DoesNotContain("unsafe-inline", string.Join("", response.Headers.GetValues("Content-Security-Policy")));
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/js/demo-login.js")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Overview")).StatusCode);
    }

    [SqlServerFact]
    public async Task DisplayedDemoCredentialsUseNormalStaffLoginAndCannotAccessManagerTools()
    {
        await using var app = DemoApp(); await app.InitializeAsync(); using var client = app.Browser();
        var html = await client.GetStringAsync("/Account/Login");
        var username = Value(html, "demo-username"); var password = Value(html, "demo-password");
        await using var scope = app.Services.CreateAsyncScope();
        var employee = (await scope.ServiceProvider.GetRequiredService<EmployeeRepository>().FindByUsernameAsync(username))!;
        // Match the owner's explicit account update only in this disposable test database.
        Assert.True((await scope.ServiceProvider.GetRequiredService<AdministrationService>().SaveEmployeeAsync(1, employee.EmployeeId, new EmployeeInput
        {
            FirstName = employee.FirstName, LastName = employee.LastName, Username = username,
            Role = "STAFF", Status = "ACTIVE", Password = password
        })).Success);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Username"] = username, ["Password"] = password }))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await TestApp.LoginAsync(client, username, password)).StatusCode);
        foreach (var page in new[] { "/Overview", "/Inventory", "/Catalog", "/Activity" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(page)).StatusCode);
        foreach (var page in new[] { "/Admin/Items", "/Admin/Employees", "/Admin/Locations", "/Activity/Correct/1" })
        {
            var denied = await client.GetAsync(page);
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            Assert.Contains("AccessDenied", denied.Headers.Location!.OriginalString);
        }
        var token = await TestApp.TokenAsync(client, "/Inventory/Move");
        var movement = await client.PostAsync("/Inventory/Move", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.ItemId"] = "1", ["Input.LocationId"] = "1", ["Input.Direction"] = "CHECK IN", ["Input.Quantity"] = "1",
            ["Input.Notes"] = "Public demo regression", ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, movement.StatusCode);
        Assert.Equal(13, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=1 AND location_id=1"));
        Assert.Equal(employee.EmployeeId, await app.ScalarAsync("SELECT employee_id FROM dbo.TRANSACTION_LOG WHERE notes='Public demo regression'"));
        using var manager = app.Browser();
        Assert.Equal(HttpStatusCode.OK, (await TestApp.LoginAsync(manager, "jordan.lee", password)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await TestApp.LoginAsync(manager, "jordan.lee")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/Admin/Employees")).StatusCode);
    }

    [Fact]
    public async Task MovementLimitsArePerClientAddressAndRejectBeforeDatabaseAccess()
    {
        await using var app = new TestApp { KnownProxies = "192.0.2.10" };
        // Even unauthenticated/invalid submissions consume permits before authentication or SQL.
        for (var i = 0; i < 20; i++)
            Assert.Equal(302, (await RequestAsync(app, "192.0.2.10", "198.51.100.1", "POST", "/Inventory/Move")).Response.StatusCode);
        var rejected = await RequestAsync(app, "192.0.2.10", "198.51.100.1", "POST", "/Inventory/Move");
        Assert.Equal(429, rejected.Response.StatusCode);
        Assert.True(rejected.Response.Headers.ContainsKey("Retry-After"));
        Assert.Equal(302, (await RequestAsync(app, "192.0.2.10", "198.51.100.2", "POST", "/Inventory/Move")).Response.StatusCode);
        Assert.Equal(200, (await RequestAsync(app, "192.0.2.10", "198.51.100.1", "GET", "/Account/Login")).Response.StatusCode);
        Assert.Equal(302, (await RequestAsync(app, "192.0.2.10", "198.51.100.1", "GET", "/Inventory/Move")).Response.StatusCode);
        // Untrusted X-Forwarded-For values cannot select fresh buckets.
        for (var i = 0; i < 20; i++)
            Assert.Equal(302, (await RequestAsync(app, "192.0.2.99", $"198.51.100.{i + 1}", "POST", "/Inventory/Move")).Response.StatusCode);
        Assert.Equal(429, (await RequestAsync(app, "192.0.2.99", "198.51.100.200", "POST", "/Inventory/Move")).Response.StatusCode);
        Assert.Empty(app.ServerErrors);
    }

    [Fact]
    public async Task PageRefreshLimitKeepsStaticAssetsAndOtherAddressesAvailable()
    {
        await using var app = new TestApp { KnownProxies = "192.0.2.10" };
        // Cheap anonymous redirects exercise the same global limiter as database-backed pages.
        for (var i = 0; i < 300; i++)
            Assert.Equal(302, (await RequestAsync(app, "192.0.2.10", "198.51.100.1", "GET", "/Overview")).Response.StatusCode);
        var rejected = await RequestAsync(app, "192.0.2.10", "198.51.100.1", "GET", "/Account/Login");
        Assert.Equal(429, rejected.Response.StatusCode);
        Assert.True(rejected.Response.Headers.ContainsKey("Retry-After"));
        Assert.Equal(200, (await RequestAsync(app, "192.0.2.10", "198.51.100.1", "GET", "/css/site.css")).Response.StatusCode);
        Assert.Equal(200, (await RequestAsync(app, "192.0.2.10", "198.51.100.2", "GET", "/Account/Login")).Response.StatusCode);
        Assert.Empty(app.ServerErrors);
    }

    [SqlServerFact]
    public async Task TwentyFirstMovementIsRejectedWithoutChangingStockOrHistory()
    {
        await using var app = DemoApp(); await app.InitializeAsync(DemoPassword);
        using var client = app.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await TestApp.LoginAsync(client, DemoUsername, DemoPassword)).StatusCode);
        var token = await TestApp.TokenAsync(client, "/Inventory/Move");
        var initialHistory = (int)(await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG"))!;
        for (var i = 0; i < 20; i++)
            Assert.Equal(HttpStatusCode.Redirect, (await MoveAsync()).StatusCode);
        var rejected = await MoveAsync();
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal(32, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=1 AND location_id=1"));
        Assert.Equal(initialHistory + 20, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Inventory/Move")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Activity")).StatusCode);
        Assert.Empty(app.ServerErrors);

        Task<HttpResponseMessage> MoveAsync() => client.PostAsync("/Inventory/Move", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.ItemId"] = "1", ["Input.LocationId"] = "1", ["Input.Direction"] = "CHECK IN", ["Input.Quantity"] = "1",
            ["__RequestVerificationToken"] = token
        }));
    }

    private static Task<HttpContext> RequestAsync(TestApp app, string peer, string client, string method, string path) => app.Server.SendAsync(context =>
    {
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost");
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Headers["X-Forwarded-For"] = client;
    });

    private static string InputTag(string html, string id)
    {
        var match = Regex.Match(html, $"<input\\b(?=[^>]*\\bid=\"{Regex.Escape(id)}\")[^>]*>");
        Assert.True(match.Success, $"Missing displayed credential: {id}");
        return match.Value;
    }

    private static string Value(string html, string id) => WebUtility.HtmlDecode(Regex.Match(InputTag(html, id), "\\bvalue=\"([^\"]*)\"").Groups[1].Value);
}
