using System.Text.RegularExpressions;
using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Data.SqlClient;

namespace BlueBox.Web.Tests;

public sealed class FailureSwitch : SaveChangesInterceptor
{
    public bool FailHistory { get; set; }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (FailHistory && data.Context!.ChangeTracker.Entries<StockTransaction>().Any(e => e.State == EntityState.Added))
            throw new InvalidOperationException("Injected history-write failure for rollback test.");
        return ValueTask.FromResult(result);
    }
}

public sealed partial class TestApp : WebApplicationFactory<Program>
{
    public const string Password = "Local-test-password-42!";
    private readonly string path = Path.Combine(Path.GetTempPath(), $"bluebox-test-{Guid.NewGuid():N}.db");
    private readonly string? sqlServer = Environment.GetEnvironmentVariable("BLUEBOX_TEST_SQLSERVER");
    private readonly string databaseName = $"BlueBoxTest_{Guid.NewGuid():N}";
    private bool databaseCreated;
    public FailureSwitch Failures { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<BlueBoxDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<BlueBoxDbContext>>();
            services.AddDbContext<BlueBoxDbContext>(options =>
            {
                if (sqlServer is null) options.UseSqlite($"Data Source={path};Default Timeout=5");
                else options.UseSqlServer(SqlConnectionString(databaseName));
                options.AddInterceptors(Failures);
            });
        });
    }

    public async Task InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BlueBoxDbContext>();
        if (sqlServer is null) await db.Database.EnsureCreatedAsync(); // Test database only.
        else
        {
            // Never connect to or delete the database named in the input string.
            // Tests create and later delete only their own generated database.
            await using var master = new SqlConnection(SqlConnectionString("master"));
            await master.OpenAsync();
            await using var create = new SqlCommand($"CREATE DATABASE [{databaseName}]", master);
            await create.ExecuteNonQueryAsync();
            databaseCreated = true;
            await using var target = new SqlConnection(SqlConnectionString(databaseName));
            await target.OpenAsync();
            foreach (var resource in new[] { "BlueBox.Baseline.sql", "BlueBox.Update.sql" })
            {
                using var reader = new StreamReader(typeof(TestApp).Assembly.GetManifestResourceStream(resource)!);
                var script = (await reader.ReadToEndAsync()).Replace("YourBlueBoxDatabaseName", databaseName, StringComparison.Ordinal);
                await using var command = new SqlCommand(script, target) { CommandTimeout = 60 };
                await command.ExecuteNonQueryAsync();
            }
        }
        await DemoSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<Employee>>(), Password);
    }

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    public static async Task<string> TokenAsync(HttpClient client, string page)
    {
        var response = await client.GetAsync(page);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        return TokenPattern().Match(html).Groups[1].Value;
    }

    public static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string username = "avery.chen", string? password = null, string? returnUrl = null)
    {
        var token = await TokenAsync(client, "/Account/Login");
        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = username,
            ["Password"] = password ?? Password,
            ["ReturnUrl"] = returnUrl ?? "",
            ["__RequestVerificationToken"] = token
        }));
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();

    private string SqlConnectionString(string database) => new SqlConnectionStringBuilder(sqlServer!)
    {
        InitialCatalog = database,
        Pooling = false
    }.ConnectionString;

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (databaseCreated)
        {
            await using var master = new SqlConnection(SqlConnectionString("master"));
            await master.OpenAsync();
            await using var drop = new SqlCommand($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]", master);
            await drop.ExecuteNonQueryAsync();
        }
        foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
    }
}
