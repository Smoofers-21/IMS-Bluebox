using System.Security.Claims;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBox.Web.Pages.Admin;

public sealed class EmployeesModel(AdministrationService admin) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
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
            var result = await admin.SaveEmployeeAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, System.Globalization.CultureInfo.InvariantCulture), Id, Input, ct);
            if (result.Success) { TempData["Success"] = result.Message; return RedirectToPage("/Admin/Employees", new { Id }); }
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
