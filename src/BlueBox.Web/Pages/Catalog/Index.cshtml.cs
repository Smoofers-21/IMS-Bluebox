using BlueBox.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBox.Web.Pages.Catalog;

public sealed class IndexModel(InventoryQueries queries) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Type { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public bool HasNextPage { get; private set; }
    public List<CatalogRow> Rows { get; private set; } = [];
    public async Task OnGetAsync(CancellationToken ct)
    {
        PageNumber = Math.Max(1, PageNumber);
        var page = await queries.CatalogAsync(Search, Type, PageNumber, ct);
        Rows = page.Rows;
        HasNextPage = page.HasNextPage;
    }
}
