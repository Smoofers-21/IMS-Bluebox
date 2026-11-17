using System.Security.Claims;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBox.Web.Pages.Admin;

public sealed class EmployeesModel(AdministrationService admin) : PageModel
{
    [BindProperty(SupportsGet = true), FromRoute] public int? Id { get; set; }
    [BindProperty] public EmployeeInput Input { get; set; } = new();
    public List<EmployeeSummary> Employees { get; private set; } = [];
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Input = new() { Role = "STAFF", Status = "ACTIVE" };
        Employees = await admin.EmployeesAsync(ct);
        if (Id.HasValue)
        {
            var employee = Employees.Find(e => e.Id == Id);
            if (employee is null) return NotFound();
            Input = new() { FirstName = employee.FirstName, LastName = employee.LastName, Username = employee.Username, Role = employee.Role, Status = employee.Status };
        }
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!Id.HasValue && string.IsNullOrEmpty(Input.Password)) ModelState.AddModelError("Input.Password", "A password is required for a new account.");
        if (ModelState.IsValid)
        {
            var actor = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, System.Globalization.CultureInfo.InvariantCulture);
            var result = await admin.SaveEmployeeAsync(actor, Id, Input, ct);
            if (result.Success)
            {
                if (Id == actor)
                {
                    await HttpContext.SignOutAsync();
                    TempData["Notice"] = Input.Status == "ACTIVE"
                        ? "Your account was updated. Sign in again with your current credentials."
                        : "Your account is no longer active. Contact another manager if access is needed.";
                    return RedirectToPage("/Account/Login");
                }
                TempData["Success"] = result.Message;
                return Id.HasValue ? RedirectToPage("/Admin/Employees", new { id = Id.Value }) : RedirectToPage("/Admin/Employees");
            }
            ModelState.AddModelError("", result.Message);
        }
        Input.Password = null;
        if (ModelState.TryGetValue("Input.Password", out var passwordState))
        {
            passwordState.RawValue = null;
            passwordState.AttemptedValue = null;
        }
        Employees = await admin.EmployeesAsync(ct);
        return Page();
    }
}
