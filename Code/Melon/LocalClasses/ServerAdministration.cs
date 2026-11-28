using System.Diagnostics;
using System.Drawing;
using System.Net.Http.Json;
using System.Text.Json;
using Melon.Interface;
using Melon.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using static Melon.LocalClasses.StateManager;

namespace Melon.LocalClasses;

public sealed partial class ServerAdministration : IMelonAdministration
{
    public static ServerAdministration Instance { get; } = new();
    private readonly object settingsLock = new();
    private readonly object databaseLock = new();
    private readonly object pluginLock = new();
    private static readonly HttpClient Releases = new();
    private readonly HashSet<IPlugin> active = [];
    private string? pendingMongoConnection;
    private bool restartRequired;
    private Guid runId;
    private bool connected;
    private bool needsAdmin;
    private string[] conversions = [];
    public event Action<bool>? ShutdownRequested;
    public ControllerMessages Messages { get; } = new();

    public void PublishMessage(string topic, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(message);
        Messages.Publish(topic, message);
    }

    public void BeginRun()
    {
        Security.ClearAccessCodes();
        runId = Guid.NewGuid();
        lock (settingsLock) { pendingMongoConnection = null; restartRequired = false; }
    }

    public void ConnectDatabase()
    {
        lock (databaseLock)
        {
            connected = false;
            needsAdmin = false;
            conversions = [];
            DbClient = CreateDatabaseClient(MelonSettings.MongoDbConnectionString);
            Database.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
            conversions = DbVersionManager.CheckVersionCompatibility().Split(',', StringSplitOptions.RemoveEmptyEntries);
            needsAdmin = !Database.GetCollection<User>("Users").Find(FilterDefinition<User>.Empty).Limit(1).Any();
            UserAccounts.EnsureUniqueUsernames(Database);
            AuthSessions.EnsureIndexes(Database);
            SecurityLog.EnsureIndexes(Database);
            EqFilters.EnsureIndexes(Database);
            connected = true;
            if (!needsAdmin) File.Delete(Path.Combine(melonPath, "Configs", "setup.pending"));
            if (conversions.Length > 0 && LaunchArgs.ContainsKey("allowConversion"))
                ConvertDatabase();
            // Existing libraries only got indexes after a scan; make sure search has them without one.
            if (conversions.Length == 0 || LaunchArgs.ContainsKey("allowConversion"))
            {
                var db = Database;
                Task.Run(() => MelonScanner.EnsureSearchIndexes(db));
            }
        }
    }

    private static IMongoDatabase Database => DbClient.GetDatabase("Melon");

    public ServerStatus GetStatus()
    {
        lock (databaseLock)
            return new(StateManager.Version, StateManager.Branch, melonPath, connected && DbClient.Cluster.Description.State == MongoDB.Driver.Core.Clusters.ClusterState.Connected,
                needsAdmin || File.Exists(Path.Combine(melonPath, "Configs", "setup.pending")), [.. conversions], MelonScanner.Scanning,
                new ScanProgress { FoundFiles = MelonScanner.FoundFiles, ScannedFiles = MelonScanner.ScannedFiles,
                    Status = MelonScanner.CurrentStatus ?? "Idle" }) { RunId = runId };
    }

    public ControllerSettings GetSettings()
    {
        lock (settingsLock)
        {
            var s = MelonSettings;
            var appearance = new Appearance(s.DefaultLanguage, s.UseMenuColor, s.Text.ToArgb(), s.ShadedText.ToArgb(),
                s.BackgroundText.ToArgb(), s.Highlight.ToArgb(), s.Melon.ToArgb(), s.Error.ToArgb());
            string[] servers;
            try { servers = new MongoUrl(pendingMongoConnection ?? s.MongoDbConnectionString).Servers.Select(host => host.ToString()).ToArray(); }
            catch (Exception) { servers = []; } // A controller can replace an invalid legacy connection.
            return new([.. s.LibraryPaths], [.. s.ArtistSplitIndicators], [.. s.GenreSplitIndicators],
                s.QueueCleanupWaitInHours, s.ListeningURL, Security.GetSSLConfig().PathToCert, appearance, servers, restartRequired,
                s.MaxArtUploadMegabytes, s.MaxPlaylistImportMegabytes, s.LogFileMegabytes, s.LogFilesKept);
        }
    }

