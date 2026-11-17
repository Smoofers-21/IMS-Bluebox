using BlueBox.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBox.Web.Pages;

public sealed class OverviewModel(OverviewQueries overview) : PageModel
{
    public OverviewSummary Summary { get; private set; } = new(0, 0, 0, 0);
    public List<WarehouseTotal> Warehouses { get; private set; } = [];
    public List<ActivityRow> Recent { get; private set; } = [];
    public async Task OnGetAsync(CancellationToken ct)
    {
        var snapshot = await overview.LoadAsync(ct);
        Summary = snapshot.Summary;
        Warehouses = snapshot.Warehouses;
        Recent = snapshot.Recent;
    }
}
