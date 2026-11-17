using System.Net;
using System.Text;
using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BlueBox.Web.Tests;

public sealed class MilestoneTwoTests
{
    private static ItemInput Item(string status = "ACTIVE") => new() { Name = "Demo title", Type = "MOVIE", Format = "DVD", Status = status };
    private static EmployeeInput Employee(string username, string role = "STAFF", string status = "ACTIVE") => new()
    { FirstName = "Demo", LastName = "Employee", Username = username, Role = role, Status = status };

    [Fact]
    public async Task ManagerInputsAndCorrectionExplanationAreValidatedWithoutDatabaseAccess()
    {
        var db = new SqlDatabase(new ConfigurationBuilder().Build(), NullLogger<SqlDatabase>.Instance);
        var admin = new AdministrationService(db, new PasswordHasher<Employee>());
        Assert.False((await admin.SaveItemAsync(1, null, new())).Success);
        var item = Item(); item.PosterUrl = "javascript:alert(1)";
        Assert.False((await admin.SaveItemAsync(1, null, item)).Success);
        item.PosterUrl = "http://example.com/poster.jpg";
        Assert.False(InputValidation.IsValid(item));
        item.PosterUrl = "https://example.com/poster.jpg";
        Assert.True(InputValidation.IsValid(item));
        Assert.False((await admin.SaveLocationAsync(1, null, new() { Name = "  ", Address = "Test", Status = "ACTIVE" })).Success);
        Assert.False((await admin.SaveEmployeeAsync(1, null, Employee("demo"))).Success);
        Assert.False((await new StockService(db).CorrectAsync(1, 1, "   ")).Success);
        Assert.False((await new StockService(db).CorrectAsync(1, 1, new string('x', 256))).Success);
        Assert.False((await new StockService(db).CorrectAsync(1, 0, "Reason")).Success);
    }

