using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BlueBox.Web.Pages.Activity;

public sealed class IndexModel(BlueBoxDbContext db, InventoryQueries queries) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Direction { get; set; }
    [BindProperty(SupportsGet = true)] public int? LocationId { get; set; }
    public List<Location> Locations { get; private set; } = [];
    public List<ActivityRow> Rows { get; private set; } = [];
    public async Task OnGetAsync(CancellationToken ct)
    {
        Locations = await db.Locations.AsNoTracking().OrderBy(l => l.LocationName).ToListAsync(ct);
        Rows = await queries.Activity(Search?.Trim(), Direction, LocationId).OrderByDescending(t => t.Time).ThenByDescending(t => t.Id).Take(100).ToListAsync(ct);
    }
}
