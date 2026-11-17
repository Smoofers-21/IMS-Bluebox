using System.Security.Claims;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBox.Web.Pages.Admin;

public sealed class ItemsModel(AdministrationService admin, InventoryQueries queries, OmdbClient movies) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty] public ItemInput Input { get; set; } = new();
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
        Items = await queries.CatalogAsync(Search, null, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await admin.SaveItemAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, System.Globalization.CultureInfo.InvariantCulture), Id, Input, ct);
            if (result.Success) { TempData["Success"] = result.Message; return RedirectToPage(new { Id, Search }); }
            ModelState.AddModelError("", result.Message);
        }
        Items = await queries.CatalogAsync(Search, null, ct);
        return Page();
    }

    public async Task<JsonResult> OnGetLookupAsync(string? title, CancellationToken ct) => new(await movies.SearchAsync(title, ct));
    public async Task<JsonResult> OnGetMovieAsync(string? movieId, CancellationToken ct) => new(await movies.DetailAsync(movieId, ct));
}
