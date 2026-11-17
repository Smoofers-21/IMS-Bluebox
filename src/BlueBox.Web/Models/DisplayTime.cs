using System.Globalization;

namespace BlueBox.Web.Models;

public static class DisplayTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
    public static string Central(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
        return local.ToString("MMM d, yyyy · HH:mm", CultureInfo.InvariantCulture) + (Zone.IsDaylightSavingTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc)) ? " CDT" : " CST");
    }
}
