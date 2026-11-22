using System.Text.Json;
using Melon.Models;

namespace Melon.Interface;

public record ServerStatus(string Version, string Branch, string DataDirectory, bool DatabaseConnected,
    bool SetupRequired, string[] PendingConversions, bool Scanning, ScanProgress Scan)
{
    public Guid RunId { get; init; }
}
public record LibraryTotals(long Songs, long Albums, long Artists);
public record TransferItem(string Id, string Name, string Kind);
public record ControllerMessage(long Sequence, string Topic, string Message, ServerStatus? Status = null);
public record Appearance(string Language, bool UseMenuColor, int Text, int ShadedText,
    int BackgroundText, int Highlight, int Melon, int Error);
public record ControllerSettings(string[] LibraryPaths, string[] ArtistSplitIndicators,
    string[] GenreSplitIndicators, double QueueCleanupHours, string ListeningUrl,
    string? CertificatePath, Appearance Appearance, string[] MongoServers, bool RestartRequired);
public record ManagedUser(string Id, string Username, string Role);
public record AccessCode(string Code, DateTime ExpiresUtc);
public record ListeningDay(DateTime DateUtc, long Plays, long Skips, double Milliseconds);
public record RankedTrack(string Name, long Plays);
public record ListeningSummary(ListeningDay[] Days, RankedTrack[] TopTracks);
public record PlaylistOwner(string Id, string Username);
public record PluginInfo(string Name, string Authors, string Version, string Description, bool IsEnabled);
public record ReleaseInfo(bool UpdateAvailable, string CurrentVersion, string LatestVersion, string ReleaseNotes);
public record PluginField(string Key, string Label, string Type, object Value);
public record PluginAction(string Key, string Label);
public record PluginPage(PluginField[] Fields, PluginAction[] Actions);
/// <param name="Installed">False when settings are saved for a plugin provider that is not currently loaded.</param>
public record ArtworkProviderInfo(string Id, string Name, string Description, string[] Kinds, bool Enabled, int Priority,
    bool RequiresApiKey, bool HasApiKey, string? ApiKeyUrl, bool BuiltIn, bool Installed);
public record ArtworkStatus(bool FetchAfterScan, bool Running, int Processed, int Total, int Updated, string? LastResult,
    ArtworkProviderInfo[] Providers);

/// <summary>
/// How FFTVIS (.fvz) frames are generated and stored. Clients may request any format within <see cref="VisualLimits"/>.
/// </summary>
/// <param name="Bands">Bars per frame.</param>
/// <param name="Fps">Frames per second stored; clients interpolate between frames.</param>
/// <param name="FftResolution">FFT window in samples, a power of two.</param>
/// <param name="Mapping">Normalized, Mel or Log10.</param>
/// <param name="DbFloor">Sound below this level (dB, negative) shows as 0.</param>
/// <param name="DbRange">dB above the floor that map onto 0–1.</param>
/// <param name="Smoothness">Average each bar with this many neighbours on each side.</param>
/// <param name="Stereo">Store left and right instead of a mono mix.</param>
/// <param name="QuantizeBits">Bits per stored value, 4–16.</param>
/// <param name="DecayPerSecond">Peak-meter fall applied when encoding, full-scale units per second (0 = off).</param>
/// <param name="Codec">Brotli, Zstd or None.</param>
public record VisualFormat(int Bands = 64, int Fps = 60, int FftResolution = 4096, string Mapping = "Normalized",
    double DbFloor = -80, double DbRange = 90, double FrequencyMin = 20, double FrequencyMax = 20000, int Smoothness = 0,
    bool Stereo = false, int QuantizeBits = 8, double DecayPerSecond = 0, string Codec = "Brotli");

/// <summary>The range of <see cref="VisualFormat"/> values clients may request. Requests outside it are clamped.</summary>
public record VisualLimits(int MinBands = 4, int MaxBands = 256, int MinFps = 10, int MaxFps = 240,
    int MinFftResolution = 1024, int MaxFftResolution = 16384, int MinQuantizeBits = 4, int MaxQuantizeBits = 16,
    bool AllowStereo = true);

public record VisualsSettings(bool Enabled, VisualFormat Defaults, VisualLimits Limits, int CacheMegabytes, int MaxConcurrentJobs);

