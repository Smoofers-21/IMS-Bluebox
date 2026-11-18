using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBox.Web.Pages.Inventory;

public sealed class IndexModel(InventoryQueries queries) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Type { get; set; }
    [BindProperty(SupportsGet = true)] public int? LocationId { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public bool HasNextPage { get; private set; }
    public List<Location> Locations { get; private set; } = [];
    public List<InventoryRow> Rows { get; private set; } = [];
    public async Task OnGetAsync(CancellationToken ct)
    {
        Locations = await queries.LocationsAsync(ct: ct);
        PageNumber = Math.Max(1, PageNumber);
        var page = await queries.InventoryAsync(Search, Type, LocationId, pageNumber: PageNumber, ct: ct);
        Rows = page.Rows;
        HasNextPage = page.HasNextPage;
    }
}
