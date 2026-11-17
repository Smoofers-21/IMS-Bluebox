using System.ComponentModel.DataAnnotations;
using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBox.Web.Pages.Account;

public sealed class LoginModel(EmployeeRepository employees, IPasswordHasher<Employee> hasher) : PageModel
{
    [BindProperty, Required, StringLength(50)] public string Username { get; set; } = "";
    [BindProperty, Required, StringLength(PasswordPolicy.MaximumLength)] public string Password { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? RedirectToPage("/Overview") : Page();

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();
        var username = Username.Trim();
        var employee = await employees.FindByUsernameAsync(username, ct);
        var result = PasswordVerificationResult.Failed;
        if (employee is not null)
        {
            try { result = hasher.VerifyHashedPassword(employee, employee.PasswordHash, Password); }
            catch (FormatException) { /* Legacy or invalid hashes cannot authenticate. */ }
        }
        if (employee is null || result == PasswordVerificationResult.Failed || employee.EmploymentStatus != "ACTIVE"
            || employee.AccessRole is not ("STAFF" or "MANAGER"))
        {
            ModelState.AddModelError("", "Unable to sign in. Check your credentials or contact your manager.");
            return Page();
        }
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            employee.PasswordHash = hasher.HashPassword(employee, Password);
            await employees.UpdateHashAsync(employee.EmployeeId, employee.PasswordHash, ct);
        }
        await HttpContext.SignInAsync(EmployeeCookieEvents.CreatePrincipal(employee));
        return Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl!) : RedirectToPage("/Overview");
    }
}
