using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBox.Web.Pages.Activity;

public sealed class CorrectModel(InventoryQueries queries, StockService stock) : PageModel
{
    [BindProperty(SupportsGet = true), FromRoute] public long Id { get; set; }
    [BindProperty, Required, StringLength(255)] public string Explanation { get; set; } = "";
    public ActivityRow Movement { get; private set; } = null!;
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var movement = await queries.MovementAsync(Id, ct);
        if (movement is null) return NotFound();
        Movement = movement;
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await stock.CorrectAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, System.Globalization.CultureInfo.InvariantCulture), Id, Explanation, ct);
            if (result.Success) { TempData["Success"] = result.Message; return RedirectToPage("Index"); }
            ModelState.AddModelError("", result.Message);
        }
        return await OnGetAsync(ct);
    }
}
