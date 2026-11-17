using System.Security.Claims;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBox.Web.Pages.Admin;

public sealed class LocationsModel(AdministrationService admin, InventoryQueries queries) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public LocationInput Input { get; set; } = new();
    public List<Location> Locations { get; private set; } = [];
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Input = new() { Status = "ACTIVE" };
        Locations = await queries.LocationsAsync(ct: ct);
        if (Id.HasValue)
        {
            var location = Locations.Find(l => l.LocationId == Id);
            if (location is null) return NotFound();
            Input = new() { Name = location.LocationName, Address = location.Address, Status = location.LocationStatus };
        }
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await admin.SaveLocationAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, System.Globalization.CultureInfo.InvariantCulture), Id, Input, ct);
            if (result.Success)
            {
                TempData["Success"] = result.Message;
                return Id.HasValue ? RedirectToPage("/Admin/Locations", new { id = Id.Value }) : RedirectToPage("/Admin/Locations");
            }
            ModelState.AddModelError("", result.Message);
        }
        Locations = await queries.LocationsAsync(ct: ct);
        return Page();
    }
}
