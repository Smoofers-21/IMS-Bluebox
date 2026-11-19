using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace BlueBox.Web.Services;

internal static class TrustedProxyConfiguration
{
    public static ForwardedHeadersOptions? Read(IConfiguration configuration)
    {
        var configured = configuration["Hosting:KnownProxies"];
        if (string.IsNullOrWhiteSpace(configured)) return null;

        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1
        };
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var value in configured.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!IPAddress.TryParse(value, out var address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
                throw new InvalidOperationException("Hosting:KnownProxies must contain exact proxy IP addresses separated by semicolons.");
            options.KnownProxies.Add(address);
        }
        if (options.KnownProxies.Count == 0)
            throw new InvalidOperationException("Hosting:KnownProxies must contain at least one exact proxy IP address.");
        return options;
    }
}
