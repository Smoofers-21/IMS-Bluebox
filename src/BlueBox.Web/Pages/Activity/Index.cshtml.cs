using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBox.Web.Pages.Activity;

public sealed class IndexModel(InventoryQueries queries) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Direction { get; set; }
    [BindProperty(SupportsGet = true)] public int? LocationId { get; set; }
    public List<Location> Locations { get; private set; } = [];
    public List<ActivityRow> Rows { get; private set; } = [];
    public async Task OnGetAsync(CancellationToken ct)
    {
        Locations = await queries.LocationsAsync(ct: ct);
        Rows = await queries.ActivityAsync(Search, Direction, LocationId, ct);
    }
}
