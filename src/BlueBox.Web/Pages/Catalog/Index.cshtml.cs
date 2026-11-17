using BlueBox.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBox.Web.Pages.Catalog;

public sealed class IndexModel(InventoryQueries queries) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Type { get; set; }
    public List<CatalogRow> Rows { get; private set; } = [];
    public async Task OnGetAsync(CancellationToken ct) => Rows = await queries.CatalogAsync(Search, Type, ct);
}
