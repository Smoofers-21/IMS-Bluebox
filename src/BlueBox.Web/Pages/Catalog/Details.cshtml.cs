using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBox.Web.Pages.Catalog;

public sealed class DetailsModel(InventoryQueries queries) : PageModel
{
    public Item Item { get; private set; } = null!;
    public List<InventoryRow> Balances { get; private set; } = [];
    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var item = await queries.ItemAsync(id, ct);
        if (item is null) return NotFound();
        Item = item;
        Balances = (await queries.InventoryAsync(null, null, null, id, ct: ct)).Rows;
        return Page();
    }
}
