using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.SqlClient;

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
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromHours(4);
            options.EventsType = typeof(EmployeeCookieEvents);
        });
        builder.Services.AddAuthorization(options => options.AddPolicy("Manager", policy => policy.RequireRole("MANAGER")));
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Runs before authentication, so rejected bursts do not trigger cookie/database validation.
            // Partition by client address: the public STAFF account is shared by many visitors.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
                    }));
            options.AddPolicy("login", context => HttpMethods.IsPost(context.Request.Method)
                ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
                    })
                : RateLimitPartition.GetNoLimiter("login-read"));
            options.AddPolicy("stock-movement", context => HttpMethods.IsPost(context.Request.Method)
                ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
                    })
                : RateLimitPartition.GetNoLimiter("movement-read"));
            options.OnRejected = async (rejected, ct) =>
            {
                rejected.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    rejected.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
                        .ToString(System.Globalization.CultureInfo.InvariantCulture);
                await rejected.HttpContext.Response.WriteAsync("Too many requests. Wait a minute and try again.", ct);
            };
        });
        builder.Services.AddRazorPages(options =>
        {
            options.Conventions.AuthorizeFolder("/");
            options.Conventions.AuthorizeFolder("/Admin", "Manager");
            options.Conventions.AuthorizePage("/Activity/Correct", "Manager");
            options.Conventions.AllowAnonymousToPage("/Account/Login");
            options.Conventions.AddPageApplicationModelConvention("/Account/Login",
                model => model.EndpointMetadata.Add(new EnableRateLimitingAttribute("login")));
            options.Conventions.AddPageApplicationModelConvention("/Inventory/Move",
                model => model.EndpointMetadata.Add(new EnableRateLimitingAttribute("stock-movement")));
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

        // Host integration may already handle forwarding (for example IIS). For other
        // verified proxies, opt in with exact addresses; never trust arbitrary headers.
        if (TrustedProxyConfiguration.Read(app.Configuration) is { } proxyOptions)
            app.UseForwardedHeaders(proxyOptions);

        app.Use(async (context, next) =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers["X-XSS-Protection"] = "0";
            context.Response.Headers["Referrer-Policy"] = "same-origin";
            context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            context.Response.Headers.XFrameOptions = "DENY";
            context.Response.Headers.ContentSecurityPolicy = "default-src 'self';script-src 'self';style-src 'self';img-src 'self' https: data:;font-src 'self';connect-src 'self';frame-ancestors 'none';base-uri 'self';form-action 'self';";
            context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            context.Response.Headers.CacheControl = "no-store";
            await next(context);
        });
        app.UseExceptionHandler("/Error");
        if (!app.Environment.IsDevelopment()) app.UseHsts();
        app.UseHttpsRedirection();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context =>
            {
                var request = context.Context.Request;
                var versions = context.Context.RequestServices.GetRequiredService<IFileVersionProvider>();
                var versioned = request.Query.TryGetValue("v", out var version) && version.Count == 1
                    && !string.IsNullOrEmpty(version[0])
                    && versions.AddFileVersionToPath(request.PathBase, request.Path.Value!)
                        == QueryHelpers.AddQueryString(request.Path.Value!, "v", version[0]!);
                context.Context.Response.Headers.CacheControl = versioned
                    ? "public, max-age=31536000, immutable" : "public, max-age=3600";
            }
        });
        app.UseRouting();
        app.UseRateLimiter();
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

    internal static IResult HandleError(HttpContext context, ILogger<Program> logger)
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        if (exception is not null) logger.LogError(exception, "Unable to complete a BlueBox request");
        var unavailable = exception is SqlException or TimeoutException;
        return Results.Content(unavailable ? """
            <!doctype html><html lang="en"><meta charset="utf-8"><title>BlueBox unavailable</title>
            <h1>BlueBox is temporarily unavailable</h1>
            <p>Your request could not be completed. Check the connection or try again shortly.</p>
            <a href="/">Return to overview</a></html>
            """ : """
            <!doctype html><html lang="en"><meta charset="utf-8"><title>BlueBox request failed</title>
            <h1>Your request could not be completed</h1>
            <p>Try again shortly. If the problem continues, contact your manager.</p>
            <a href="/">Return to overview</a></html>
            """, "text/html; charset=utf-8", statusCode: unavailable
                ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError);
    }
}
