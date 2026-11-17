using System.Security.Claims;
using BlueBox.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace BlueBox.Web.Services;

public sealed class EmployeeCookieEvents(BlueBoxDbContext db) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var employee = int.TryParse(id, out var employeeId)
            ? await db.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.EmployeeId == employeeId, context.HttpContext.RequestAborted)
            : null;
        if (employee is null || employee.EmploymentStatus != "ACTIVE" || employee.AccessRole is not ("STAFF" or "MANAGER"))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync();
            return;
        }
        // Role changes take effect on the next request, not at cookie expiry.
        if (context.Principal?.FindFirstValue(ClaimTypes.Role) != employee.AccessRole || context.Principal.Identity?.Name != employee.DisplayName)
        {
            context.ReplacePrincipal(CreatePrincipal(employee));
            context.ShouldRenew = true;
        }
    }

    public static ClaimsPrincipal CreatePrincipal(Models.Employee employee) => new(new ClaimsIdentity(
    [
        new(ClaimTypes.NameIdentifier, employee.EmployeeId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        new(ClaimTypes.Name, employee.DisplayName),
        new(ClaimTypes.Role, employee.AccessRole)
    ], CookieAuthenticationDefaults.AuthenticationScheme));
}