    [Fact]
    public async Task NewRoutesRequireSignIn()
    {
        await using var app = new TestApp(); using var client = app.Browser();
        foreach (var path in new[] { "/Overview", "/Admin/Items", "/Admin/Employees", "/Admin/Locations", "/Activity/Correct/1", "/Admin/Items?handler=Lookup&title=demo" })
            Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public void CentralTimeLabelsRespectSummerAndWinter()
    {
        Assert.Equal("Jan 15, 2026 · 06:00 CST", DisplayTime.Central(new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("Jul 15, 2026 · 07:00 CDT", DisplayTime.Central(new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("Nov 1, 2026 · 01:30 CDT", DisplayTime.Central(new DateTime(2026, 11, 1, 6, 30, 0, DateTimeKind.Utc)));
        Assert.Equal("Nov 1, 2026 · 01:30 CST", DisplayTime.Central(new DateTime(2026, 11, 1, 7, 30, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task AdministrationLinksResolveForNewAndExistingRecords()
    {
        await using var app = new TestApp(); using var client = app.Browser();
        var links = app.Services.GetRequiredService<LinkGenerator>();
        foreach (var name in new[] { "Items", "Employees", "Locations" })
        {
            Assert.Equal($"/Admin/{name}", links.GetPathByPage($"/Admin/{name}", values: new { id = (int?)null }));
            Assert.Equal($"/Admin/{name}/1", links.GetPathByPage($"/Admin/{name}", values: new { id = 1 }));
            Assert.Equal($"/Admin/{name}?Search=demo", links.GetPathByPage($"/Admin/{name}", values: new { Search = "demo" }));
            Assert.Equal($"/Admin/{name}/1?Search=demo", links.GetPathByPage($"/Admin/{name}", values: new { id = 1, Search = "demo" }));
        }
    }

    [SqlServerFact]
    public async Task StaffCannotGetOrPostManagerScreensOrUseManagerServices()
    {
        await using var app = new TestApp(); await app.InitializeAsync(); using var client = app.Browser(); await TestApp.LoginAsync(client);
        foreach (var path in new[] { "/Admin/Items", "/Admin/Employees", "/Admin/Locations", "/Activity/Correct/1", "/Admin/Items?handler=Lookup&title=demo" })
        {
            var get = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, get.StatusCode);
            Assert.Contains("AccessDenied", get.Headers.Location!.OriginalString);
            var post = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>()));
            Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
            Assert.Contains("AccessDenied", post.Headers.Location!.OriginalString);
        }
        await using var scope = app.Services.CreateAsyncScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<AdministrationService>().SaveItemAsync(2, null, Item())).Success);
        Assert.False((await scope.ServiceProvider.GetRequiredService<StockService>().CorrectAsync(2, 1, "Not authorized")).Success);
    }

    [SqlServerFact]
    public async Task ManagerScreensCreateAndEditCatalogWithoutAddingStock()
    {
        await using var app = new TestApp(); await app.InitializeAsync(); using var client = app.Browser(); await TestApp.LoginAsync(client, "jordan.lee");
        foreach (var page in new[] { "/Admin/Items", "/Admin/Employees", "/Admin/Locations", "/Overview", "/Activity/Correct/1" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(page)).StatusCode);
        var response = await client.PostAsync("/Admin/Items", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Name"] = "HTTP test movie",
            ["Input.Type"] = "MOVIE",
            ["Input.Format"] = "DVD",
            ["Input.Status"] = "ACTIVE",
            ["__RequestVerificationToken"] = await TestApp.TokenAsync(client, "/Admin/Items")
        }));
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            Assert.Fail("Unexpected server failure: " + string.Join("\n", app.ServerErrors.Select(error => error.ToString())));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(1, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.ITEM WHERE item_name='HTTP test movie'"));
        Assert.Equal(0, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.CURRENT_INVENTORY AS b JOIN dbo.ITEM AS i ON i.item_id=b.item_id WHERE i.item_name='HTTP test movie'"));
        response = await client.PostAsync("/Admin/Items", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Name"] = "Missing type",
            ["Input.Format"] = "DVD",
            ["Input.Status"] = "ACTIVE",
            ["__RequestVerificationToken"] = await TestApp.TokenAsync(client, "/Admin/Items")
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.ITEM WHERE item_name='Missing type'"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Admin/Items", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);

        var id = Convert.ToInt32(await app.ScalarAsync("SELECT item_id FROM dbo.ITEM WHERE item_name='HTTP test movie'"), System.Globalization.CultureInfo.InvariantCulture);
        response = await client.PostAsync($"/Admin/Items/{id}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = "1",
            ["Input.Name"] = "HTTP edited movie",
            ["Input.Type"] = "MOVIE",
            ["Input.Format"] = "Blu-ray",
            ["Input.Status"] = "ACTIVE",
            ["__RequestVerificationToken"] = await TestApp.TokenAsync(client, $"/Admin/Items/{id}")
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Admin/Items/{id}", response.Headers.Location!.OriginalString);
        Assert.Equal("Blu-ray", await app.ScalarAsync($"SELECT media_format FROM dbo.ITEM WHERE item_id={id}"));

        response = await client.PostAsync("/Admin/Locations", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Name"] = "HTTP warehouse",
            ["Input.Address"] = "Demo address",
            ["Input.Status"] = "ACTIVE",
            ["__RequestVerificationToken"] = await TestApp.TokenAsync(client, "/Admin/Locations")
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(1, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.LOCATION WHERE location_name='HTTP warehouse'"));

        response = await client.PostAsync("/Admin/Employees", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.FirstName"] = "HTTP",
            ["Input.LastName"] = "Staff",
            ["Input.Username"] = "http.staff",
            ["Input.Role"] = "STAFF",
            ["Input.Status"] = "ACTIVE",
            ["Input.Password"] = "tiny",
            ["__RequestVerificationToken"] = await TestApp.TokenAsync(client, "/Admin/Employees")
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invalid = await response.Content.ReadAsStringAsync();
        Assert.Contains("minimum length of 12", invalid); Assert.DoesNotContain("value=\"tiny\"", invalid);
        Assert.Equal(0, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.EMPLOYEE WHERE username='http.staff'"));
        response = await client.PostAsync("/Admin/Employees", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.FirstName"] = "HTTP",
            ["Input.LastName"] = "Staff",
            ["Input.Username"] = "http.staff",
            ["Input.Role"] = "STAFF",
            ["Input.Status"] = "ACTIVE",
            ["Input.Password"] = TestApp.Password,
            ["__RequestVerificationToken"] = await TestApp.TokenAsync(client, "/Admin/Employees")
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var createdEmployee = app.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await TestApp.LoginAsync(createdEmployee, "http.staff")).StatusCode);
    }

    [SqlServerFact]
    public async Task EmployeeAdministrationProtectsLastManagerAndPreservesPasswords()
    {
        await using var app = new TestApp(); await app.InitializeAsync(); await using var scope = app.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdministrationService>();
        Assert.False((await admin.SaveEmployeeAsync(1, 1, Employee("jordan.lee", "STAFF"))).Success);
        Assert.False((await admin.SaveEmployeeAsync(1, 1, Employee("jordan.lee", "MANAGER", "INACTIVE"))).Success);
        var create = Employee("new.staff"); create.Password = TestApp.Password;
        Assert.True((await admin.SaveEmployeeAsync(1, null, create)).Success);
        Assert.False((await admin.SaveEmployeeAsync(1, null, create)).Success);
        var id = Convert.ToInt32(await app.ScalarAsync("SELECT employee_id FROM dbo.EMPLOYEE WHERE username='new.staff'"), System.Globalization.CultureInfo.InvariantCulture);
        var hash = await app.ScalarAsync("SELECT password_hash FROM dbo.EMPLOYEE WHERE username='new.staff'");
        Assert.True((await admin.SaveEmployeeAsync(1, id, Employee("new.staff", "STAFF", "INACTIVE"))).Success);
        Assert.Equal(hash, await app.ScalarAsync("SELECT password_hash FROM dbo.EMPLOYEE WHERE username='new.staff'"));
        create.Password = "Replacement-password-42!";
        Assert.True((await admin.SaveEmployeeAsync(1, id, create)).Success);
        var repository = scope.ServiceProvider.GetRequiredService<EmployeeRepository>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Employee>>();
        var employee = (await repository.FindByIdAsync(id))!;
        // A login using an earlier hash cannot undo a manager's later password reset.
        Assert.False(await repository.TryUpgradeHashAsync(id, (string)hash!, hasher.HashPassword(employee, TestApp.Password)));
        Assert.Equal(employee.PasswordHash, (await repository.FindByIdAsync(id))!.PasswordHash);
        Assert.True(await repository.TryUpgradeHashAsync(id, employee.PasswordHash, hasher.HashPassword(employee, create.Password)));
        using var client = app.Browser();
        Assert.Equal(HttpStatusCode.OK, (await TestApp.LoginAsync(client, "new.staff")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await TestApp.LoginAsync(client, "new.staff", create.Password)).StatusCode);
    }

    [SqlServerFact]
    public async Task CompetingManagerDemotionsCannotRemoveBothManagers()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        await app.ExecuteAsync("UPDATE dbo.EMPLOYEE SET access_role='MANAGER' WHERE employee_id=2");
        async Task<SaveResult> Demote(int actor, int target, string username)
        {
            await using var scope = app.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<AdministrationService>().SaveEmployeeAsync(actor, target, Employee(username));
        }
        var results = await Task.WhenAll(Demote(1, 2, "avery.chen"), Demote(2, 1, "jordan.lee"));
        Assert.Single(results, r => r.Success);
        Assert.Equal(1, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.EMPLOYEE WHERE access_role='MANAGER' AND employment_status='ACTIVE'"));
    }

    [SqlServerFact]
    public async Task RetirementRequiresZeroStockAndKeepsHistory()
    {
        await using var app = new TestApp(); await app.InitializeAsync(); await using var scope = app.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdministrationService>();
        Assert.False((await admin.SaveItemAsync(1, 1, Item("ARCHIVED"))).Success);
        Assert.False((await admin.SaveLocationAsync(1, 1, new() { Name = "Warehouse", Address = "Demo", Status = "INACTIVE" })).Success);
        var count = await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG");
        Assert.True((await admin.SaveItemAsync(1, null, Item())).Success);
        var id = Convert.ToInt32(await app.ScalarAsync("SELECT MAX(item_id) FROM dbo.ITEM"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.True((await admin.SaveItemAsync(1, id, Item("ARCHIVED"))).Success);
        Assert.False((await scope.ServiceProvider.GetRequiredService<StockService>().RecordAsync(new(1, id, 1, "CHECK IN", 1, null))).Success);
        Assert.True((await admin.SaveLocationAsync(1, null, new() { Name = "Empty", Address = "Demo", Status = "ACTIVE" })).Success);
        var location = Convert.ToInt32(await app.ScalarAsync("SELECT MAX(location_id) FROM dbo.LOCATION"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.True((await admin.SaveLocationAsync(1, location, new() { Name = "Empty", Address = "Demo", Status = "INACTIVE" })).Success);
        Assert.Equal(count, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG"));
        Assert.False((await scope.ServiceProvider.GetRequiredService<StockService>().RecordAsync(new(1, 1, location, "CHECK IN", 1, null))).Success);
    }

    [SqlServerFact]
    public async Task CorrectionsFormAChainAndRejectDuplicatesAndInsufficientStock()
    {
        await using var app = new TestApp(); await app.InitializeAsync(); await using var scope = app.Services.CreateAsyncScope();
        var stock = scope.ServiceProvider.GetRequiredService<StockService>();
        var before = await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=1 AND location_id=1");
        Assert.True((await stock.RecordAsync(new(1, 1, 1, "CHECK IN", 3, "Mistaken receipt"))).Success);
        var original = Convert.ToInt64(await app.ScalarAsync("SELECT MAX(transaction_id) FROM dbo.TRANSACTION_LOG"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.True((await stock.CorrectAsync(1, original, "Reverse mistaken receipt")).Success);
        Assert.False((await stock.CorrectAsync(1, original, "Duplicate correction")).Success);
        var correction = Convert.ToInt64(await app.ScalarAsync("SELECT MAX(transaction_id) FROM dbo.TRANSACTION_LOG"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.True((await stock.CorrectAsync(1, correction, "Receipt was correct after all")).Success);
        Assert.Equal(original, await app.ScalarAsync($"SELECT corrects_transaction_id FROM dbo.TRANSACTION_LOG WHERE transaction_id={correction}"));
        Assert.Equal("Mistaken receipt", await app.ScalarAsync($"SELECT notes FROM dbo.TRANSACTION_LOG WHERE transaction_id={original}"));
        Assert.Equal(Convert.ToInt32(before, System.Globalization.CultureInfo.InvariantCulture) + 3, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=1 AND location_id=1"));
        // Original opening balance is 12; only 1 remains after an ordinary check-out.
        Assert.True((await stock.RecordAsync(new(1, 1, 1, "CHECK OUT", 14, "Later stock movement"))).Success);
        var count = await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG");
        Assert.False((await stock.CorrectAsync(1, 1, "Cannot reverse opening balance")).Success);
        Assert.Equal(count, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG"));
        using var client = app.Browser(); await TestApp.LoginAsync(client, "jordan.lee");
        var activity = await client.GetStringAsync("/Activity");
        Assert.Contains($"Reverses #{original}", activity);
        Assert.Contains($"Reversed by #{correction}", activity);
    }

    [SqlServerFact]
    public async Task CompetingCorrectionsOnlyReverseAMovementOnce()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        async Task<MovementResult> Reverse()
        {
            await using var scope = app.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<StockService>().CorrectAsync(1, 1, "Concurrent correction");
        }
        var results = await Task.WhenAll(Reverse(), Reverse());
        Assert.Single(results, r => r.Success);
        Assert.Equal(1, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG WHERE corrects_transaction_id=1"));
        Assert.Equal(0, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=1 AND location_id=1"));
    }

    [SqlServerFact]
    public async Task RetirementAndFirstCheckInCannotBothSucceed()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        await using (var setup = app.Services.CreateAsyncScope())
            Assert.True((await setup.ServiceProvider.GetRequiredService<AdministrationService>().SaveItemAsync(1, null, Item())).Success);
        var id = Convert.ToInt32(await app.ScalarAsync("SELECT MAX(item_id) FROM dbo.ITEM"), System.Globalization.CultureInfo.InvariantCulture);
        async Task<bool> Archive()
        {
            await using var scope = app.Services.CreateAsyncScope();
            return (await scope.ServiceProvider.GetRequiredService<AdministrationService>().SaveItemAsync(1, id, Item("ARCHIVED"))).Success;
        }
        async Task<bool> Receive()
        {
            await using var scope = app.Services.CreateAsyncScope();
            return (await scope.ServiceProvider.GetRequiredService<StockService>().RecordAsync(new(2, id, 1, "CHECK IN", 1, "Concurrent receipt"))).Success;
        }
        var results = await Task.WhenAll(Archive(), Receive());
        Assert.Single(results, success => success);
        var archived = Equals("ARCHIVED", await app.ScalarAsync($"SELECT item_status FROM dbo.ITEM WHERE item_id={id}"));
        Assert.Equal(archived ? 0 : 1, await app.ScalarAsync($"SELECT COALESCE(SUM(quantity_on_hand),0) FROM dbo.CURRENT_INVENTORY WHERE item_id={id}"));
    }

    [SqlServerFact]
    public async Task CorrectionHttpPostUsesOriginalFieldsAndRollsBackHistoryFailure()
    {
        await using var app = new TestApp(); await app.InitializeAsync(); using var client = app.Browser(); await TestApp.LoginAsync(client, "jordan.lee");
        var token = await TestApp.TokenAsync(client, "/Activity/Correct/1");
        var response = await client.PostAsync("/Activity/Correct/1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = "2",
            ["Explanation"] = "HTTP correction",
            ["Quantity"] = "99999",
            ["ItemId"] = "6",
            ["EmployeeId"] = "2",
            ["Direction"] = "CHECK IN",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(0, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=1 AND location_id=1"));
        Assert.Equal(1, await app.ScalarAsync("SELECT TOP (1) employee_id FROM dbo.TRANSACTION_LOG ORDER BY transaction_id DESC"));
        await app.ExecuteAsync("CREATE TRIGGER dbo.RejectCorrection ON dbo.TRANSACTION_LOG AFTER INSERT AS BEGIN; THROW 51000, 'Test failure', 1; END;");
        await using var scope = app.Services.CreateAsyncScope();
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => scope.ServiceProvider.GetRequiredService<StockService>().CorrectAsync(1, 2, "Force failure"));
        Assert.Equal(0, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.TRANSACTION_LOG WHERE corrects_transaction_id=2"));
        Assert.Equal(6, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=1 AND location_id=2"));
    }

    [SqlServerFact]
    public async Task DashboardTotalsIncludeAllBalancesAndEmptyWarehouses()
    {
        await using var app = new TestApp(); await app.InitializeAsync(); await using var scope = app.Services.CreateAsyncScope();
        var overview = scope.ServiceProvider.GetRequiredService<OverviewQueries>();
        var snapshot = await overview.LoadAsync();
        var summary = snapshot.Summary;
        Assert.Equal(await app.ScalarAsync("SELECT SUM(CAST(quantity_on_hand AS bigint)) FROM dbo.CURRENT_INVENTORY"), summary.Units);
        Assert.Equal(6, summary.ActiveItems); Assert.Equal(2, summary.ActiveLocations);
        Assert.Equal(summary.Units, snapshot.Warehouses.Sum(w => w.Quantity));
        await app.ExecuteAsync("INSERT INTO dbo.LOCATION (location_name, address, location_status) VALUES ('Empty warehouse', 'Demo', 'ACTIVE');");
        Assert.Contains((await overview.LoadAsync()).Warehouses, w => w.Name == "Empty warehouse" && w.Quantity == 0);
        async Task ChangeStock()
        {
            await using var writer = app.Services.CreateAsyncScope();
            var stock = writer.ServiceProvider.GetRequiredService<StockService>();
            for (var n = 0; n < 8; n++) Assert.True((await stock.RecordAsync(new(2, 1, 1, "CHECK IN", 1, "Dashboard concurrency test"))).Success);
        }
        async Task CheckSnapshots()
        {
            for (var n = 0; n < 8; n++)
            {
                var current = await overview.LoadAsync();
                Assert.Equal(current.Summary.Units, current.Warehouses.Sum(w => w.Quantity));
                Assert.Equal(5, current.Recent.Count);
            }
        }
        await Task.WhenAll(ChangeStock(), CheckSnapshots());
    }
}

public sealed class OmdbTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(respond(request)); }
    }
    private static OmdbClient Client(StubHandler handler, string? key = "fake-test-key") => new(new HttpClient(handler),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Omdb:ApiKey"] = key }).Build());
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task SearchAndDetailExposeOnlyDisplayMetadata()
    {
        using var handler = new StubHandler(request =>
        {
            Assert.Equal("www.omdbapi.com", request.RequestUri!.Host);
            Assert.Equal("https", request.RequestUri.Scheme);
            return request.RequestUri.Query.Contains("s=")
                ? Json("""{"Response":"True","Search":[{"imdbID":"tt1234567","Title":"Demo & Movie","Year":"2026","Type":"movie"}]}""")
                : Json("""{"Response":"True","Type":"movie","Title":"Demo & Movie","Poster":"https://example.com/poster.jpg","Rated":"PG-13","imdbRating":"8.5"}""");
        });
        var client = Client(handler);
        Assert.Equal("Demo & Movie", Assert.Single((await client.SearchAsync("Demo & Movie")).Matches).Title);
        var result = await client.DetailAsync("tt1234567");
        Assert.Equal("PG-13", result.Movie!.AgeRating);
        Assert.Equal("https://example.com/poster.jpg", result.Movie.PosterUrl);
        Assert.DoesNotContain("fake-test-key", System.Text.Json.JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData("{\"Response\":\"False\",\"Error\":\"Invalid API key!\"}")]
    [InlineData("not JSON")]
    [InlineData("[]")]
    public async Task UnavailableDataAllowsManualEntry(string body)
    {
        using var handler = new StubHandler(_ => Json(body));
        Assert.Empty((await Client(handler).SearchAsync("demo")).Matches);
        Assert.Null((await Client(handler).DetailAsync("tt1234567")).Movie);
    }

    [Theory]
    [InlineData("Too many results.", "more specific title")]
    [InlineData("Movie not found!", "No movie matches")]
    [InlineData("Invalid API key!", "unavailable")]
    public async Task ProviderErrorsGiveAccurateSafeMessages(string error, string expected)
    {
        using var handler = new StubHandler(_ => Json(System.Text.Json.JsonSerializer.Serialize(new { Response = "False", Error = error })));
        var result = await Client(handler).SearchAsync("demo");
        Assert.Empty(result.Matches); Assert.Contains(expected, result.Message ?? "");
        Assert.DoesNotContain("fake-test-key", result.Message ?? "");
        Assert.DoesNotContain("Invalid API key", result.Message ?? "");
    }

    [Fact]
    public async Task MissingKeyAndInvalidIdentifiersDoNotMakeRequests()
    {
        using var handler = new StubHandler(_ => throw new InvalidOperationException("Should not send"));
        Assert.Empty((await Client(handler, null).SearchAsync("demo")).Matches);
        Assert.Null((await Client(handler).DetailAsync("https://other.example/")).Movie);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("N/A", "N/A")]
    [InlineData("http://example.com/poster", "N/A")]
    public async Task MissingMetadataIsOptional(string poster, string rated)
    {
        using var handler = new StubHandler(_ => Json(System.Text.Json.JsonSerializer.Serialize(new { Response = "True", Type = "movie", Title = "Demo", Poster = poster, Rated = rated })));
        var result = await Client(handler).DetailAsync("tt1234567");
        Assert.NotNull(result.Movie); Assert.Null(result.Movie.PosterUrl); Assert.Null(result.Movie.AgeRating);
    }
}
