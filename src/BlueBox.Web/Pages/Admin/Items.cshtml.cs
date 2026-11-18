using System.Security.Claims;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

namespace BlueBox.Web.Pages.Admin;

public sealed class ItemsModel(AdministrationService admin, InventoryQueries queries, OmdbClient movies) : PageModel
{
    [BindProperty(SupportsGet = true), FromRoute] public int? Id { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public bool HasNextPage { get; private set; }
    [BindProperty] public ItemInput Input { get; set; } = new();
    public bool HasHistory { get; private set; }
    public List<CatalogRow> Items { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Input = new() { Type = "MOVIE", Status = "ACTIVE" };
        if (Id.HasValue)
        {
            var item = await queries.ItemAsync(Id.Value, ct);
            if (item is null) return NotFound();
            Input = new() { Name = item.ItemName, Type = item.ItemType, Format = item.MediaFormat, PosterUrl = item.PosterUrl, AgeRating = item.AgeRating, Status = item.ItemStatus };
        }
        HasHistory = Id.HasValue && await queries.ItemHasHistoryAsync(Id.Value, ct);
        await LoadItemsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await admin.SaveItemAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, System.Globalization.CultureInfo.InvariantCulture), Id, Input, ct);
            if (result.Success)
            {
                TempData["Success"] = result.Message;
                var values = new RouteValueDictionary();
                if (Id.HasValue) values["id"] = Id.Value;
                if (!string.IsNullOrWhiteSpace(Search)) values["Search"] = Search;
                if (PageNumber > 1) values["PageNumber"] = PageNumber;
                return RedirectToPage("/Admin/Items", values);
            }
            ModelState.AddModelError("", result.Message);
        }
        HasHistory = Id.HasValue && await queries.ItemHasHistoryAsync(Id.Value, ct);
        await LoadItemsAsync(ct);
        return Page();
    }

    private async Task LoadItemsAsync(CancellationToken ct)
    {
        PageNumber = Math.Max(1, PageNumber);
        var page = await queries.CatalogAsync(Search, null, PageNumber, ct);
        Items = page.Rows;
        HasNextPage = page.HasNextPage;
    }

    public async Task<JsonResult> OnGetLookupAsync(string? title, CancellationToken ct) => new(await movies.SearchAsync(title, ct));
    public async Task<JsonResult> OnGetMovieAsync(string? movieId, CancellationToken ct) => new(await movies.DetailAsync(movieId, ct));
}
