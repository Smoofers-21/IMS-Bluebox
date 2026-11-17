using BlueBox.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BlueBox.Web.Pages.Catalog;

public sealed class IndexModel(InventoryQueries queries) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Type { get; set; }
    public List<CatalogRow> Rows { get; private set; } = [];
    public async Task OnGetAsync(CancellationToken ct) => Rows = await queries.Catalog(Search?.Trim(), Type).OrderBy(i => i.Name).ThenBy(i => i.Format).Take(100).ToListAsync(ct);
}