    private void ChangeSettings(Action<Settings> change)
    {
        lock (settingsLock)
        {
            var settings = JsonConvert.DeserializeObject<Settings>(JsonConvert.SerializeObject(MelonSettings))!;
            settings.MongoDbConnectionString = pendingMongoConnection ?? MelonSettings.MongoDbConnectionString;
            change(settings);
            var desiredMongo = settings.MongoDbConnectionString;
            if (desiredMongo?.StartsWith("mongodb://melon-app:", StringComparison.Ordinal) == true &&
                File.Exists(Path.Combine(melonPath, "Configs", "ManagedMongoCredentials.json")) && desiredMongo == ManagedMongo.SavedConnection())
                settings.MongoDbConnectionString = ManagedMongo.Alias;
            Storage.SaveConfigFile("MelonSettings", settings, null);
            settings.MongoDbConnectionString = desiredMongo;
            // Existing API handlers use this address directly. Switch all users of the database together on restart.
            restartRequired |= settings.ListeningURL != MelonSettings.ListeningURL || settings.NetworkMode != MelonSettings.NetworkMode ||
                settings.HttpPort != MelonSettings.HttpPort || settings.HttpsPort != MelonSettings.HttpsPort ||
                settings.LogFileMegabytes != MelonSettings.LogFileMegabytes || settings.LogFilesKept != MelonSettings.LogFilesKept ||
                settings.MongoDbConnectionString != MelonSettings.MongoDbConnectionString;
            pendingMongoConnection = settings.MongoDbConnectionString;
            settings.MongoDbConnectionString = MelonSettings.MongoDbConnectionString;
            MelonSettings = settings;
            SetLanguage(LaunchArgs.GetValueOrDefault("lang", settings.DefaultLanguage));
            Messages.Publish("settings", "Server settings changed.");
        }
    }

    public LibraryTotals GetLibraryTotals()
    {
        long Count(string name) => Database.GetCollection<BsonDocument>(name).CountDocuments(FilterDefinition<BsonDocument>.Empty);
        return new(Count("Tracks"), Count("Albums"), Count("Artists"));
    }

