using System.Globalization;

namespace BlueBox.Web.Models;

public static class DisplayTime
{
    private static readonly TimeZoneInfo Zone = ResolveZone(id =>
        TimeZoneInfo.TryFindSystemTimeZoneById(id, out var found) ? found : null);
    public static string ZoneLabel => Zone.Equals(TimeZoneInfo.Utc) ? "UTC" : "Central time (CST/CDT)";
    public static string Central(DateTime utc) => Format(utc, Zone);

    internal static TimeZoneInfo ResolveZone(Func<string, TimeZoneInfo?> find) =>
        find("America/Chicago") ?? find("Central Standard Time") ?? TimeZoneInfo.Utc;

    internal static string Format(DateTime utc, TimeZoneInfo zone)
    {
        utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
        var suffix = zone.Equals(TimeZoneInfo.Utc) ? " UTC" : zone.IsDaylightSavingTime(utc) ? " CDT" : " CST";
        return local.ToString("MMM d, yyyy · HH:mm", CultureInfo.InvariantCulture) + suffix;
    }
}
