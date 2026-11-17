using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<BlueBoxDbContext>(options =>
{
    var connection = builder.Configuration.GetConnectionString("BlueBox");
    if (string.IsNullOrWhiteSpace(connection))
        throw new InvalidOperationException("Configure ConnectionStrings:BlueBox using user secrets or an environment variable. See docs/development.md.");
    options.UseSqlServer(connection);
});
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<InventoryQueries>();
builder.Services.AddScoped<EmployeeCookieEvents>();
builder.Services.AddScoped<IPasswordHasher<Employee>, PasswordHasher<Employee>>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "BlueBox.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(4);
    options.EventsType = typeof(EmployeeCookieEvents);
});
builder.Services.AddAuthorization(options => options.AddPolicy("Manager", policy => policy.RequireRole("MANAGER")));
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Error");
});
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();
var app = builder.Build();

if (args.Contains("--create-manager", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    await ManagerBootstrap.CreateAsync(scope.ServiceProvider.GetRequiredService<BlueBoxDbContext>(),
        scope.ServiceProvider.GetRequiredService<IPasswordHasher<Employee>>(),
        app.Configuration["Bootstrap:Username"] ?? "",
        app.Configuration["Bootstrap:Password"] ?? "");
    Console.WriteLine("Initial manager created. Remove Bootstrap credentials from local configuration after setup.");
    return;
}

if (args.Contains("--seed-demo", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    var password = app.Configuration["Demo:Password"];
    if (string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("Set Demo:Password privately before running --seed-demo.");
    await DemoSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<BlueBoxDbContext>(), scope.ServiceProvider.GetRequiredService<IPasswordHasher<Employee>>(), password);
    Console.WriteLine("Demo data created. Active accounts: jordan.lee (manager), avery.chen (staff). sam.rivera is inactive.");
    return;
}

app.UseExceptionHandler();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.Run();

public partial class Program;

// Log the cause server-side; do not expose database details or credentials.
public sealed class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception, "Unable to complete a BlueBox request");
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>BlueBox unavailable</title><h1>BlueBox is temporarily unavailable</h1><p>Your request could not be completed. Check the connection or try again shortly.</p><a href=\"/Inventory\">Return to inventory</a></html>", ct);
        return true;
    }
}
