using BlueBox.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBox.Web.Pages;

public sealed class OverviewModel(OverviewQueries overview, InventoryQueries queries) : PageModel
{
    public OverviewSummary Summary { get; private set; } = new(0, 0, 0, 0);
    public List<WarehouseTotal> Warehouses { get; private set; } = [];
    public List<ActivityRow> Recent { get; private set; } = [];
    public async Task OnGetAsync(CancellationToken ct)
    {
        Summary = await overview.SummaryAsync(ct);
        Warehouses = await overview.WarehousesAsync(ct);
        Recent = (await queries.ActivityAsync(null, null, null, ct: ct)).Rows.Take(5).ToList();
    }
}
