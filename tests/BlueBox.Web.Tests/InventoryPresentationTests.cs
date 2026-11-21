using System.Net;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlueBox.Web.Tests;

public sealed class InventoryPresentationTests
{
    [Theory]
    [InlineData(null, "MOVIE", false)]
    [InlineData("", "GAME", false)]
    [InlineData("https://example.com/movie.png", "MOVIE", true)]
    [InlineData("https://example.com/game.png", "GAME", true)]
    [InlineData("http://example.com/movie.png", "MOVIE", false)]
    [InlineData("javascript:alert(1)", "GAME", false)]
    [InlineData("https://user:password@example.com/movie.png", "MOVIE", false)]
    public async Task ThumbnailRendersOnlySafeHttpsPostersAndKeepsThePlaceholder(string? url, string type, bool imageExpected)
    {
        await using var app = new TestApp(); using var client = app.Browser();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var view = services.GetRequiredService<IRazorViewEngine>().GetView(null, "/Pages/Shared/_ItemThumbnail.cshtml", false);
        Assert.True(view.Success);
        var http = new DefaultHttpContext { RequestServices = services };
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var data = new ViewDataDictionary<ItemThumbnail>(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        { Model = new ItemThumbnail(url, type) };
        var tempData = new TempDataDictionary(http, services.GetRequiredService<ITempDataProvider>());
        using var output = new StringWriter();
        await view.View.RenderAsync(new ViewContext(action, view.View, data, tempData, output, new HtmlHelperOptions()));
        var html = output.ToString();
        Assert.Contains("mini-cover", html);
        Assert.Contains("aria-hidden=\"true\"", html);
        Assert.Equal(type == "GAME", html.Contains("mini-cover game", StringComparison.Ordinal));
        Assert.Equal(imageExpected, html.Contains("<img", StringComparison.Ordinal));
        if (imageExpected)
        {
            Assert.Contains($"src=\"{url}\"", html);
            Assert.Contains("referrerpolicy=\"no-referrer\"", html);
            Assert.Contains("loading=\"lazy\"", html);
            Assert.Contains("alt=\"\"", html);
            Assert.Contains("width=\"28\"", html);
            Assert.Contains("height=\"38\"", html);
        }
        Assert.DoesNotContain("onerror=", html);
    }

    [SqlServerFact]
    public async Task ListsExposeStoredPostersAndMovePageKeepsValidatedItemIdsAndPreselection()
    {
        await using var app = new TestApp(); await app.InitializeAsync();
        await app.ExecuteAsync("""
            UPDATE dbo.ITEM SET poster_url='https://example.com/movie.png' WHERE item_id=1;
            UPDATE dbo.ITEM SET poster_url='http://example.com/insecure.png' WHERE item_id=2;
            UPDATE dbo.ITEM SET poster_url='https://example.com/game.png' WHERE item_id=4;
            """);
        await using var scope = app.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<InventoryQueries>();
        Assert.Equal("https://example.com/movie.png", (await queries.CatalogAsync("1", null)).Rows.Single(row => row.ItemId == 1).PosterUrl);
        Assert.All((await queries.InventoryAsync(null, null, null, 1)).Rows, row => Assert.Equal("https://example.com/movie.png", row.PosterUrl));
        Assert.Null((await queries.CatalogAsync("3", null)).Rows.Single(row => row.ItemId == 3).PosterUrl);
        using var client = app.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await TestApp.LoginAsync(client)).StatusCode);
        foreach (var route in new[] { "/Catalog", "/Inventory", "/Inventory?LocationId=1" })
        {
            var html = await client.GetStringAsync(route);
            Assert.Contains("src=\"https://example.com/movie.png\"", html);
            Assert.Contains("src=\"https://example.com/game.png\"", html);
            Assert.DoesNotContain("http://example.com/insecure.png", html);
            Assert.Contains("/js/item-thumbnails.js?v=", html);
            Assert.DoesNotContain("onerror=", html);
        }
        var move = await client.GetStringAsync("/Inventory/Move?itemId=2&locationId=1");
        Assert.Contains("id=\"movement-item-search\"", move);
        Assert.Contains("aria-controls=\"movement-item-options\"", move);
        Assert.Contains("role=\"listbox\"", move);
        Assert.Contains("Search by title, format, or item ID", move);
        Assert.Contains("name=\"Input.ItemId\"", move);
        Assert.Matches("<option[^>]*selected=\"selected\"[^>]*>Moonlight Signal · DVD · #2</option>", move);
        Assert.Contains("/js/movement-item-search.js?v=", move);
        Assert.Contains("__RequestVerificationToken", move);
        var token = await TestApp.TokenAsync(client, "/Inventory/Move");
        var saved = await client.PostAsync("/Inventory/Move", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.ItemId"] = "2", ["Input.LocationId"] = "1", ["Input.Direction"] = "CHECK IN", ["Input.Quantity"] = "1",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(12, await app.ScalarAsync("SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id=2 AND location_id=1"));
        Assert.Empty(app.ServerErrors);
    }
}
