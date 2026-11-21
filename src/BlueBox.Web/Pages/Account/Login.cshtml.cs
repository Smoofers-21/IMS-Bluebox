using System.ComponentModel.DataAnnotations;
using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBox.Web.Pages.Account;

public sealed class LoginModel(EmployeeRepository employees, IPasswordHasher<Employee> hasher, IConfiguration configuration) : PageModel
{
    // Display-only values: the normal database-backed login still verifies the account.
    public string? PublicDemoUsername => configuration["PublicDemo:Username"];
    public string? PublicDemoPassword => configuration["PublicDemo:Password"];
    public bool PublicDemoEnabled => !string.IsNullOrWhiteSpace(PublicDemoUsername)
        && !string.IsNullOrWhiteSpace(PublicDemoPassword);
    // One process-wide dummy credential avoids skipping the expensive hash check for unknown usernames.
    private static readonly Employee UnknownEmployee = new();
    private static readonly string DummyHash = new PasswordHasher<Employee>().HashPassword(UnknownEmployee,
        Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
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
        try { result = hasher.VerifyHashedPassword(employee ?? UnknownEmployee, employee?.PasswordHash ?? DummyHash, Password); }
        catch (FormatException) { /* Legacy or invalid hashes cannot authenticate. */ }
        if (employee is null || result == PasswordVerificationResult.Failed || employee.EmploymentStatus != "ACTIVE"
            || employee.AccessRole is not ("STAFF" or "MANAGER"))
        {
            ModelState.AddModelError("", "Unable to sign in. Check your credentials or contact your manager.");
            return Page();
        }
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var upgraded = hasher.HashPassword(employee, Password);
            if (!await employees.TryUpgradeHashAsync(employee.EmployeeId, employee.PasswordHash, upgraded, ct))
            {
                ModelState.AddModelError("", "Your account changed while signing in. Please try again.");
                return Page();
            }
            employee.PasswordHash = upgraded;
        }
        await HttpContext.SignInAsync(EmployeeCookieEvents.CreatePrincipal(employee));
        return Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl!) : RedirectToPage("/Overview");
    }
}