/// <param name="CacheBytes">Disk used by cached visualization files.</param>
/// <param name="Running">Tracks being analysed right now.</param>
public record VisualsStatus(VisualsSettings Settings, long CacheBytes, int CacheFiles, int Running);

/// <summary>Optional controller-neutral settings and actions, executed in the server process.</summary>
public interface IControllerPlugin
{
    PluginPage GetPage();
    object? Invoke(string action, JsonElement values);
}

/// <summary>Administrative operations available to trusted plugins and local controllers.</summary>
public interface IMelonAdministration
{
    ServerStatus GetStatus();
    ControllerSettings GetSettings();
    LibraryTotals GetLibraryTotals();
    TransferItem[] SearchTransferItems(string query);
    int ImportCollections(string path, string userId);
    void ConnectManagedMongo();
    string[] GetLanguages();
    void SetDefaultLanguage(string language);
    void SetLibraryPaths(string[] paths);
    void SetArtistSplitIndicators(string[] indicators);
    void SetGenreSplitIndicators(string[] indicators);
    void SetQueueCleanupHours(double hours);
    void SetAppearance(Appearance appearance);
    void ResetColors();
    void SetListeningUrl(string url);
    void SetMongoConnection(string connectionString);
    void SetCertificate(string path, string password);
    void DisableCertificate();
    string CreateFirstAdmin(string username, string password);
    void ConvertDatabase();
    bool StartScan(bool skip = false);
    bool BackupDatabase();
    string[] ListBackups();
    void RestoreBackup(string name);
    void ResetDatabase();
    PlaylistOwner[] GetPlaylistOwners();
    int ImportPlaylists(string path, string userId);
    string ExportPlaylist(string id, string format);
    ManagedUser[] GetUsers();
    void SetUserType(string userId, string type);
    bool DeleteUser(string userId);
    AccessCode CreatePasswordReset(string userId);
    AccessCode CreateInvite(int minutes = 10);
    AccessCode[] GetInvites();
    void RevokeInvite(string code);
    ListeningSummary GetListeningSummary(string userId = "", int days = 7);
    void PruneDeletedUserStats();
    PluginInfo[] GetPlugins();
    bool SetPluginEnabled(string name, string author, bool enabled);
    void ReloadPlugins(bool rescan);
    Dictionary<string, string> GetHelp();
    PluginPage GetPluginPage(string name, string author);
    object? InvokePlugin(string name, string author, string action, JsonElement values);
    ArtworkStatus GetArtwork();
    void SetArtworkProviderEnabled(string id, bool enabled);
    /// <summary>Sets provider priority, highest first. Providers left out keep their order after the listed ones.</summary>
    void SetArtworkProviderOrder(string[] ids);
    /// <summary>Saves an API key for a provider. An empty key removes it and disables a provider that needs one.</summary>
    void SetArtworkProviderKey(string id, string apiKey);
    void SetArtworkFetchAfterScan(bool enabled);
    /// <summary>Starts filling in missing artist artwork in the background. Returns false when a fetch or scan is running.</summary>
    /// <param name="force">Also retry artists checked recently that had no match.</param>
    bool FetchArtistArtwork(bool force = false);
    /// <summary>Looks up one artist with one provider without saving anything, to check its setup.</summary>
    Task<string> TestArtworkProvider(string id, string artistName);
    /// <summary>Visualization settings, client limits and cache usage.</summary>
    VisualsStatus GetVisuals();
    /// <summary>Turns visualization data (FFTVIS frames, beat times, moments) on or off for clients.</summary>
    void SetVisualsEnabled(bool enabled);
    /// <summary>The format generated when a client doesn't ask for one. Must lie within the limits.</summary>
    void SetVisualDefaults(VisualFormat format);
    /// <summary>The range of formats clients may request. Defaults outside the new limits are pulled inside them.</summary>
    void SetVisualLimits(VisualLimits limits);
    /// <summary>Disk cache size (64–102,400 MB) and how many tracks may be analysed at once (1–16).</summary>
    void SetVisualsCache(int megabytes, int maxConcurrentJobs);
    /// <summary>Deletes every cached visualization file. Returns the bytes freed.</summary>
    long ClearVisualsCache();
    Task<ReleaseInfo> CheckForUpdates();
    Task<bool> ApplyUpdate();
    void PublishMessage(string topic, string message);
    void Restart();
    void Stop();
}
