using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBox.Web.Pages.Account;

public sealed class LogoutModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Inventory/Index");
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync();
        return RedirectToPage("/Account/Login");
    }
}
