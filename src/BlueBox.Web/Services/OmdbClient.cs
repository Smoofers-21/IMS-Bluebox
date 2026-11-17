using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using BlueBox.Web.Models;

namespace BlueBox.Web.Services;

public sealed record MovieMatch(string Id, string Title, string Year);
public sealed record MovieMetadata(string Title, string? PosterUrl, string? AgeRating);
public sealed record MovieSearchResult(List<MovieMatch> Matches, string? Message = null);
public sealed record MovieDetailResult(MovieMetadata? Movie, string? Message = null);

public sealed class OmdbClient(HttpClient http, IConfiguration configuration)
{
    private string? Key => configuration["Omdb:ApiKey"];

    public async Task<MovieSearchResult> SearchAsync(string? title, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Key)) return new([], "Movie lookup is not configured. You can enter this item manually.");
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 150) return new([], "Enter a movie title of up to 150 characters.");
        var data = await FetchAsync($"s={Uri.EscapeDataString(title.Trim())}&type=movie", ct);
        if (data is null) return new([], "Movie lookup is unavailable. You can continue with manual entry.");
        using (data)
        {
            var root = data.RootElement;
            if (Text(root, "Response") == "False")
                return Text(root, "Error") switch
                {
                    "Too many results." => new([], "Too many movie matches. Enter a more specific title."),
                    "Movie not found!" => new([], "No movie matches found. Try another title or enter it manually."),
                    _ => new([], "Movie lookup is unavailable. You can continue with manual entry.")
                };
            if (Text(root, "Response") != "True" || !root.TryGetProperty("Search", out var matches) || matches.ValueKind != JsonValueKind.Array)
                return new([], "No movie matches found. Try another title or enter it manually.");
            return new(matches.EnumerateArray().Select(m => new MovieMatch(Text(m, "imdbID") ?? "", Text(m, "Title") ?? "", Text(m, "Year") ?? ""))
                .Where(m => ValidId(m.Id) && m.Title.Length > 0).Take(10).ToList());
        }
    }

    public async Task<MovieDetailResult> DetailAsync(string? id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Key) || !ValidId(id)) return new(null, "Select a movie match, or continue manually.");
        var data = await FetchAsync($"i={Uri.EscapeDataString(id!)}&type=movie", ct);
        if (data is null) return new(null, "Movie details are unavailable. You can continue manually.");
        using (data)
        {
            var root = data.RootElement;
            if (Text(root, "Response") != "True" || Text(root, "Type") != "movie") return new(null, "Movie details were not found.");
            var poster = Optional(root, "Poster", 2048);
            if (!new HttpsUrlAttribute().IsValid(poster)) poster = null;
            return new(new(Text(root, "Title") ?? "", poster, Optional(root, "Rated", 20)));
        }
    }

    private async Task<JsonDocument?> FetchAsync(string query, CancellationToken ct)
    {
        try
        {
            // Fixed external host. The API key is never sent to the browser or logged.
            using var response = await http.GetAsync($"https://www.omdbapi.com/?apikey={Uri.EscapeDataString(Key!)}&{query}", ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
        }
        catch (HttpRequestException) { return null; }
        catch (JsonException) { return null; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; }
    }

    private static bool ValidId(string? id) => id is not null && Regex.IsMatch(id, "^tt[0-9]{5,12}$", RegexOptions.CultureInvariant);
    private static string? Text(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static string? Optional(JsonElement element, string name, int max) => Text(element, name) is { Length: > 0 } text
        && text != "N/A" && text.Length <= max ? text : null;
}
