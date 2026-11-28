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
    string? CertificatePath, Appearance Appearance, string[] MongoServers, bool RestartRequired,
    int ArtUploadMegabytes = 20, int PlaylistImportMegabytes = 2, int LogFileMegabytes = 100, int LogFilesKept = 5);
public record ManagedUser(string Id, string Username, string Role);
/// <param name="Link">For invites: the link to encode in a QR code. It carries the server's addresses and certificate fingerprint.</param>
public record AccessCode(string Code, DateTime ExpiresUtc, string? Link = null);
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

/// <summary>One address clients can try. <paramref name="Kind"/> is lan, lan6, mdns, public, public6, tailscale or custom.</summary>
/// <param name="LastWorkedUtc">For internet addresses: the last time an app reached Melon this way from outside (null = not yet).
/// Apps try every address at once, so one that doesn't work costs nothing; this only helps them pick the likeliest first.</param>
public record ServerEndpoint(string Url, string Kind, DateTime? LastWorkedUtc = null);

/// <summary>What Remote Access is set to and whether it is working. Signed-in clients can read this; only local controllers change it.</summary>
/// <param name="Method">Off, Automatic, Tailscale, ReverseProxy or Custom.</param>
/// <param name="State">Off, Starting, Waiting (nobody has connected from outside yet), Working or Problem.</param>
/// <param name="LastOutsideUtc">The last time a client reached Melon from outside the home network.</param>
public record RemoteAccessStatus(bool Enabled, string Method, bool RouterPort, bool IPv6, bool Pkarr, string? PublicAddress,
    int ExternalPort, string State, string? Detail, DateTime? LastOutsideUtc = null, string? PublicUrl = null);

/// <summary>Everything a client needs to find this server again. Encoded in invite and sign-in QR codes.</summary>
/// <param name="Fingerprint">SHA-256 of the HTTPS certificate (DER), base64url. Clients pin it.</param>
/// <param name="PkarrKey">z-base32 public key the server publishes its addresses under, when Pkarr is on.</param>
public record ConnectionInfo(string ServerId, string Name, string? Fingerprint, ServerEndpoint[] Endpoints, string? PkarrKey,
    RemoteAccessStatus Remote);

/// <summary>What Remote Access should use. <paramref name="Method"/> is Automatic, Tailscale or ReverseProxy.</summary>
/// <param name="PublicUrl">ReverseProxy only: the address clients use, such as https://music.example.com.</param>
public record RemoteAccessOptions(string Method, bool OpenRouterPort, bool UseIPv6, bool PublishPkarr, int ExternalPort = 0, string? PublicUrl = null);

/// <param name="Option">RouterPort, IPv6, Pkarr, Tailscale or Advanced.</param>
/// <param name="Level">Required, Recommended, Optional or Unavailable.</param>
public record NetworkRecommendation(string Option, string Level, string Reason);

/// <summary>A look at this network before turning on Remote Access.</summary>
/// <param name="RouterWanAddress">The public side of the router, as the router reports it (UPnP / NAT-PMP).</param>
/// <param name="Cgnat">The internet provider shares one public address between customers.</param>
/// <param name="DoubleNat">Two routers in a row (for example the provider's modem-router and your own).</param>
/// <param name="AddressKind">Dynamic, Static or Unknown.</param>
public record NetworkDiagnostics(DateTime CheckedUtc, string? PublicIPv4, string? RouterWanAddress, bool RouterFound, string? RouterProtocol,
    bool Cgnat, bool DoubleNat, string? IPv6Address, bool IPv6Works, string AddressKind, string? AddressKindReason,
    string? TailscaleAddress, string? TailscaleName, string[] Hops, string[] LanAddresses, NetworkRecommendation[] Recommendations, string[] Notes);

/// <param name="Mode">Automatic (Melon manages ports and its own certificate) or Custom (your listening URL and certificate).</param>
/// <param name="BroadcastName">The name other devices see on the local network. Empty means "Melon on {computer}".</param>
/// <param name="ServerLink">The server's address plus how to find it, for a "connect to this server" QR code (no sign-in code).</param>
public record NetworkOverview(string Mode, int HttpPort, int HttpsPort, string ListeningUrl, string? CertificatePath, string? Fingerprint,
    bool Broadcast, string BroadcastName, string BroadcastNameInUse, string ServiceType, string? BroadcastProblem, ServerEndpoint[] Endpoints,
    RemoteAccessStatus Remote, NetworkDiagnostics? Diagnostics, string[] BlockedAddresses, string? PkarrKey, bool TailscaleInstalled,
    string? ServerLink = null);

/// <summary>An address that looks like it's up to no good, from the security log and remote traffic.</summary>
/// <param name="Local">The address is on the home network.</param>
public record SecuritySuggestion(string Address, string Reason, int Count, DateTime LastSeenUtc, bool Local);

/// <summary>A client that reached Melon from outside the home network.</summary>
public record RemoteClient(string Address, DateTime FirstSeenUtc, DateTime LastSeenUtc, long Requests, long Denied, long NotFound,
    string? Username, bool Blocked);

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
    /// <summary>Upload size limits in megabytes (artwork 1–200, playlist imports 1–100). Not exposed over the web API.</summary>
    void SetUploadLimits(int artMegabytes, int playlistMegabytes);
    /// <summary>Server log size: megabytes per file (1–1024) and files kept (1–100). Applies after a restart.</summary>
    void SetLogLimits(int fileMegabytes, int filesKept);
    /// <summary>Signs out every device: rotates the token signing key and revokes all sessions.</summary>
    void SignOutEveryone();
    void SetAppearance(Appearance appearance);
    void ResetColors();
    void SetListeningUrl(string url);
    void SetMongoConnection(string connectionString);
    void SetCertificate(string path, string password);
    void DisableCertificate();
    /// <summary>Ports, local network broadcast, Remote Access, the last network check and blocked addresses.</summary>
    NetworkOverview GetNetwork();
    /// <summary>Melon picks its ports, makes its own HTTPS certificate and broadcasts on the local network. Applies after a restart.</summary>
    void UseAutomaticNetworking(int httpPort = 14524, int httpsPort = 14525);
    /// <summary>Your own listening URL and optional certificate (.pfx). Turns off Melon's automatic networking. Applies after a restart.</summary>
    void UseCustomNetworking(string listeningUrl, string certificatePath, string certificatePassword);
    /// <summary>Local network broadcast (mDNS, service type _melon._tcp). An empty name means "Melon on {computer}".</summary>
    void SetBroadcast(bool enabled, string name);
    /// <summary>Checks the public address, CGNAT, double NAT, IPv6, the router's port opening and whether the address changes.</summary>
    Task<NetworkDiagnostics> DiagnoseNetwork();
    Task<RemoteAccessStatus> EnableRemoteAccess(RemoteAccessOptions options);
    Task DisableRemoteAccess();
    /// <summary>Addresses that look suspicious from the security log and remote traffic, worst first.</summary>
    SecuritySuggestion[] GetSecuritySuggestions();
    /// <summary>Clients seen from outside the home network since Melon started.</summary>
    RemoteClient[] GetRemoteClients();
    /// <summary>Refuses every request from an address (IPv4, IPv6 or CIDR range).</summary>
    void BlockAddress(string address, string reason);
    void UnblockAddress(string address);
    /// <summary>Stops suggesting this address until it does something new.</summary>
    void DismissSuggestion(string address);
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
    /// <summary>Recent sign-ins, failures and account changes, newest first (kept 90 days).</summary>
    SecurityEvent[] GetSecurityEvents(int count = 100, bool warningsOnly = false);
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