    public TransferItem[] SearchTransferItems(string query)
    {
        var filter = Builders<BsonDocument>.Filter.Regex("Name", new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(query), "i"));
        IEnumerable<TransferItem> Find(string collection, string kind) => Database.GetCollection<BsonDocument>(collection)
            .Find(filter).Sort(Builders<BsonDocument>.Sort.Ascending("Name")).Limit(50)
            .Project(Builders<BsonDocument>.Projection.Include("_id").Include("Name")).ToList()
            .Select(row => new TransferItem(row["_id"].ToString()!, row["Name"].AsString, kind));
        return Find("Playlists", "Playlist").Concat(Find("Collections", "Collection")).OrderBy(item => item.Name).ToArray();
    }

    public string[] GetLanguages() => typeof(StateManager).Assembly.GetManifestResourceNames()
        .Where(name => name.StartsWith("Melon.Strings.UIStrings") && name.EndsWith(".resources"))
        .Select(name => name["Melon.Strings.UIStrings".Length..^".resources".Length]).Order().ToArray();

    public void SetDefaultLanguage(string language)
    {
        language = language.ToUpperInvariant();
        if (!GetLanguages().Contains(language)) throw new ArgumentException("Unknown language.");
        ChangeSettings(s => s.DefaultLanguage = language);
    }

    public void ConnectManagedMongo()
    {
        lock (databaseLock) SetMongoConnection(ManagedMongo.Connect());
    }

    public void SetLibraryPaths(string[] paths)
    {
        foreach (var path in paths)
            if (!Directory.Exists(path)) throw new ArgumentException($"Path does not exist: {path}");
        ChangeSettings(s => s.LibraryPaths = [.. paths]);
    }

    public void SetArtistSplitIndicators(string[] indicators) =>
        ChangeSettings(s => s.ArtistSplitIndicators = ValidateIndicators(indicators));
    public void SetGenreSplitIndicators(string[] indicators) =>
        ChangeSettings(s => s.GenreSplitIndicators = ValidateIndicators(indicators));
    private static List<string> ValidateIndicators(string[] values) =>
        values.Any(string.IsNullOrEmpty) ? throw new ArgumentException("Empty split indicator.") : [.. values];

    public void SetQueueCleanupHours(double hours)
    {
        if (!double.IsFinite(hours) || hours < 0 && hours != -1 || hours > TimeSpan.MaxValue.TotalHours - 1)
            throw new ArgumentException("Specify hours or -1 to disable cleanup.");
        ChangeSettings(s => s.QueueCleanupWaitInHours = hours);
    }

    public void SetUploadLimits(int artMegabytes, int playlistMegabytes)
    {
        if (artMegabytes is < 1 or > 200) throw new ArgumentException("Artwork limit must be 1–200 MB.");
        if (playlistMegabytes is < 1 or > 100) throw new ArgumentException("Playlist import limit must be 1–100 MB.");
        ChangeSettings(s => { s.MaxArtUploadMegabytes = artMegabytes; s.MaxPlaylistImportMegabytes = playlistMegabytes; });
    }

    public void SetLogLimits(int fileMegabytes, int filesKept)
    {
        if (fileMegabytes is < 1 or > 1024) throw new ArgumentException("Log file size must be 1–1024 MB.");
        if (filesKept is < 1 or > 100) throw new ArgumentException("Log files kept must be 1–100.");
        ChangeSettings(s => { s.LogFileMegabytes = fileMegabytes; s.LogFilesKept = filesKept; });
    }


    public void SignOutEveryone()
    {
        AuthSessions.SignOutEveryone(connected ? Database : null);
        Messages.Publish("users", "Every device was signed out.");
    }

    public void SetAppearance(Appearance appearance)
    {
        var language = appearance.Language.ToUpperInvariant();
        if (!typeof(StateManager).Assembly.GetManifestResourceNames().Contains($"Melon.Strings.UIStrings{language}.resources"))
            throw new ArgumentException("Unknown language.");
        ChangeSettings(s =>
        {
            s.DefaultLanguage = language;
            s.UseMenuColor = appearance.UseMenuColor;
            s.Text = Color.FromArgb(appearance.Text);
            s.ShadedText = Color.FromArgb(appearance.ShadedText);
            s.BackgroundText = Color.FromArgb(appearance.BackgroundText);
            s.Highlight = Color.FromArgb(appearance.Highlight);
            s.Melon = Color.FromArgb(appearance.Melon);
            s.Error = Color.FromArgb(appearance.Error);
        });
    }

    public void ResetColors()
    {
        lock (settingsLock)
            SetAppearance(GetSettings().Appearance with { ShadedText = Color.FromArgb(100,100,100).ToArgb(),
                BackgroundText = Color.FromArgb(66,66,66).ToArgb(), Highlight = Color.FromArgb(97,214,214).ToArgb(),
                Melon = Color.FromArgb(26,225,19).ToArgb(), Error = Color.Red.ToArgb(), Text = Color.FromArgb(204,204,204).ToArgb() });
    }

    public void SetListeningUrl(string url)
    {
        var probe = url.Replace("://*:", "://localhost:").Replace("://+:", "://localhost:");
        if (!Uri.TryCreate(probe, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || uri.UserInfo.Length != 0 || uri.PathAndQuery != "/" || uri.Fragment.Length != 0)
            throw new ArgumentException("Expected an HTTP or HTTPS listening address.");
        ChangeSettings(s => s.ListeningURL = url);
    }

    public void SetMongoConnection(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("The MongoDB address is invalid.");
        try
        {
            using var client = CreateDatabaseClient(connectionString);
            client.GetDatabase("Melon").RunCommand<BsonDocument>(new BsonDocument("ping", 1));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or MongoConfigurationException)
        { throw new ArgumentException("The MongoDB address is invalid."); }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        { throw new InvalidOperationException("Could not connect to MongoDB. Check the address and credentials."); }
        ChangeSettings(s => s.MongoDbConnectionString = connectionString);
    }

    public void SetCertificate(string path, string password)
    {
        var config = new SSLConfig { PathToCert = Path.GetFullPath(path), Password = password };
        if (Security.VerifySSLConfig(config) != "Valid") throw new ArgumentException("Invalid or expired certificate.");
        SaveCertificate(config);
    }

    public void DisableCertificate() => SaveCertificate(new SSLConfig { PathToCert = "", Password = "" });

    private void SaveCertificate(SSLConfig config)
    {
        lock (settingsLock)
        {
            Storage.SaveConfigFile("SSLConfig", new SSLConfig { PathToCert = config.PathToCert, Password = config.Password }, ["Password"]);
            Security.SetSSLConfig(config);
            restartRequired = true;
            Messages.Publish("settings", "HTTPS configuration changed.");
        }
    }

    public string CreateFirstAdmin(string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        lock (databaseLock)
        {
            if (!connected) throw new InvalidOperationException("Database unavailable.");
            var users = Database.GetCollection<User>("Users");
            if (users.Find(FilterDefinition<User>.Empty).Limit(1).Any())
                throw new InvalidOperationException("First-run setup has already finished.");
            const string firstAdminId = "000000000000000000000001";
            users.InsertOne(new User { _id = firstAdminId, Username = username, Type = "Admin",
                Password = Security.HashPassword(password, out var salt), Salt = salt, LastLogin = DateTime.UtcNow,
                Bio = "", FavAlbum = "", FavTrack = "", Friends = [] });
            SecurityLog.Record(SecurityEventKinds.AccountCreated, firstAdminId, username, detail: "First administrator");
            needsAdmin = false;
            File.Delete(Path.Combine(melonPath, "Configs", "setup.pending"));
            Messages.Publish("setup", "First administrator created.");
            Networking.RemoteAccessService.Kick(); // setup is done: start the local network broadcast
            return firstAdminId;
        }
    }

    private T WithIdleDatabase<T>(Func<T> operation)
    {
        lock (databaseLock)
        {
            if (!connected) throw new InvalidOperationException("Database unavailable.");
            if (MelonScanner.Scanning) throw new InvalidOperationException("A scan is running.");
            return operation();
        }
    }

    public void ConvertDatabase() => WithIdleDatabase(() =>
    {
        if (conversions.Length > 0) DbVersionManager.ConvertCollections(conversions);
        conversions = DbVersionManager.CheckVersionCompatibility().Split(',', StringSplitOptions.RemoveEmptyEntries);
        return true;
    });

    public bool StartScan(bool skip = false)
    {
        lock (databaseLock)
        {
            if (!connected || conversions.Length != 0) throw new InvalidOperationException("Database is not ready to scan.");
            return MelonScanner.TryStartScan(skip);
        }
    }

    public bool BackupDatabase() => WithIdleDatabase(Transfer.ExportDb);
    public string[] ListBackups() => Transfer.ListBackups();
    public void RestoreBackup(string name) => WithIdleDatabase(() =>
    {
        Transfer.ImportDb(name);
        conversions = DbVersionManager.CheckVersionCompatibility().Split(',', StringSplitOptions.RemoveEmptyEntries);
        needsAdmin = !Database.GetCollection<User>("Users").Find(FilterDefinition<User>.Empty).Limit(1).Any();
        return true;
    });
    public void ResetDatabase() => WithIdleDatabase(() => { MelonScanner.ResetDb(); return true; });
    public PlaylistOwner[] GetPlaylistOwners() => Database.GetCollection<User>("Users")
        .Find(FilterDefinition<User>.Empty).Project(u => new PlaylistOwner(u._id, u.Username)).ToList().ToArray();

    public int ImportPlaylists(string path, string userId) => ImportMediaLists(path, userId, false);
    public int ImportCollections(string path, string userId) => ImportMediaLists(path, userId, true);

    private int ImportMediaLists(string path, string userId, bool collection)
    {
        if (!Database.GetCollection<User>("Users").Find(u => u._id == userId).Any())
            throw new ArgumentException("Unknown playlist owner.");
        var files = Directory.Exists(path) ? Directory.GetFiles(path) : [path];
        return files.Count(file => Transfer.ImportPlaylist(file, userId, collection));
    }
    public string ExportPlaylist(string id, string format)
    {
        var export = Transfer.GetPlaylistExport(id, format);
        var directory = Directory.CreateDirectory(Path.Combine(melonPath, "Exports", "Playlists"));
        var path = Path.Combine(directory.FullName, export.Name);
        var temporary = Path.Combine(directory.FullName, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, export.Contents);
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
        return path;
    }

    public PluginInfo[] GetPlugins()
    {
        lock (pluginLock)
            return Plugins.Select(p => new PluginInfo(p.Name, p.Authors, p.Version, p.Description, active.Contains(p))).ToArray();
    }

    private static IPlugin? FindPlugin(string name, string author) => Plugins.Find(p => p.Name == name && p.Authors == author);
    private void StartPlugin(IPlugin plugin)
    {
        plugin.LoadMelonCommands(Host);
        plugin.LoadMelonServerCommands(WebApi);
        if (plugin.Execute() != 0) throw new InvalidOperationException($"Plugin failed to start: {plugin.Name}");
        active.Add(plugin);
    }

    public bool SetPluginEnabled(string name, string author, bool enabled)
    {
        lock (pluginLock)
        {
            var plugin = FindPlugin(name, author);
            if (plugin is null) return false;
            var key = $"{name}:{author}";
            var next = DisabledPlugins.Where(k => k != key).ToList();
            if (!enabled) next.Add(key);
            bool wasActive = active.Contains(plugin);
            if (enabled && !wasActive) StartPlugin(plugin);
            if (!enabled && wasActive) StopPlugin(plugin);
            try { Storage.SaveConfigFile("DisabledPlugins", next, null); }
            catch
            {
                if (enabled && !wasActive) StopPlugin(plugin);
                if (!enabled && wasActive) StartPlugin(plugin);
                throw;
            }
            DisabledPlugins = next;
            Messages.Publish("plugins", "Plugin activation changed.");
            return true;
        }
    }

    private void StopPlugin(IPlugin plugin)
    {
        if (plugin.Destroy() != 0) throw new InvalidOperationException($"Plugin failed to stop: {plugin.Name}");
        active.Remove(plugin);
        ArtworkService.Instance.UnregisterAssembly(plugin.GetType().Assembly);
    }

    public void UnloadPlugins()
    {
        lock (pluginLock)
        {
            foreach (var plugin in active.ToArray()) StopPlugin(plugin);
            foreach (var context in PluginsContexts) context.Unload();
            PluginsContexts.Clear();
            Plugins.Clear();
        }
    }

    public void ReloadPlugins(bool rescan)
    {
        lock (pluginLock)
        {
            if (MelonFlags.DisablePlugins || LaunchArgs.ContainsKey("disablePlugins")) return;
            if (rescan) { UnloadPlugins(); PluginsManager.LoadPlugins(); }
            else foreach (var p in active.ToArray()) StopPlugin(p);
            foreach (var p in Plugins.Where(p => !DisabledPlugins.Contains($"{p.Name}:{p.Authors}")))
            {
                try { StartPlugin(p); }
                catch (Exception ex) { Serilog.Log.Error(ex, "Failed to start plugin {Name}", p.Name); }
            }
            Messages.Publish("plugins", "Plugin list refreshed.");
        }
    }

    public Dictionary<string,string> GetHelp()
    {
        lock (pluginLock)
        {
            var help = new Dictionary<string,string> { ["--version"] = "Show version", ["--disablePlugins"] = "Start without plugins",
                ["--allowConversion"] = "Back up and convert incompatible collections", ["--lang"] = "Override language" };
            foreach (var plugin in active)
                foreach (var item in plugin.GetHelpOptions()) help.TryAdd(item.Key, item.Value);
            return help;
        }
    }

    private IControllerPlugin ControllerPlugin(string name, string author)
    {
        var plugin = FindPlugin(name, author);
        return plugin is IControllerPlugin controller && active.Contains(plugin) ? controller :
            throw new InvalidOperationException("Plugin has no active controller page.");
    }
    public PluginPage GetPluginPage(string name, string author)
    {
        lock (pluginLock) return ControllerPlugin(name, author).GetPage();
    }
    public object? InvokePlugin(string name, string author, string action, JsonElement values)
    {
        lock (pluginLock)
        {
            var result = ControllerPlugin(name, author).Invoke(action, values);
            Messages.Publish("plugins", $"Plugin action completed: {name}");
            return result;
        }
    }

    public Func<GithubResponse, Task>? PrepareWindowsUpdate { private get; set; }
    private readonly SemaphoreSlim updateLock = new(1);
    /// <summary>The newest release on this server's branch (tags look like "Beta-0.9.0.0").</summary>
    private static async Task<GithubResponse> FetchRelease()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/EpsiRho/MelonMediaServer/releases?per_page=100");
        request.Headers.UserAgent.ParseAdd("MelonUpdater");
        using var response = await Releases.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var releases = await response.Content.ReadFromJsonAsync<GithubResponse[]>() ?? throw new IOException("Empty release list.");
        // A release counts by its tag, or by its title when the tag isn't "Branch-version" (see ReleaseTag.TagOf).
        var latest = ReleaseTag.Latest(releases.Where(r => !r.draft).Select(r => ReleaseTag.TagOf(r.tag_name, r.name)).OfType<string>(), StateManager.Branch)
            ?? throw new IOException($"No releases on the {StateManager.Branch} branch.");
        return releases.First(r => !r.draft && ReleaseTag.TagOf(r.tag_name, r.name) == latest.Tag);
    }

    private static bool IsNewer(GithubResponse release) =>
        ReleaseTag.TryParse(ReleaseTag.TagOf(release.tag_name, release.name), out var tag) && tag.Version > System.Version.Parse(StateManager.Version);

    public async Task<ReleaseInfo> CheckForUpdates()
    {
        var release = await FetchRelease();
        bool available = IsNewer(release);
        return new(available, StateManager.Version, ReleaseTag.TagOf(release.tag_name, release.name) ?? release.tag_name, release.body ?? "");
    }

    public async Task<bool> ApplyUpdate()
    {
        if (!await updateLock.WaitAsync(0)) throw new InvalidOperationException("An update is already being prepared.");
        try
        {
            var release = await FetchRelease();
            if (!IsNewer(release)) return false;
            if (OperatingSystem.IsWindows())
            {
                var prepare = PrepareWindowsUpdate ?? throw new InvalidOperationException("The host does not support Windows bundle updates.");
                await prepare(release);
            }
            else StartInstaller();
            Stop();
            return true;
        }
        finally { updateLock.Release(); }
    }

    private static void StartInstaller()
    {
        // Bundle layout: <root>/Server (this process) and <root>/Installer.
        var server = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var root = server.Name.Equals("Server", StringComparison.OrdinalIgnoreCase) ? server.Parent!.FullName : server.FullName;
        var directory = Directory.Exists(Path.Combine(root, "Installer")) ? Path.Combine(root, "Installer") : server.FullName;
        var program = Path.Combine(directory, OperatingSystem.IsWindows() ? "MelonInstaller.exe" : "MelonInstaller");
        // Releases carry their own .NET, so the installer is always a native exe; there's no "dotnet MelonInstaller.dll" to fall back on.
        if (!File.Exists(program)) throw new FileNotFoundException("MelonInstaller was not found next to the server.", program);
        var start = new ProcessStartInfo(program) { UseShellExecute = false };
        foreach (var arg in new[] { "-update", "-restart", "-installPath", root, "-branch", StateManager.Branch, "-lang", Language }) start.ArgumentList.Add(arg);
        Process.Start(start);
    }

    public void Stop() => ShutdownRequested?.Invoke(false);
    public void Restart() => ShutdownRequested?.Invoke(true);
}
