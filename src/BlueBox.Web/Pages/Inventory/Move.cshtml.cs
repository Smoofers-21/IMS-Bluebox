using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BlueBox.Web.Pages.Inventory;

public sealed class MoveModel(BlueBoxDbContext db, StockService stock) : PageModel
{
    public sealed class MovementInput
    {
        [Required, Range(1, int.MaxValue)] public int? ItemId { get; set; }
        [Required, Range(1, int.MaxValue)] public int? LocationId { get; set; }
        [Required, RegularExpression("^(CHECK IN|CHECK OUT)$")] public string Direction { get; set; } = "CHECK OUT";
        [Required, Range(1, int.MaxValue)] public int? Quantity { get; set; } = 1;
        [StringLength(255)] public string? Notes { get; set; }
    }
    [BindProperty] public MovementInput Input { get; set; } = new();
    public List<Item> Items { get; private set; } = [];
    public List<Location> Locations { get; private set; } = [];
    public object[] Balances { get; private set; } = [];

    public async Task OnGetAsync(int? itemId, int? locationId, CancellationToken ct)
    {
        Input.ItemId = itemId;
        Input.LocationId = locationId;
        await LoadAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var employeeId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, System.Globalization.CultureInfo.InvariantCulture);
            var result = await stock.RecordAsync(new MovementRequest(employeeId, Input.ItemId!.Value, Input.LocationId!.Value, Input.Direction, Input.Quantity!.Value, Input.Notes), ct);
            if (result.Success)
            {
                TempData["Success"] = $"Stock movement recorded. New warehouse balance: {result.Balance}.";
                return RedirectToPage("Index", new { locationId = Input.LocationId });
            }
            ModelState.AddModelError("", result.Message);
        }
        await LoadAsync(ct);
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Items = await db.Items.AsNoTracking().Where(i => i.ItemStatus == "ACTIVE").OrderBy(i => i.ItemName).ThenBy(i => i.MediaFormat).ToListAsync(ct);
        Locations = await db.Locations.AsNoTracking().Where(l => l.LocationStatus == "ACTIVE").OrderBy(l => l.LocationName).ToListAsync(ct);
        Balances = (await db.Inventory.AsNoTracking().Select(i => new { i.ItemId, i.LocationId, Quantity = i.QuantityOnHand }).ToListAsync(ct)).Cast<object>().ToArray();
    }
}
