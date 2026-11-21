using System.Net;
using System.Text.RegularExpressions;
using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlueBox.Web.Tests;

public sealed class PublicDemoTests
{
    [Fact]
    public async Task AnonymousDemoDisclosurePreservesTheNormalLoginFormAndSecurityHeaders()
    {
        await using var app = new TestApp(); using var client = app.Browser();
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
        await using var app = new TestApp(); await app.InitializeAsync(); using var client = app.Browser();
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

    private static string InputTag(string html, string id)
    {
        var match = Regex.Match(html, $"<input\\b(?=[^>]*\\bid=\"{Regex.Escape(id)}\")[^>]*>");
        Assert.True(match.Success, $"Missing displayed credential: {id}");
        return match.Value;
    }

    private static string Value(string html, string id) => WebUtility.HtmlDecode(Regex.Match(InputTag(html, id), "\\bvalue=\"([^\"]*)\"").Groups[1].Value);
}
