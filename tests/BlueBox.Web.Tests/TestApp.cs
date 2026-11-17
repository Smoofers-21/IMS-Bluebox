using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BlueBox.Web.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BLUEBOX_TEST_SQLSERVER")))
            Skip = "Set BLUEBOX_TEST_SQLSERVER to a disposable test instance to run real SQL Server integration checks.";
    }
}
public sealed class SqlServerTheoryAttribute : TheoryAttribute
{
    public SqlServerTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BLUEBOX_TEST_SQLSERVER")))
            Skip = "Set BLUEBOX_TEST_SQLSERVER to a disposable test instance to run real SQL Server integration checks.";
    }
}

public sealed partial class TestApp : WebApplicationFactory<Program>
{
    public const string Password = "Local-test-password-42!";
    private readonly string? sqlServer = Environment.GetEnvironmentVariable("BLUEBOX_TEST_SQLSERVER");
    private readonly string databaseName = $"BlueBoxTest_{Guid.NewGuid():N}";
    private bool databaseCreated;
    public ConcurrentQueue<Exception> ServerErrors { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.AddProvider(new ExceptionCapture(ServerErrors)));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<SqlDatabase>();
            services.AddScoped(sp => new SqlDatabase(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:BlueBox"] = sqlServer is null ? "Server=localhost;Database=Unused;Integrated Security=True" : ConnectionString(databaseName)
            }).Build(), sp.GetRequiredService<ILogger<SqlDatabase>>()));
        });
    }

    private sealed class ExceptionCapture(ConcurrentQueue<Exception> errors) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(errors);
        public void Dispose() { }
        private sealed class CaptureLogger(ConcurrentQueue<Exception> errors) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (exception is not null && logLevel >= LogLevel.Error) errors.Enqueue(exception);
            }
        }
    }

    public async Task InitializeAsync(string? password = null)
    {
        if (sqlServer is null) throw new InvalidOperationException("SQL Server tests require a disposable instance; no fake SQL backend is used.");
        await using var master = new SqlConnection(ConnectionString("master"));
        await master.OpenAsync();
        await using var create = new SqlCommand($"CREATE DATABASE [{databaseName}]", master);
        await create.ExecuteNonQueryAsync(); databaseCreated = true;
        await using var target = new SqlConnection(ConnectionString(databaseName)); await target.OpenAsync();
        using var reader = new StreamReader(typeof(TestApp).Assembly.GetManifestResourceStream("BlueBox.CurrentSchema.sql")!);
        foreach (var batch in Regex.Split(await reader.ReadToEndAsync(), @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            await using var command = new SqlCommand(batch, target) { CommandTimeout = 60 };
            await command.ExecuteNonQueryAsync();
        }
        await using var scope = Services.CreateAsyncScope();
        await DemoSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<SqlDatabase>(), scope.ServiceProvider.GetRequiredService<IPasswordHasher<Employee>>(), password ?? Password);
    }

    public async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = new SqlConnection(ConnectionString(databaseName)); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); return await command.ExecuteScalarAsync();
    }
    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(ConnectionString(databaseName)); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); await command.ExecuteNonQueryAsync();
    }
    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
    public static async Task<string> TokenAsync(HttpClient client, string page)
    {
        var response = await client.GetAsync(page); response.EnsureSuccessStatusCode();
        return TokenPattern().Match(await response.Content.ReadAsStringAsync()).Groups[1].Value;
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
    private string ConnectionString(string database) => new SqlConnectionStringBuilder(sqlServer!) { InitialCatalog = database, Pooling = false }.ConnectionString;
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (!databaseCreated) return;
        await using var master = new SqlConnection(ConnectionString("master")); await master.OpenAsync();
        await using var drop = new SqlCommand($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]", master);
        await drop.ExecuteNonQueryAsync();
    }
}
