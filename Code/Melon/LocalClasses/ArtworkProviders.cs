using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Melon.Interface;

namespace Melon.LocalClasses;

/// <summary>Spaces out requests to one web service.</summary>
internal sealed class Throttle(TimeSpan spacing)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTime next = DateTime.MinValue;

    public async Task WaitAsync(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            var wait = next - DateTime.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, token);
            next = DateTime.UtcNow + spacing;
        }
        finally { gate.Release(); }
    }
}

internal static class ArtistNames
{
    /// <summary>Compares names ignoring case, accents, punctuation and a leading "The".</summary>
    public static bool Same(string? a, string? b) => a != null && b != null && Key(a) == Key(b) && Key(a).Length > 0;

    public static string Key(string name)
    {
        var text = name.Trim();
        if (text.StartsWith("the ", StringComparison.OrdinalIgnoreCase)) text = text[4..];
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Replace("&", "and").Normalize(NormalizationForm.FormD))
            if (char.IsLetterOrDigit(c)) builder.Append(char.ToLowerInvariant(c));
        return builder.ToString();
    }

    public static bool SharesAlbum(IEnumerable<string> theirs, IReadOnlyList<string> ours)
    {
        var keys = ours.Select(Key).Where(k => k.Length > 0).ToHashSet();
        return theirs.Any(title => keys.Contains(Key(title)));
    }
}

/// <summary>Resolves MusicBrainz artist IDs for providers that need them. MusicBrainz allows one request per second.</summary>
internal static class MusicBrainz
{
    private static readonly Throttle Limit = new(TimeSpan.FromMilliseconds(1100));

    public static async Task<string?> FindArtistIdAsync(HttpClient http, string name, IReadOnlyList<string> albums, CancellationToken token)
    {
        var phrase = name.Replace("\\", "\\\\").Replace("\"", "\\\"");
        using var search = await Get(http, $"https://musicbrainz.org/ws/2/artist/?query={Uri.EscapeDataString($"artist:\"{phrase}\"")}&fmt=json&limit=10", token);
        if (search == null) return null;
        var exact = search.RootElement.GetProperty("artists").EnumerateArray()
            .Where(a => a.TryGetProperty("score", out var s) && s.GetInt32() >= 90 && ArtistNames.Same(a.GetProperty("name").GetString(), name))
            .Select(a => a.GetProperty("id").GetString()!).ToList();
        if (exact.Count <= 1) return exact.FirstOrDefault();
        if (albums.Count == 0) return null; // Several artists share this name and nothing tells them apart.
        foreach (var id in exact.Take(3))
        {
            using var groups = await Get(http, $"https://musicbrainz.org/ws/2/release-group?artist={id}&fmt=json&limit=100", token);
            if (groups != null && ArtistNames.SharesAlbum(groups.RootElement.GetProperty("release-groups").EnumerateArray()
                    .Select(g => g.GetProperty("title").GetString() ?? ""), albums))
                return id;
        }
        return null;
    }

    private static async Task<JsonDocument?> Get(HttpClient http, string url, CancellationToken token)
    {
        await Limit.WaitAsync(token);
        var response = await http.GetAsync(url, token);
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            // MusicBrainz sheds load with 503s; back off once before giving up on this artist.
            response.Dispose();
            await Task.Delay(TimeSpan.FromSeconds(3), token);
            await Limit.WaitAsync(token);
            response = await http.GetAsync(url, token);
        }
        using var _ = response;
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(token), cancellationToken: token);
    }
}

/// <summary>Artist pictures from Deezer's public API. No key, enabled by default.</summary>
internal sealed class DeezerProvider : IArtworkProvider
{
    private static readonly Throttle Limit = new(TimeSpan.FromMilliseconds(150)); // Deezer allows 50 requests per 5 seconds.
    public string Id => "deezer";
    public string Name => "Deezer";
    public string Description => "Artist pictures from Deezer. No account needed.";
    public IReadOnlyCollection<ArtworkKind> Kinds { get; } = [ArtworkKind.ArtistPicture];
    public bool EnabledByDefault => true;

