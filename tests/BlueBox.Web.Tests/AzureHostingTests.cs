using System.Net;
using System.Reflection;
using BlueBox.Web.Data;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BlueBox.Web.Tests;

public sealed class AzureHostingTests
{
    [Fact]
    public async Task TrustedProxyRestoresHttpsAndKeepsLoginLimitsSeparateByClient()
    {
        await using var app = new TestApp { KnownProxies = "192.0.2.10" };
        for (var attempt = 0; attempt < 10; attempt++)
            Assert.Equal(400, (await PostAsync(app, "192.0.2.10", "198.51.100.1", "http")).Response.StatusCode);
        var rejected = await PostAsync(app, "192.0.2.10", "198.51.100.1", "http");
        Assert.Equal(429, rejected.Response.StatusCode);
        Assert.Equal("https", rejected.Request.Scheme);
        Assert.Equal(IPAddress.Parse("198.51.100.1"), rejected.Connection.RemoteIpAddress);
        Assert.Equal(400, (await PostAsync(app, "192.0.2.10", "198.51.100.2", "http")).Response.StatusCode);
    }

    [Fact]
    public async Task UnknownProxyCannotSpoofHttpsOrBypassLoginLimits()
    {
        await using var app = new TestApp { KnownProxies = "192.0.2.10" };
        for (var attempt = 0; attempt < 10; attempt++)
            Assert.Equal(400, (await PostAsync(app, "192.0.2.99", $"198.51.100.{attempt + 1}", "https")).Response.StatusCode);
        Assert.Equal(429, (await PostAsync(app, "192.0.2.99", "198.51.100.100", "https")).Response.StatusCode);
        var context = await PostAsync(app, "192.0.2.98", "198.51.100.1", "http");
        Assert.Equal("http", context.Request.Scheme);
        Assert.Equal(IPAddress.Parse("192.0.2.98"), context.Connection.RemoteIpAddress);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("192.0.2.0/24")]
    [InlineData(";;;")]
    public void InvalidProxyTrustConfigurationIsRejected(string proxies)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Hosting:KnownProxies"] = proxies }).Build();
        Assert.Throws<InvalidOperationException>(() => TrustedProxyConfiguration.Read(configuration));
    }

    [Fact]
    public async Task TransientOpenFailureIsRetriedBeforeAnyCommandRuns()
    {
        using var connection = new SqlConnection(); var attempts = 0;
        await SqlDatabase.OpenWithRetryAsync(connection, () => ++attempts == 1
            ? Task.FromException(SqlError(40613)) : Task.CompletedTask, CancellationToken.None);
        Assert.Equal(2, attempts);
        using var command = connection.CreateCommand();
        Assert.True(command.RetryLogicProvider is null || command.RetryLogicProvider.RetryLogic.NumberOfTries == 1);
    }

    [Theory]
    [InlineData(18456)] // Authentication: retrying cannot fix a wrong password.
    [InlineData(4060)] // A wrong database name must not be treated as an idle resume.
    [InlineData(-2)] // Do not broadly retry timeouts.
    public async Task PermanentOpenFailureIsNotRetried(int number)
    {
        using var connection = new SqlConnection(); var attempts = 0; var error = SqlError(number);
        var actual = await Assert.ThrowsAsync<SqlException>(() => SqlDatabase.OpenWithRetryAsync(connection,
            () => { attempts++; return Task.FromException(error); }, CancellationToken.None));
        Assert.Same(error, actual); Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExhaustedOpenRetriesKeepTheSqlErrorForTheUnavailableResponse()
    {
        using var connection = new SqlConnection(); var attempts = 0; var error = SqlError(40613);
        var actual = await Assert.ThrowsAsync<SqlException>(() => SqlDatabase.OpenWithRetryAsync(connection,
            () => { attempts++; return Task.FromException(error); }, CancellationToken.None));
        Assert.Same(error, actual); Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task CancellationStopsOpenRetries()
    {
        using var connection = new SqlConnection(); using var cancellation = new CancellationTokenSource(); var attempts = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SqlDatabase.OpenWithRetryAsync(connection,
            () => { attempts++; cancellation.Cancel(); return Task.FromException(SqlError(40613)); }, cancellation.Token));
        Assert.Equal(1, attempts);
    }

    private static Task<HttpContext> PostAsync(TestApp app, string proxy, string client, string scheme) => app.Server.SendAsync(context =>
    {
        context.Connection.RemoteIpAddress = IPAddress.Parse(proxy);
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString("localhost");
        context.Request.Method = "POST";
        context.Request.Path = "/Account/Login";
        context.Request.Headers["X-Forwarded-For"] = client;
        context.Request.Headers["X-Forwarded-Proto"] = "https";
    });

    // Inject provider errors without contacting any database. SqlException has no public constructor.
    private static SqlException SqlError(int number)
    {
        var error = (SqlError)Activator.CreateInstance(typeof(SqlError), BindingFlags.NonPublic | BindingFlags.Instance,
            null, [number, (byte)0, (byte)20, "test-server", "injected SQL error", "", 1, 0, null], null)!;
        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(errors, [error]);
        return (SqlException)typeof(SqlException).GetMethod("CreateException", BindingFlags.NonPublic | BindingFlags.Static,
            [typeof(SqlErrorCollection), typeof(string)])!.Invoke(null, [errors, "16.0"])!;
    }
}
