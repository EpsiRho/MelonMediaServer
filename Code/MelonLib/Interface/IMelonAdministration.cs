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
    Task<ReleaseInfo> CheckForUpdates();
    Task<bool> ApplyUpdate();
    void PublishMessage(string topic, string message);
    void Restart();
    void Stop();
}
