using System.Text.RegularExpressions;
using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace BlueBox.Web;

// Program.Main and MapRoutes follow the instructor's WebAppWithDB_Starter_v2.
// Database helpers live in Data/SqlDatabase.cs so routes/pages share one implementation.
public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        // Optional private file, outside the checkout; never commit its contents.
        if (Environment.GetEnvironmentVariable("BLUEBOX_PRIVATE_CONFIG") is { Length: > 0 } privateFile)
            builder.Configuration.AddJsonFile(privateFile, optional: false, reloadOnChange: false);
        builder.Services.AddLogging(logging => logging.AddConsole());
        builder.Services.AddScoped<SqlDatabase>();
        builder.Services.AddScoped<EmployeeRepository>();
        builder.Services.AddScoped<StockService>();
        builder.Services.AddScoped<InventoryQueries>();
        builder.Services.AddScoped<AdministrationService>();
        builder.Services.AddScoped<OverviewQueries>();
        builder.Services.AddHttpClient<OmdbClient>(client => client.Timeout = TimeSpan.FromSeconds(5)).RemoveAllLoggers();
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
            options.Conventions.AuthorizeFolder("/Admin", "Manager");
            options.Conventions.AuthorizePage("/Activity/Correct", "Manager");
            options.Conventions.AllowAnonymousToPage("/Account/Login");
        });
        var app = builder.Build();

        if (args.Contains("--create-manager", StringComparer.Ordinal))
        {
            await using var scope = app.Services.CreateAsyncScope();
            await ManagerBootstrap.CreateAsync(scope.ServiceProvider.GetRequiredService<SqlDatabase>(),
                scope.ServiceProvider.GetRequiredService<IPasswordHasher<Employee>>(),
                app.Configuration["Bootstrap:Username"] ?? "", app.Configuration["Bootstrap:Password"] ?? "");
            Console.WriteLine("Initial manager created. Remove Bootstrap credentials from private configuration.");
            return;
        }
        if (args.Contains("--seed-demo", StringComparer.Ordinal))
        {
            await using var scope = app.Services.CreateAsyncScope();
            var password = app.Configuration["Demo:Password"];
            if (string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("Set Demo:Password privately before running --seed-demo.");
            await DemoSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<SqlDatabase>(),
                scope.ServiceProvider.GetRequiredService<IPasswordHasher<Employee>>(), password);
            Console.WriteLine("Demo data created. Active accounts: jordan.lee (manager), avery.chen (staff). sam.rivera is inactive.");
            return;
        }

        app.Use(async (context, next) =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
            context.Response.Headers.XFrameOptions = "DENY";
            context.Response.Headers.ContentSecurityPolicy = "default-src 'self';script-src 'self';style-src 'self';img-src 'self' https: data:;font-src 'self';connect-src 'self';frame-ancestors 'none';base-uri 'self';form-action 'self';";
            context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            context.Response.Headers.CacheControl = "no-store";
            await next(context);
        });
        app.UseExceptionHandler("/Error");
        if (!app.Environment.IsDevelopment()) app.UseHsts();
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        MapRoutes(app);
        app.MapRazorPages();
        await app.RunAsync();
    }

    private static void MapRoutes(WebApplication app)
    {
        // Configure this to the instructor's assigned username path when needed.
        // It is a route name, not a credential; never publish database passwords.
        var route = app.Configuration["Database:RoutePrefix"] ?? "bluebox";
        if (!Regex.IsMatch(route, "^[A-Za-z0-9_-]{1,50}$", RegexOptions.CultureInvariant)
            || new[] { "Inventory", "Catalog", "Activity", "Account", "Admin", "Overview", "Error", "api" }.Contains(route, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Database:RoutePrefix must contain 1–50 letters, digits, underscores, or hyphens.");
        app.MapGet("/", () => Results.Redirect($"/{route}", permanent: false));
        app.MapGet($"/{route}", HandleRoot);
        app.MapMethods("/Error", ["GET", "POST"], HandleError);
        app.MapGet("/api/inventory/balance", async (int itemId, int locationId, InventoryQueries queries, CancellationToken ct) =>
        {
            if (itemId <= 0 || locationId <= 0) return Results.BadRequest();
            return Results.Ok(new { quantity = await queries.BalanceAsync(itemId, locationId, ct) });
        }).RequireAuthorization();
    }

    private static IResult HandleRoot(HttpContext context) => Results.Redirect(
        context.User.Identity?.IsAuthenticated == true ? "/Overview" : "/Account/Login");

    private static IResult HandleError(HttpContext context, ILogger<Program> logger)
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        if (exception is not null) logger.LogError(exception, "Unable to complete a BlueBox request");
        return Results.Content("""
            <!doctype html><html lang="en"><meta charset="utf-8"><title>BlueBox unavailable</title>
            <h1>BlueBox is temporarily unavailable</h1>
            <p>Your request could not be completed. Check the connection or try again shortly.</p>
            <a href="/Inventory">Return to inventory</a></html>
            """, "text/html; charset=utf-8", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
