using System.Net;
using System.Text.RegularExpressions;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BlueBox.Web.Tests;

public sealed class MainReviewTests
{
    [Fact]
    public async Task PagedListTargetsResolveWithTheirFilterValues()
    {
        await using var app = new TestApp(); using var client = app.Browser();
        var links = app.Services.GetRequiredService<LinkGenerator>();
        foreach (var page in new[] { "/Catalog/Index", "/Inventory/Index", "/Admin/Items" })
        {
            var url = links.GetPathByPage(page, values: new { PageNumber = 3, Search = "Paging fixture", Type = "MOVIE", LocationId = 1 });
            Assert.False(string.IsNullOrEmpty(url), $"Cannot generate paging URL for {page}.");
            Assert.Equal("3", QueryHelpers.ParseQuery(new Uri(client.BaseAddress!, url!).Query)["PageNumber"].ToString());
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(7)]
    public async Task ManagerPagerTagHelperGeneratesAHrefForNewAndExistingItems(int? id)
    {
        await using var app = new TestApp(); using var client = app.Browser();
        var context = new DefaultHttpContext { RequestServices = app.Services };
        context.Request.RouteValues["page"] = "/Admin/Items";
        if (id.HasValue) context.Request.RouteValues["id"] = id.Value;
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "catalog"));
        var route = new RouteData(); route.Values["page"] = "/Admin/Items";
        if (id.HasValue) route.Values["id"] = id.Value;
        var view = new ViewContext { HttpContext = context, RouteData = route, ActionDescriptor = new PageActionDescriptor { ViewEnginePath = "/Admin/Items" } };
        var helper = new AnchorTagHelper(app.Services.GetRequiredService<IHtmlGenerator>()) { ViewContext = view, Page = "/Admin/Items" };
        var model = new BlueBox.Web.Pages.Admin.ItemsModel(null!, null!, null!) { Id = id, Search = "Paging fixture", PageNumber = 2 };
        helper.RouteValues = model.ItemRouteValues(3);
        var output = new TagHelperOutput("a", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        helper.Process(new TagHelperContext([], new Dictionary<object, object>(), "pager"), output);
        Assert.False(string.IsNullOrEmpty(output.Attributes["href"]?.Value?.ToString()));
        var uri = new Uri(client.BaseAddress!, output.Attributes["href"].Value.ToString()!);
        Assert.Equal(id.HasValue ? $"/Admin/Items/{id}" : "/Admin/Items", uri.AbsolutePath);
        Assert.Equal("3", QueryHelpers.ParseQuery(uri.Query)["PageNumber"].ToString());
        Assert.Equal("Paging fixture", QueryHelpers.ParseQuery(uri.Query)["Search"].ToString());
    }