    public async Task<IReadOnlyList<ArtworkCandidate>> FindArtistArtworkAsync(ArtworkQuery query, ArtworkProviderContext context, CancellationToken token)
    {
        using var search = await Get(context.Http, $"https://api.deezer.com/search/artist?q={Uri.EscapeDataString(query.ArtistName)}&limit=10", token);
        var matches = search.RootElement.GetProperty("data").EnumerateArray()
            .Where(a => ArtistNames.Same(a.GetProperty("name").GetString(), query.ArtistName))
            .OrderByDescending(a => a.TryGetProperty("nb_fan", out var fans) ? fans.GetInt64() : 0)
            .Select(a => (Id: a.GetProperty("id").GetInt64(), Picture: a.TryGetProperty("picture_xl", out var p) ? p.GetString() : null))
            .ToList();
        var match = matches.FirstOrDefault();
        if (matches.Count > 1 && query.AlbumNames.Count > 0)
        {
            // Same name, different artists: pick the one whose albums overlap the library.
            match = default;
            foreach (var candidate in matches.Take(3))
            {
                using var albums = await Get(context.Http, $"https://api.deezer.com/artist/{candidate.Id}/albums?limit=100", token);
                if (ArtistNames.SharesAlbum(albums.RootElement.GetProperty("data").EnumerateArray()
                        .Select(a => a.GetProperty("title").GetString() ?? ""), query.AlbumNames)) { match = candidate; break; }
            }
        }
        // Deezer returns a placeholder silhouette with an empty image hash when it has no picture.
        if (match.Picture is not { Length: > 0 } url || url.Contains("/artist//", StringComparison.Ordinal)) return [];
        return [new ArtworkCandidate(new Uri(url), ArtworkKind.ArtistPicture, 1000, 1000)];
    }

    private static async Task<JsonDocument> Get(HttpClient http, string url, CancellationToken token)
    {
        await Limit.WaitAsync(token);
        var document = JsonDocument.Parse(await http.GetStringAsync(url, token));
        if (document.RootElement.TryGetProperty("error", out var error))
        {
            document.Dispose();
            throw new HttpRequestException("Deezer: " + (error.TryGetProperty("message", out var m) ? m.GetString() : "request failed"));
        }
        return document;
    }
}

/// <summary>Artist thumbnails and backgrounds from fanart.tv. Needs a free personal API key and a MusicBrainz ID.</summary>
internal sealed class FanartTvProvider : IArtworkProvider
{
    private static readonly Throttle Limit = new(TimeSpan.FromMilliseconds(250));
    public string Id => "fanarttv";
    public string Name => "fanart.tv";
    public string Description => "Curated artist pictures and backgrounds. Needs a free personal API key.";
    public IReadOnlyCollection<ArtworkKind> Kinds { get; } = [ArtworkKind.ArtistPicture, ArtworkKind.ArtistBanner];
    public bool RequiresApiKey => true;
    public string? ApiKeyUrl => "https://fanart.tv/get-an-api-key/";
    public bool RequiresMusicBrainzId => true;

    public async Task<IReadOnlyList<ArtworkCandidate>> FindArtistArtworkAsync(ArtworkQuery query, ArtworkProviderContext context, CancellationToken token)
    {
        if (query.MusicBrainzArtistId == null || string.IsNullOrEmpty(context.ApiKey)) return [];
        await Limit.WaitAsync(token);
        var url = $"https://webservice.fanart.tv/v3/music/{Uri.EscapeDataString(query.MusicBrainzArtistId)}?api_key={Uri.EscapeDataString(context.ApiKey)}";
        using var response = await context.Http.GetAsync(url, token);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new UnauthorizedAccessException();
        if (response.StatusCode == HttpStatusCode.NotFound) return [];
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(token), cancellationToken: token);
        var root = document.RootElement;
        return [.. Images(root, "artistthumb", ArtworkKind.ArtistPicture, 1000, 1000),
                .. Images(root, "artistbackground", ArtworkKind.ArtistBanner, 1920, 1080)];
    }

    private static IEnumerable<ArtworkCandidate> Images(JsonElement root, string property, ArtworkKind kind, int width, int height)
    {
        if (!root.TryGetProperty(property, out var list) || list.ValueKind != JsonValueKind.Array) return [];
        return list.EnumerateArray()
            .OrderByDescending(i => i.TryGetProperty("likes", out var l) && int.TryParse(l.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .Select(i => i.TryGetProperty("url", out var u) && Uri.TryCreate(u.GetString(), UriKind.Absolute, out var uri) ? uri : null)
            .Where(uri => uri != null)
            // Older fanart.tv entries list http:// asset URLs; the same files are served over HTTPS.
            .Select(uri => uri!.Scheme == Uri.UriSchemeHttp ? new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri : uri)
            .Select(uri => new ArtworkCandidate(uri!, kind, width, height))
            .ToList();
    }
}
