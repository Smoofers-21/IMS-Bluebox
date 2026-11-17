using System.Data;
using System.Net;
using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlueBox.Web.Tests;

public sealed class ExternalReviewTests
{
    private static ItemInput Item(string status = "ACTIVE") => new() { Name = "Review edition", Type = "MOVIE", Format = "DVD", Status = status };
    private static EmployeeInput Staff(string username = "avery.chen", string? password = null) => new()
    { FirstName = "Avery", LastName = "Chen", Username = username, Role = "STAFF", Status = "ACTIVE", Password = password };

    [Fact]
    public void TimeZoneFallbackUsesWindowsIdThenClearlyLabeledUtc()
    {
        var attempts = new List<string>();
        var central = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
        Assert.Same(central, DisplayTime.ResolveZone(id => { attempts.Add(id); return id == "Central Standard Time" ? central : null; }));
        Assert.Equal(new[] { "America/Chicago", "Central Standard Time" }, attempts);
        var fallback = DisplayTime.ResolveZone(_ => null);
        Assert.Equal(TimeZoneInfo.Utc, fallback);
        Assert.Equal("Jul 15, 2026 · 12:00 UTC", DisplayTime.Format(new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc), fallback));
    }

    [SqlServerTheory]
    [InlineData("password")]
    [InlineData("username")]
    public async Task CredentialChangesRevokeExistingCookies(string change)
    {
        await using var app = new TestApp(); await app.InitializeAsync(); using var oldSession = app.Browser();
        await TestApp.LoginAsync(oldSession);
        await using var scope = app.Services.CreateAsyncScope();
        var input = change == "password" ? Staff(password: "Replacement-password-42!") : Staff("renamed.staff");
        Assert.True((await scope.ServiceProvider.GetRequiredService<AdministrationService>().SaveEmployeeAsync(1, 2, input)).Success);
        var denied = await oldSession.GetAsync("/Inventory");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("/Account/Login", denied.Headers.Location!.OriginalString);
        using var fresh = app.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await TestApp.LoginAsync(fresh, input.Username, input.Password ?? TestApp.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync("/Inventory")).StatusCode);
    }

    [SqlServerFact]
    public async Task DisplayNameChangesDoNotRevokeUnchangedCredentials()
    {
        await using var app = new TestApp(); await app.InitializeAsync(); using var client = app.Browser(); await TestApp.LoginAsync(client);
        await using var scope = app.Services.CreateAsyncScope(); var input = Staff(); input.FirstName = "Updated";
        Assert.True((await scope.ServiceProvider.GetRequiredService<AdministrationService>().SaveEmployeeAsync(1, 2, input)).Success);
        var page = await client.GetAsync("/Inventory"); Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Updated Chen", await page.Content.ReadAsStringAsync());
    }

    [SqlServerFact]
    public async Task LoginHashUpgradeIssuesACookieForTheUpgradedHash()
    {
        await using var app = new TestApp(); await app.InitializeAsync(); await using var scope = app.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<EmployeeRepository>();
        var employee = (await repository.FindByIdAsync(2))!;
        var legacy = new PasswordHasher<Employee>(Options.Create(new PasswordHasherOptions { IterationCount = 1 })).HashPassword(employee, TestApp.Password);
        Assert.True(await repository.TryUpgradeHashAsync(2, employee.PasswordHash, legacy));
        using var client = app.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await TestApp.LoginAsync(client)).StatusCode);
        Assert.NotEqual(legacy, (await repository.FindByIdAsync(2))!.PasswordHash);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Inventory")).StatusCode);
    }

    [SqlServerFact]
    public async Task EditionIdentityIsFixedAfterHistoryButMetadataRemainsEditable()
    {
        await using var app = new TestApp(); await app.InitializeAsync(); await using var scope = app.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdministrationService>(); var stock = scope.ServiceProvider.GetRequiredService<StockService>();
        Assert.True((await admin.SaveItemAsync(1, null, Item())).Success);
        var id = Convert.ToInt32(await app.ScalarAsync("SELECT MAX(item_id) FROM dbo.ITEM"), System.Globalization.CultureInfo.InvariantCulture);
        var input = Item(); input.Format = "Blu-ray";
        Assert.True((await admin.SaveItemAsync(1, id, input)).Success); // No movements yet.
        Assert.True((await stock.RecordAsync(new(1, id, 1, "CHECK IN", 2, "Edition fixture"))).Success);
        input.Format = "DVD";
        Assert.False((await admin.SaveItemAsync(1, id, input)).Success);
        input.Format = "Blu-ray"; input.Type = "GAME";
        Assert.False((await admin.SaveItemAsync(1, id, input)).Success);
        input.Type = "MOVIE"; input.Name = "Corrected display title"; input.AgeRating = "PG";
        Assert.True((await admin.SaveItemAsync(1, id, input)).Success);
        Assert.Equal("Blu-ray", await app.ScalarAsync($"SELECT media_format FROM dbo.ITEM WHERE item_id={id}"));
        Assert.True((await stock.RecordAsync(new(1, id, 1, "CHECK OUT", 2, "Return to zero"))).Success);
        input.Format = "DVD";
        Assert.False((await admin.SaveItemAsync(1, id, input)).Success); // Zero balance does not erase history.
        using var client = app.Browser(); await TestApp.LoginAsync(client, "jordan.lee");
        var html = await client.GetStringAsync($"/Admin/Items/{id}");
        Assert.Contains("readonly=\"readonly\"", html); Assert.Contains("disabled=\"disabled\"", html);
        Assert.Contains("fixed after the first stock movement", html);
    }

    [SqlServerTheory]
    [InlineData("STAFF", "ACTIVE")]
    [InlineData("MANAGER", "INACTIVE")]
    public async Task SelfAccountChangesExplainTheSignOut(string role, string status)
    {
        await using var app = new TestApp(); await app.InitializeAsync(); await using var scope = app.Services.CreateAsyncScope();
        var other = Staff(); other.Role = "MANAGER";
        Assert.True((await scope.ServiceProvider.GetRequiredService<AdministrationService>().SaveEmployeeAsync(1, 2, other)).Success);
        using var client = app.Browser(); await TestApp.LoginAsync(client, "jordan.lee");
        Assert.Contains("Saving will sign you out", await client.GetStringAsync("/Admin/Employees/1"));
        var response = await client.PostAsync("/Admin/Employees/1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = "2",
            ["Input.FirstName"] = "Jordan",
            ["Input.LastName"] = "Lee",
            ["Input.Username"] = "jordan.lee",
            ["Input.Role"] = role,
            ["Input.Status"] = status,
            ["__RequestVerificationToken"] = await TestApp.TokenAsync(client, "/Admin/Employees/1")
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); Assert.Equal("/Account/Login", response.Headers.Location!.OriginalString);
        var login = await client.GetStringAsync("/Account/Login");
        Assert.Contains(status == "ACTIVE" ? "Sign in again" : "no longer active", login);
        Assert.Equal("MANAGER", await app.ScalarAsync("SELECT access_role FROM dbo.EMPLOYEE WHERE employee_id=2"));
    }

    [SqlServerFact]
    public async Task CorrectionsExplainHowToReactivateRetiredRecords()
    {
        await using var app = new TestApp(); await app.InitializeAsync(); await using var scope = app.Services.CreateAsyncScope();
        var stock = scope.ServiceProvider.GetRequiredService<StockService>(); var admin = scope.ServiceProvider.GetRequiredService<AdministrationService>();
        Assert.True((await admin.SaveItemAsync(1, null, Item())).Success);
        var item = Convert.ToInt32(await app.ScalarAsync("SELECT MAX(item_id) FROM dbo.ITEM"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.True((await admin.SaveLocationAsync(1, null, new() { Name = "Review warehouse", Address = "Demo", Status = "ACTIVE" })).Success);
        var location = Convert.ToInt32(await app.ScalarAsync("SELECT MAX(location_id) FROM dbo.LOCATION"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.True((await stock.RecordAsync(new(1, item, location, "CHECK IN", 1, "Receipt"))).Success);
        var original = Convert.ToInt64(await app.ScalarAsync("SELECT MAX(transaction_id) FROM dbo.TRANSACTION_LOG"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.True((await stock.RecordAsync(new(1, item, location, "CHECK OUT", 1, "Dispatch"))).Success);
        Assert.True((await admin.SaveItemAsync(1, item, Item("ARCHIVED"))).Success);
        Assert.Contains("Reactivate this catalog item", (await stock.CorrectAsync(1, original, "Correction")).Message);
        Assert.True((await admin.SaveItemAsync(1, item, Item())).Success);
        Assert.True((await admin.SaveLocationAsync(1, location, new() { Name = "Review warehouse", Address = "Demo", Status = "INACTIVE" })).Success);
        Assert.Contains("Reactivate this warehouse", (await stock.CorrectAsync(1, original, "Correction")).Message);
    }

    [SqlServerTheory]
    [InlineData("item")]
    [InlineData("location")]
    public async Task RetirementWaitsAtTheParentBeforeTakingStockLocks(string parent)
    {
        await using var app = new TestApp(); await app.InitializeAsync(); await using var scope = app.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdministrationService>();
        Assert.True((await admin.SaveItemAsync(1, null, Item())).Success);
        Assert.True((await admin.SaveLocationAsync(1, null, new() { Name = "Lock fixture", Address = "Demo", Status = "ACTIVE" })).Success);
        var item = Convert.ToInt32(await app.ScalarAsync("SELECT MAX(item_id) FROM dbo.ITEM"), System.Globalization.CultureInfo.InvariantCulture);
        var location = Convert.ToInt32(await app.ScalarAsync("SELECT MAX(location_id) FROM dbo.LOCATION"), System.Globalization.CultureInfo.InvariantCulture);
        var database = scope.ServiceProvider.GetRequiredService<SqlDatabase>();
        await using var movement = await database.OpenConnectionAsync();
        await using var transaction = (SqlTransaction)await movement.BeginTransactionAsync(IsolationLevel.Serializable);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Task<SaveResult>? retirement = null;
        try
        {
            using var session = new SqlCommand("SELECT @@SPID;", movement, transaction);
            var sessionId = Convert.ToInt32(await session.ExecuteScalarAsync(timeout.Token), System.Globalization.CultureInfo.InvariantCulture);
            // Pause a movement after its parent reads and indexed balance lock, before the write.
            using var hold = SqlDatabase.Command("""
                SELECT employee_id FROM dbo.EMPLOYEE WITH (HOLDLOCK) WHERE employee_id=1;
                SELECT item_id FROM dbo.ITEM WITH (HOLDLOCK) WHERE item_id=@item;
                SELECT location_id FROM dbo.LOCATION WITH (HOLDLOCK) WHERE location_id=@location;
                SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WITH (UPDLOCK,HOLDLOCK) WHERE item_id=@item AND location_id=@location;
                """, movement, transaction, SqlDatabase.Parameter("@item", SqlDbType.Int, item), SqlDatabase.Parameter("@location", SqlDbType.Int, location));
            await hold.ExecuteNonQueryAsync(timeout.Token);
            async Task<SaveResult> Retire()
            {
                await using var writer = app.Services.CreateAsyncScope();
                var service = writer.ServiceProvider.GetRequiredService<AdministrationService>();
                return parent == "item" ? await service.SaveItemAsync(1, item, Item("ARCHIVED"), timeout.Token)
                    : await service.SaveLocationAsync(1, location, new() { Name = "Lock fixture", Address = "Demo", Status = "INACTIVE" }, timeout.Token);
            }
            retirement = Retire();
            await using var observer = await database.OpenConnectionAsync(timeout.Token);
            using var blocked = new SqlCommand("SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id=@session AND database_id=DB_ID();", observer);
            blocked.Parameters.Add(SqlDatabase.Parameter("@session", SqlDbType.Int, sessionId));
            while (Convert.ToInt32(await blocked.ExecuteScalarAsync(timeout.Token), System.Globalization.CultureInfo.InvariantCulture) == 0)
                await Task.Delay(20, timeout.Token);
            using var finish = SqlDatabase.Command("""
                INSERT INTO dbo.CURRENT_INVENTORY (item_id,location_id,quantity_on_hand,last_updated_at) VALUES (@item,@location,1,SYSUTCDATETIME());
                INSERT INTO dbo.TRANSACTION_LOG (employee_id,item_id,location_id,transaction_type,quantity_moved,transaction_datetime,notes)
                VALUES (1,@item,@location,'CHECK IN',1,SYSUTCDATETIME(),'Lock fixture receipt');
                """, movement, transaction, SqlDatabase.Parameter("@item", SqlDbType.Int, item), SqlDatabase.Parameter("@location", SqlDbType.Int, location));
            await finish.ExecuteNonQueryAsync(timeout.Token);
            await transaction.CommitAsync(timeout.Token);
            Assert.False((await retirement).Success);
            Assert.Empty(app.DatabaseWarnings);
        }
        finally
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync(CancellationToken.None);
            await timeout.CancelAsync();
            if (retirement is not null)
            {
                try { await retirement; } catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }
            }
        }
    }
}
