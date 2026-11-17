using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BlueBox.Web.Pages.Catalog;

public sealed class DetailsModel(BlueBoxDbContext db, InventoryQueries queries) : PageModel
{
    public Item Item { get; private set; } = null!;
    public List<InventoryRow> Balances { get; private set; } = [];
    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var item = await db.Items.AsNoTracking().SingleOrDefaultAsync(i => i.ItemId == id, ct);
        if (item is null) return NotFound();
        Item = item;
        Balances = await queries.Inventory(null, null, null).Where(i => i.ItemId == id).OrderBy(i => i.Location).ToListAsync(ct);
        return Page();
    }
}
