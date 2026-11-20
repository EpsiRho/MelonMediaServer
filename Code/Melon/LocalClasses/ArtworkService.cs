using System.Linq.Expressions;
using Melon.Interface;
using Melon.Models;
using MongoDB.Driver;
using Newtonsoft.Json;

namespace Melon.LocalClasses;

/// <summary>Saved artwork settings. Keys are stored by provider id so plugin settings survive reloads.</summary>
internal sealed class ArtworkConfig
{
    public bool FetchAfterScan { get; set; } = true;
    public List<string> Order { get; set; } = [];
    public Dictionary<string, bool> Enabled { get; set; } = [];
    /// <summary>JSON object of provider id → API key, encrypted at rest. Never empty: Storage cannot decrypt "".</summary>
    public string Keys { get; set; } = "{}";
}

/// <summary>Registry of artwork providers and the background job that fills in missing artist artwork.</summary>
public sealed class ArtworkService : IArtworkProviderRegistry
{
    public static ArtworkService Instance { get; } = new();
    private const string ConfigFile = "ArtworkProviders";
    private static readonly TimeSpan RetryAfter = TimeSpan.FromDays(30);
    private const long MaxImageBytes = 15 * 1024 * 1024;
    private static readonly Lazy<HttpClient> SharedHttp = new(() =>
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        // MusicBrainz rejects (503) agents that do not follow its "Name/version ( contact )" format.
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"MelonMediaServer/{StateManager.Version ?? "1.0"} ( https://github.com/EpsiRho/MelonMediaServer )");
        return http;
    });
    internal static HttpClient Http => SharedHttp.Value;

    private readonly object gate = new();
    private readonly List<IArtworkProvider> providers = [];
    private readonly HashSet<string> builtIn = [];
    private ArtworkConfig config = new();
    private Dictionary<string, string> keys = [];
    private CancellationTokenSource? fetch;
    private Task fetchTask = Task.CompletedTask;
    private int processed, total, updated;
    private string? lastResult;

    private ArtworkService()
    {
        // Registration order is the default priority: fanart.tv wins once it has a key, Deezer works out of the box.
        foreach (var provider in new IArtworkProvider[] { new FanartTvProvider(), new DeezerProvider() })
        {
            Register(provider);
            builtIn.Add(provider.Id);
        }
    }

    public event Action<string>? Changed;
    private void Publish(string message) => Changed?.Invoke(message);

    // ── Settings ─────────────────────────────────────────────────────────────

    public void LoadConfig()
    {
        lock (gate)
        {
            ArtworkConfig? loaded = null;
            try { loaded = Storage.LoadConfigFile<ArtworkConfig>(ConfigFile, ["Keys"], out _); }
            catch (Exception ex) { Serilog.Log.Warning(ex, "Artwork provider settings could not be read; using defaults."); }
            config = loaded ?? new ArtworkConfig();
            config.Order ??= [];
            config.Enabled ??= [];
            try { keys = JsonConvert.DeserializeObject<Dictionary<string, string>>(config.Keys ?? "{}") ?? []; }
            catch (JsonException) { keys = []; }
            if (loaded == null) Save();
        }
    }

    private void Save()
    {
        config.Keys = JsonConvert.SerializeObject(keys);
        Storage.SaveConfigFile(ConfigFile, new ArtworkConfig
        {
            FetchAfterScan = config.FetchAfterScan, Order = [.. config.Order],
            Enabled = new(config.Enabled), Keys = config.Keys
        }, ["Keys"]);
    }

    private void Change(Action change, string message)
    {
        lock (gate)
        {
            var before = JsonConvert.SerializeObject(config);
            var beforeKeys = new Dictionary<string, string>(keys);
            change();
            try { Save(); }
            catch
            {
                config = JsonConvert.DeserializeObject<ArtworkConfig>(before)!;
                keys = beforeKeys;
                throw;
            }
        }
        Publish(message);
    }

    private string Known(string id)
    {
        id = (id ?? "").Trim().ToLowerInvariant();
        lock (gate)
            if (providers.Any(p => p.Id == id) || KnownIds().Contains(id)) return id;
        throw new ArgumentException("Unknown artwork provider.");
    }

    private HashSet<string> KnownIds() => [.. providers.Select(p => p.Id), .. config.Order, .. config.Enabled.Keys, .. keys.Keys];

    private bool HasKey(string id) => keys.TryGetValue(id, out var key) && key.Length > 0;

    private bool IsEnabled(IArtworkProvider provider) =>
        (config.Enabled.TryGetValue(provider.Id, out var on) ? on : provider.EnabledByDefault) &&
        (!provider.RequiresApiKey || HasKey(provider.Id));

    private List<string> OrderedIds()
    {
        var order = config.Order.Where(KnownIds().Contains).ToList();
        foreach (var provider in providers) if (!order.Contains(provider.Id)) order.Add(provider.Id);
        return order;
    }

    public IReadOnlyList<IArtworkProvider> Providers
    {
        get
        {
            lock (gate)
            {
                var order = OrderedIds();
                return providers.OrderBy(p => order.IndexOf(p.Id)).ToArray();
            }
        }
    }

    public ArtworkStatus GetStatus()
    {
        lock (gate)
        {
            var order = OrderedIds();
            var rows = order.Select((id, index) =>
            {
                var provider = providers.Find(p => p.Id == id);
                if (provider == null)
                    return new ArtworkProviderInfo(id, id, "Provided by a plugin that is not loaded.", [],
                        config.Enabled.GetValueOrDefault(id), index, false, HasKey(id), null, false, false);
                return new ArtworkProviderInfo(provider.Id, provider.Name, provider.Description,
                    provider.Kinds.Select(k => k.ToString()).ToArray(), IsEnabled(provider), index, provider.RequiresApiKey,
                    HasKey(id), provider.ApiKeyUrl, builtIn.Contains(id), true);
            }).ToArray();
            return new(config.FetchAfterScan, !fetchTask.IsCompleted, processed, total, updated, lastResult, rows);
        }
    }

    public void SetEnabled(string id, bool enabled)
    {
        id = Known(id);
        lock (gate)
        {
            var provider = providers.Find(p => p.Id == id);
            if (enabled && provider?.RequiresApiKey == true && !HasKey(id))
                throw new ArgumentException($"Add an API key for {provider.Name} first.");
        }
        Change(() => config.Enabled[id] = enabled, "Artwork provider settings changed.");
    }

    public void SetOrder(string[] ids)
    {
        var wanted = ids.Select(Known).Distinct().ToList();
        Change(() => config.Order = [.. wanted, .. OrderedIds().Where(id => !wanted.Contains(id))], "Artwork provider order changed.");
    }

    public void SetKey(string id, string apiKey)
    {
        id = Known(id);
        apiKey = (apiKey ?? "").Trim();
        Change(() =>
        {
            if (apiKey.Length == 0) { keys.Remove(id); config.Enabled.Remove(id); }
            else { keys[id] = apiKey; config.Enabled[id] = true; } // Entering a key is the admin opting in.
        }, "Artwork provider key changed.");
    }

    public void SetFetchAfterScan(bool enabled) => Change(() => config.FetchAfterScan = enabled, "Artwork settings changed.");

    public bool FetchAfterScan { get { lock (gate) return config.FetchAfterScan; } }

    // ── Registry ─────────────────────────────────────────────────────────────

    public void Register(IArtworkProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var id = provider.Id?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(id) || id != provider.Id)
            throw new ArgumentException("Artwork provider ids must be non-empty, trimmed and lowercase.");
        lock (gate)
        {
            if (providers.Any(p => p.Id == id)) throw new InvalidOperationException($"An artwork provider named '{id}' is already registered.");
            providers.Add(provider);
        }
        Publish("Artwork providers changed.");
    }

    public bool Unregister(string id)
    {
        bool removed;
        lock (gate)
        {
            if (builtIn.Contains(id)) throw new InvalidOperationException("Built-in artwork providers cannot be removed; disable them instead.");
            removed = providers.RemoveAll(p => p.Id == id) > 0;
        }
        if (removed) Publish("Artwork providers changed.");
        return removed;
    }

    /// <summary>Drops providers whose code lives in an unloading plugin, in case the plugin did not unregister them.</summary>
    internal void UnregisterAssembly(System.Reflection.Assembly assembly)
    {
        string[] stale;
        lock (gate) stale = providers.Where(p => p.GetType().Assembly == assembly && !builtIn.Contains(p.Id)).Select(p => p.Id).ToArray();
        foreach (var id in stale) Unregister(id);
    }

    private (IArtworkProvider Provider, ArtworkProviderContext Context)[] Active()
    {
        lock (gate)
        {
            var order = OrderedIds();
            return providers.Where(IsEnabled).OrderBy(p => order.IndexOf(p.Id))
                .Select(p => (p, new ArtworkProviderContext { Http = Http, ApiKey = keys.GetValueOrDefault(p.Id) })).ToArray();
        }
    }

    // ── Fetching ─────────────────────────────────────────────────────────────

    public bool IsFetching { get { lock (gate) return !fetchTask.IsCompleted; } }

    /// <summary>Starts a background fetch. Never runs alongside a library scan, which rewrites artist documents.</summary>
    public bool StartFetch(bool force)
    {
        var active = Active();
        if (active.Length == 0) throw new InvalidOperationException("No artwork providers are enabled.");
        lock (gate)
        {
            if (!fetchTask.IsCompleted || MelonScanner.Scanning) return false;
            fetch?.Dispose();
            fetch = new CancellationTokenSource();
            var token = fetch.Token;
            processed = total = updated = 0;
            lastResult = null;
            fetchTask = Task.Run(() => RunAsync(active, force, token));
        }
        Publish("Artwork fetch started.");
        return true;
    }

    /// <summary>Stops a running fetch and waits for its current artist to finish.</summary>
    public void CancelAndWait()
    {
        Task running;
        lock (gate) { fetch?.Cancel(); running = fetchTask; }
        try { running.Wait(TimeSpan.FromSeconds(30)); } catch (AggregateException) { }
    }

    private async Task RunAsync((IArtworkProvider Provider, ArtworkProviderContext Context)[] active, bool force, CancellationToken token)
    {
        string result;
        try
        {
            result = await FetchAll(active, force, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { result = "Artwork fetch stopped."; }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Artwork fetch failed");
            result = "Artwork fetch failed. Check the server logs.";
        }
        lock (gate) lastResult = result;
        Publish(result);
    }

    private async Task<string> FetchAll((IArtworkProvider Provider, ArtworkProviderContext Context)[] active, bool force, CancellationToken token)
    {
        var database = StateManager.DbClient.GetDatabase("Melon");
        var artists = database.GetCollection<Artist>("Artists");
        var albums = database.GetCollection<Album>("Albums");
        var kinds = active.SelectMany(a => a.Provider.Kinds).ToHashSet();
        var f = Builders<Artist>.Filter;
        var missing = new List<FilterDefinition<Artist>>();
        if (kinds.Contains(ArtworkKind.ArtistPicture)) missing.Add(f.Eq(a => a.ArtistPfpArtCount, 0));
        if (kinds.Contains(ArtworkKind.ArtistBanner)) missing.Add(f.Eq(a => a.ArtistBannerArtCount, 0));
        if (missing.Count == 0) return "The enabled providers cannot supply artist artwork.";
        var filter = f.Or(missing);
        if (!force) filter &= f.Or(f.Eq(a => a.ArtworkCheckedUtc, null), f.Lt(a => a.ArtworkCheckedUtc, DateTime.UtcNow - RetryAfter));
        var projection = Builders<Artist>.Projection.Include(a => a._id).Include(a => a.Name).Include(a => a.ArtistPfpArtCount)
            .Include(a => a.ArtistBannerArtCount).Include(a => a.MusicBrainzArtistId);
        var queue = artists.Find(filter).Project<Artist>(projection).ToList();
        lock (gate) total = queue.Count;
        bool needsMbid = active.Any(a => a.Provider.RequiresMusicBrainzId);
        var rejected = new HashSet<string>();
        var notes = new List<string>();

        foreach (var artist in queue)
        {
            token.ThrowIfCancellationRequested();
            var needed = new HashSet<ArtworkKind>();
            if (artist.ArtistPfpArtCount == 0) needed.Add(ArtworkKind.ArtistPicture);
            if (artist.ArtistBannerArtCount == 0) needed.Add(ArtworkKind.ArtistBanner);
            needed.IntersectWith(kinds);
            var albumNames = albums.Find(Builders<Album>.Filter.ElemMatch(a => a.AlbumArtists, l => l.Name == artist.Name))
                .Project(a => a.Name).Limit(25).ToList();
            var mbid = artist.MusicBrainzArtistId;
            if (needsMbid && mbid == null)
            {
                try { mbid = await MusicBrainz.FindArtistIdAsync(Http, artist.Name, albumNames, token) ?? ""; }
                catch (HttpRequestException ex) { Serilog.Log.Warning(ex, "MusicBrainz lookup failed for {Artist}", artist.Name); }
            }
            var query = new ArtworkQuery(artist.Name, string.IsNullOrEmpty(mbid) ? null : mbid, albumNames);
            bool changed = false;
            foreach (var (provider, context) in active)
            {
                if (needed.Count == 0) break;
                if (rejected.Contains(provider.Id) || !needed.Overlaps(provider.Kinds)) continue;
                if (provider.RequiresMusicBrainzId && query.MusicBrainzArtistId == null) continue;
                IReadOnlyList<ArtworkCandidate> found;
                try { found = await provider.FindArtistArtworkAsync(query, context, token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (UnauthorizedAccessException)
                {
                    rejected.Add(provider.Id);
                    notes.Add($"{provider.Name} rejected its API key.");
                    continue;
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "Artwork provider {Provider} failed for {Artist}", provider.Id, artist.Name);
                    continue;
                }
                foreach (var kind in needed.ToArray())
                    foreach (var candidate in found.Where(c => c.Kind == kind))
                        if (await TrySave(artists, artist._id, candidate, token)) { needed.Remove(kind); changed = true; break; }
            }
            artists.UpdateOne(f.Eq(a => a._id, artist._id), Builders<Artist>.Update
                .Set(a => a.ArtworkCheckedUtc, DateTime.UtcNow).Set(a => a.MusicBrainzArtistId, mbid), cancellationToken: CancellationToken.None);
            lock (gate) { processed++; if (changed) updated++; } // Progress is read through GetStatus; no message per artist.
        }
        var summary = total == 0 ? "No artists need artwork." : $"Artwork added for {updated:N0} of {total:N0} artists.";
        return string.Join(" ", notes.Prepend(summary));
    }

    /// <summary>Downloads one candidate and attaches it, only if the artist still has no art of that kind.</summary>
    private static async Task<bool> TrySave(IMongoCollection<Artist> artists, string id, ArtworkCandidate candidate, CancellationToken token)
    {
        if (candidate.Url.Scheme != Uri.UriSchemeHttps) return false;
        var image = await Download(candidate.Url, token);
        if (image == null) return false;
        bool picture = candidate.Kind == ArtworkKind.ArtistPicture;
        Expression<Func<Artist, int>> count = picture ? a => a.ArtistPfpArtCount : a => a.ArtistBannerArtCount;
        Expression<Func<Artist, List<string>>> paths = picture ? a => a.ArtistPfpPaths : a => a.ArtistBannerPaths;
        Expression<Func<Artist, int>> selected = picture ? a => a.ArtistPfpDefault : a => a.ArtistBannerArtDefault;
        var folder = Path.Combine(StateManager.melonPath, picture ? "ArtistPfps" : "ArtistBanners");
        var name = $"{id}-0.{image.Value.Extension}";
        // Claim slot 0 atomically so an upload that lands meanwhile is never overwritten.
        var f = Builders<Artist>.Filter;
        var claimed = artists.UpdateOne(f.Eq(a => a._id, id) & f.Eq(count, 0), Builders<Artist>.Update.Set(count, 1));
        if (claimed.ModifiedCount == 0) return false;
        try
        {
            Directory.CreateDirectory(folder);
            await File.WriteAllBytesAsync(Path.Combine(folder, name), image.Value.Bytes, CancellationToken.None);
            artists.UpdateOne(f.Eq(a => a._id, id), Builders<Artist>.Update.Set(paths, [name]).Set(selected, 0));
            return true;
        }
        catch
        {
            artists.UpdateOne(f.Eq(a => a._id, id) & f.Eq(count, 1), Builders<Artist>.Update.Set(count, 0));
            throw;
        }
    }

    private static async Task<(byte[] Bytes, string Extension)?> Download(Uri url, CancellationToken token)
    {
        try
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxImageBytes) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, token)) > 0)
            {
                if (buffer.Length + read > MaxImageBytes) return null;
                buffer.Write(chunk, 0, read);
            }
            var bytes = buffer.ToArray();
            // Trust the bytes, not the provider's headers. The extension doubles as the MIME subtype on download.
            string? extension = bytes switch
            {
                [0xFF, 0xD8, 0xFF, ..] => "jpeg",
                [0x89, 0x50, 0x4E, 0x47, ..] => "png",
                [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => "webp",
                _ => null
            };
            return extension == null ? null : (bytes, extension);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !token.IsCancellationRequested)
        {
            Serilog.Log.Warning(ex, "Artwork download failed: {Host}", url.Host);
            return null;
        }
    }

    /// <summary>Asks one provider about one artist without saving anything.</summary>
    public async Task<string> TestAsync(string id, string artistName)
    {
        id = Known(id);
        if (string.IsNullOrWhiteSpace(artistName)) throw new ArgumentException("Enter an artist name.");
        IArtworkProvider provider;
        ArtworkProviderContext context;
        lock (gate)
        {
            provider = providers.Find(p => p.Id == id) ?? throw new InvalidOperationException("That provider's plugin is not loaded.");
            if (provider.RequiresApiKey && !HasKey(id)) throw new ArgumentException($"Add an API key for {provider.Name} first.");
            context = new ArtworkProviderContext { Http = Http, ApiKey = keys.GetValueOrDefault(id) };
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            string? mbid = null;
            if (provider.RequiresMusicBrainzId)
            {
                mbid = await MusicBrainz.FindArtistIdAsync(Http, artistName.Trim(), [], timeout.Token);
                if (mbid == null) return $"MusicBrainz has no unambiguous match for \"{artistName.Trim()}\", which {provider.Name} needs.";
            }
            var found = await provider.FindArtistArtworkAsync(new ArtworkQuery(artistName.Trim(), mbid, []), context, timeout.Token);
            if (found.Count == 0) return $"{provider.Name} is working but has no artwork for \"{artistName.Trim()}\".";
            var kinds = found.Select(c => c.Kind).Distinct().Select(k => k == ArtworkKind.ArtistPicture ? "a picture" : "a banner");
            return $"{provider.Name} is working: found {string.Join(" and ", kinds)} for \"{artistName.Trim()}\".";
        }
        catch (UnauthorizedAccessException) { throw new ArgumentException($"{provider.Name} rejected the API key."); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            Serilog.Log.Warning(ex, "Artwork provider test failed for {Provider}", id);
            throw new InvalidOperationException($"{provider.Name} could not be reached.");
        }
    }
}