    [Fact]
    public async Task LoginPostsAreLimitedWhileReadsAndOtherRoutesRemainAvailable()
    {
        await using var app = new TestApp(); using var client = app.Browser();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            // Even invalid/no-token POSTs consume the limit, without opening a database connection.
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Account/Login", new FormUrlEncodedContent([]))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Login")).StatusCode);
        }
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.0.2.123"); // An untrusted header cannot bypass the limit.
        var rejected = await client.PostAsync("/Account/Login", new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("text/plain", rejected.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", rejected.Content.Headers.ContentType?.CharSet);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Contains("Wait a minute", await rejected.Content.ReadAsStringAsync());
        Assert.True(rejected.Headers.CacheControl!.NoStore);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Login")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Inventory")).StatusCode);
    }

    [Fact]
    public async Task OnlyCorrectlyVersionedStaticAssetsGetImmutableCaching()
    {
        await using var app = new TestApp(); using var client = app.Browser();
        var versions = app.Services.GetRequiredService<IFileVersionProvider>();
        foreach (var path in new[] { "/css/site.css", "/js/movement.js", "/js/movies.js" })
        {
            var url = versions.AddFileVersionToPath(PathString.Empty, path);
            Assert.Contains("?v=", url);
            var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.Public);
            Assert.Equal(TimeSpan.FromDays(365), response.Headers.CacheControl.MaxAge);
            Assert.Contains("immutable", response.Headers.CacheControl.ToString());
            Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
            foreach (var unversioned in new[] { path, path + "?v=outdated", url + "&v=duplicate" })
            {
                var plain = await client.GetAsync(unversioned);
                Assert.Equal(TimeSpan.FromHours(1), plain.Headers.CacheControl!.MaxAge);
                Assert.DoesNotContain("immutable", plain.Headers.CacheControl.ToString());
            }
        }
        var missing = await client.GetAsync("/css/missing.css?v=anything");
        Assert.False(missing.Headers.CacheControl?.Public ?? false);
        var login = await client.GetAsync("/Account/Login");
        Assert.True(login.Headers.CacheControl!.NoStore);
        Assert.Equal("0", Assert.Single(login.Headers.GetValues("X-XSS-Protection")));
        Assert.Equal("same-origin", Assert.Single(login.Headers.GetValues("Referrer-Policy")));
        Assert.Contains("camera=()", Assert.Single(login.Headers.GetValues("Permissions-Policy")));
        Assert.Contains("?v=", await login.Content.ReadAsStringAsync());
    }

    [Fact]
    public void ErrorResponsesDistinguishUnavailableServicesFromCodeFailuresWithoutLeakingDetails()
    {
        foreach (var failure in new Exception?[] { new TimeoutException("private timeout details"), new InvalidOperationException("private bug details"), null })
        {
            var context = new DefaultHttpContext();
            if (failure is not null) context.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature { Error = failure });
            var result = Program.HandleError(context, NullLogger<Program>.Instance);
            Assert.Equal(failure is TimeoutException ? 503 : 500, ((IStatusCodeHttpResult)result).StatusCode);
            var html = ((Microsoft.AspNetCore.Http.HttpResults.ContentHttpResult)result).ResponseContent!;
            Assert.Contains("href=\"/\"", html);
            Assert.DoesNotContain("private", html);
        }
    }

    [SqlServerFact]
    public async Task UnknownUsernamesStillVerifyAPasswordHashAndCannotSignIn()
    {
        var hasher = new RecordingHasher();
        await using var parent = new TestApp();
        await parent.InitializeAsync();
        await using var configured = parent.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPasswordHasher<Employee>>();
            services.AddSingleton<IPasswordHasher<Employee>>(hasher);
        }));
        using var client = configured.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        foreach (var username in new[] { "missing.employee", "avery.chen" })
        {
            var before = hasher.Verifications;
            var response = await TestApp.LoginAsync(client, username, "wrong-password");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Unable to sign in", await response.Content.ReadAsStringAsync());
            Assert.Equal(before + 1, hasher.Verifications);
            Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Overview")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Redirect, (await TestApp.LoginAsync(client)).StatusCode);
        using var fresh = configured.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var success = await TestApp.LoginAsync(fresh);
        var session = success.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("BlueBox.Session=", StringComparison.Ordinal));
        Assert.Contains("secure", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", session, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingHasher : IPasswordHasher<Employee>
    {
        private readonly PasswordHasher<Employee> inner = new();
        public int Verifications { get; private set; }
        public string HashPassword(Employee user, string password) => inner.HashPassword(user, password);
        public PasswordVerificationResult VerifyHashedPassword(Employee user, string hashedPassword, string providedPassword)
        {
            Verifications++;
            return inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }

    [SqlServerFact]
    public async Task CatalogInventoryAndManagerListsReachEveryMatchingRowAndKeepFilters()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        await app.ExecuteAsync("""
            INSERT INTO dbo.ITEM (item_name,item_type,media_format,item_status)
            SELECT TOP (201) CONCAT('Paging fixture ', RIGHT(CONCAT('000', ROW_NUMBER() OVER (ORDER BY object_id)), 3)),
                'MOVIE','DVD','ACTIVE' FROM sys.all_objects ORDER BY object_id;
            """);
        await using var scope = app.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<InventoryQueries>();
        var catalog = new List<CatalogRow>(); var inventory = new List<InventoryRow>();
        for (var number = 1; number <= 3; number++)
        {
            var page = await queries.CatalogAsync("Paging fixture", "MOVIE", number);
            var balances = await queries.InventoryAsync("Paging fixture", "MOVIE", 1, pageNumber: number);
            Assert.Equal(number < 3, page.HasNextPage); Assert.Equal(number < 3, balances.HasNextPage);
            Assert.Equal(number < 3 ? 100 : 1, page.Rows.Count);
            Assert.Equal(page.Rows.Select(row => row.ItemId), balances.Rows.Select(row => row.ItemId));
            Assert.All(balances.Rows, row => { Assert.Equal(1, row.LocationId); Assert.Equal(0, row.OnHand); });
            catalog.AddRange(page.Rows); inventory.AddRange(balances.Rows);
        }
        Assert.Equal(201, catalog.Select(row => row.ItemId).Distinct().Count());
        Assert.Equal(201, inventory.Select(row => (row.ItemId, row.LocationId)).Distinct().Count());
        Assert.Equal("Paging fixture 001", catalog[0].Name); Assert.Equal("Paging fixture 201", catalog[^1].Name);
        var allBalances = new List<InventoryRow>();
        for (var number = 1; number <= 5; number++)
        {
            var page = await queries.InventoryAsync("Paging fixture", "MOVIE", null, pageNumber: number);
            allBalances.AddRange(page.Rows);
            Assert.Equal(number < 5, page.HasNextPage);
        }
        Assert.Equal(402, allBalances.Count); // Both seeded warehouses, including zero stock.
        Assert.Equal(402, allBalances.Select(row => (row.ItemId, row.LocationId)).Distinct().Count());
        Assert.Empty((await queries.CatalogAsync("Paging fixture", "GAME")).Rows);
        Assert.Empty((await queries.InventoryAsync("Paging fixture", "MOVIE", 1, pageNumber: 4)).Rows);
        using var client = app.Browser(); await TestApp.LoginAsync(client, "jordan.lee");
        foreach (var path in new[] { "/Catalog?Search=Paging%20fixture&Type=MOVIE", "/Inventory?Search=Paging%20fixture&Type=MOVIE&LocationId=1", "/Admin/Items?Search=Paging%20fixture" })
        {
            var html = await client.GetStringAsync(path + "&PageNumber=2");
            Assert.Contains("Page 2", html); Assert.Contains("rel=\"prev\"", html); Assert.Contains("rel=\"next\"", html);
            var next = ElementUrl(html, "a", "rel", "next", "href");
            var nextUri = new Uri(client.BaseAddress!, next);
            var nextQuery = QueryHelpers.ParseQuery(nextUri.Query);
            Assert.True(nextQuery["PageNumber"] == "3", $"Wrong next-page URL on {path}: {next}");
            Assert.Equal("Paging fixture", nextQuery["Search"].ToString());
            Assert.Contains("Paging fixture 200", html); Assert.DoesNotContain("Paging fixture 201", html);
            if (path.StartsWith("/Inventory", StringComparison.Ordinal)) { Assert.Contains("LocationId=1", html); Assert.Contains("Type=MOVIE", html); }
            if (path.StartsWith("/Catalog", StringComparison.Ordinal)) Assert.Contains("Type=MOVIE", html);
            var last = await client.GetStringAsync(next);
            Assert.Contains("Paging fixture 201", last); Assert.DoesNotContain("rel=\"next\"", last);
            Assert.Contains("Page 1", await client.GetStringAsync(path + "&PageNumber=-1"));
            Assert.Contains("Page 1", await client.GetStringAsync(path)); // Applying filters starts at page one.
        }
        var newItemHtml = await client.GetStringAsync("/Admin/Items?Search=Paging%20fixture&PageNumber=2");
        var newAction = ItemFormAction(newItemHtml);
        Assert.Equal("/Admin/Items", new Uri(client.BaseAddress!, newAction).AbsolutePath);
        Assert.Equal("Paging fixture", QueryHelpers.ParseQuery(new Uri(client.BaseAddress!, newAction).Query)["Search"].ToString());
        Assert.Equal("2", QueryHelpers.ParseQuery(new Uri(client.BaseAddress!, newAction).Query)["PageNumber"].ToString());
        var token = await TestApp.TokenAsync(client, newAction);
        var invalid = await client.PostAsync(newAction, new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.Contains("Page 2", await invalid.Content.ReadAsStringAsync());
        var editUrl = $"/Admin/Items/{catalog[100].ItemId}?Search=Paging%20fixture&PageNumber=2";
        var editHtml = await client.GetStringAsync(editUrl);
        var newLink = ElementUrl(editHtml, "a", "id", "new-item-link", "href");
        Assert.Equal("/Admin/Items", new Uri(client.BaseAddress!, newLink).AbsolutePath);
        Assert.Contains("<h2>New item</h2>", await client.GetStringAsync(newLink));
        var editAction = ItemFormAction(editHtml);
        Assert.Equal($"/Admin/Items/{catalog[100].ItemId}", new Uri(client.BaseAddress!, editAction).AbsolutePath);
        Assert.Equal("Paging fixture", QueryHelpers.ParseQuery(new Uri(client.BaseAddress!, editAction).Query)["Search"].ToString());
        Assert.Equal("2", QueryHelpers.ParseQuery(new Uri(client.BaseAddress!, editAction).Query)["PageNumber"].ToString());
        var countBeforeEdit = await app.ScalarAsync("SELECT COUNT(*) FROM dbo.ITEM");
        token = await TestApp.TokenAsync(client, editAction);
        var saved = await client.PostAsync(editAction, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Input.Name"] = catalog[100].Name, ["Input.Type"] = "MOVIE", ["Input.Format"] = "DVD", ["Input.Status"] = "ACTIVE", ["Input.AgeRating"] = "PG"
        }));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Contains("PageNumber=2", saved.Headers.Location!.OriginalString);
        Assert.Contains("Search=Paging%20fixture", saved.Headers.Location.OriginalString);
        Assert.Equal(countBeforeEdit, await app.ScalarAsync("SELECT COUNT(*) FROM dbo.ITEM"));
        Assert.Equal("PG", (await queries.ItemAsync(catalog[100].ItemId))!.AgeRating);
        // An exact final page of 100 rows must not advertise a phantom next page.
        await app.ExecuteAsync("DELETE FROM dbo.ITEM WHERE item_name='Paging fixture 201'");
        Assert.False((await queries.CatalogAsync("Paging fixture", "MOVIE", 2)).HasNextPage);
        Assert.False((await queries.InventoryAsync("Paging fixture", "MOVIE", 1, pageNumber: 2)).HasNextPage);
    }

    private static string ItemFormAction(string html) => ElementUrl(html, "form", "id", "item-form", "action");

    private static string ElementUrl(string html, string element, string attribute, string value, string urlAttribute)
    {
        var tag = Regex.Match(html, $"<{element}\\b(?=[^>]*\\b{attribute}=\"{value}\")[^>]*>",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.True(tag.Success, $"The rendered {element} with {attribute}={value} must be present.");
        var url = Regex.Match(tag.Value, $"\\b{urlAttribute}=\"([^\"]+)\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.True(url.Success, $"The {element} must have an explicit {urlAttribute} URL: {tag.Value}");
        return WebUtility.HtmlDecode(url.Groups[1].Value);
    }
}
