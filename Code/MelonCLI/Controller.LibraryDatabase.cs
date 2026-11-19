using System.Globalization;
using Melon.Interface;
using Admin = Melon.Interface.IMelonAdministration;

namespace MelonCLI;

internal sealed partial class Controller
{
    private async Task Library(Screen page)
    {
        var s = status!;
        if (s.DatabaseConnected)
        {
            var n = await Read<LibraryTotals>(nameof(Admin.GetLibraryTotals));
            int folders = settings.LibraryPaths.Length;
            page.Body.Add(new Cards([
                new Card(T("Songs"), $"{n.Songs:N0}", $"{folders:N0} {T(folders == 1 ? "folder" : "folders")}"),
                new Card(T("Albums"), $"{n.Albums:N0}", n.Albums > 0 ? $"≈ {n.Songs / (double)n.Albums:0.#} {T("songs each")}" : null),
                new Card(T("Artists"), $"{n.Artists:N0}", n.Artists > 0 ? $"≈ {n.Albums / (double)n.Artists:0.#} {T("albums each")}" : null)]));
            if (s.Scanning)
            {
                double fraction = s.Scan.FoundFiles > 0 ? s.Scan.ScannedFiles / s.Scan.FoundFiles : 0;
                page.Body.Add(new Panel(T("Library scan"), new Meter(T(s.Scan.Status ?? ""), fraction, $"{s.Scan.ScannedFiles:N0} / {s.Scan.FoundFiles:N0}", Tone.Info),
                    $"{fraction:P0}", Tone.Info, Tone.Info));
            }
        }
        if (settings.LibraryPaths.Length == 0)
            page.Body.Add(new Callout(T("Add a folder so Melon knows where your music lives."), Tone.Warn, T("No library folders yet")));

        if (s.DatabaseConnected && !s.Scanning)
        {
            page.Items.Add(Entry.Header(T("Library scan")));
            page.Items.Add(Action("Start Library Scan", () => Scan(true)) with { Value = T("new files") });
            page.Items.Add(Action("Start Full Library Scan", () => Scan(false)) with { Value = T("everything") });
        }
        page.Items.Add(Entry.Header(T("Library folders")));
        foreach (var path in settings.LibraryPaths)
            page.Items.Add(new Entry(Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) is { Length: > 0 } name ? name : path, async () =>
            {
                string choice = await Choose("Library folders", ["Open folder", "Remove"]);
                if (choice == "Open folder") await Platform.Open(path);
                else { await Confirm("Remove", path); await ChangeFolders(path, false); Say(T("Folder removed.")); }
            }) { Value = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path)) });
        page.Items.Add(Action("Add folder", async () =>
        {
            await ChangeFolders(await ui.PickPath(T("Library folder"), true, ""), true); Say(T("Folder added."));
        }));
        page.Items.Add(Entry.Header(T("Metadata")));
        page.Items.Add(Setting("Artist split indicators", Summary(settings.ArtistSplitIndicators), () => Indicators(true)));
        page.Items.Add(Setting("Genre split indicators", Summary(settings.GenreSplitIndicators), () => Indicators(false)));
    }
    private static string Summary(string[] values) => values.Length == 0 ? "—" : string.Join(" ", values.Select(v => v.Trim().Length == 0 ? "·" : v.Trim()));
    private async Task Scan(bool skip)
    {
        if (!await Read<bool>(nameof(Admin.StartScan), new { skip })) Say(T("A scan is already running."), Tone.Warn);
        else Say(T("Scan started."), Tone.Info);
    }
    private async Task ChangeFolders(string path, bool add)
    {
        var latest = await Read<ControllerSettings>(nameof(Admin.GetSettings));
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var paths = add ? latest.LibraryPaths.Append(path).Distinct(comparer) : latest.LibraryPaths.Where(p => !comparer.Equals(p, path));
        await Write(nameof(Admin.SetLibraryPaths), new { paths });
    }
    private async Task Indicators(bool artists)
    {
        var initial = artists ? settings.ArtistSplitIndicators : settings.GenreSplitIndicators;
        var value = await ui.Input(T(artists ? "Artist split indicators" : "Genre split indicators"),
            string.Join("\n", initial), multiline: true, help: T("One per line"));
        await Write(artists ? nameof(Admin.SetArtistSplitIndicators) : nameof(Admin.SetGenreSplitIndicators),
            new { indicators = value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Distinct() });
        Say(T("Saved."));
    }

    private async Task DatabasePage(Screen page)
    {
        var s = status!;
        var backups = await Read<string[]>(nameof(Admin.ListBackups));
        page.Body.Add(new Cards([
            new Card(T("Status"), T(s.DatabaseConnected ? "Connected" : "Disconnected"),
                settings.MongoServers.Length > 0 ? string.Join(", ", settings.MongoServers) : null, Dot: s.DatabaseConnected ? Tone.Good : Tone.Danger),
            new Card(T("Backups"), $"{backups.Length:N0}", backups.Length > 0 ? T("Latest") + " " + backups.Max() : T("None yet")),
            new Card(T("Queue cleanup"), settings.QueueCleanupHours < 0 ? T("Off") : $"{settings.QueueCleanupHours:0.#} h", T("Removes stale play queues"))], 24));

        page.Items.Add(Entry.Header(T("Connection")));
        page.Items.Add(Action("Change connection", async () =>
        {
            await Write(nameof(Admin.SetMongoConnection), new { connectionString = await Ask("MongoDB connection URL", secret: true, help: "mongodb://host:27017") });
            await Restart();
        }));
        if (!s.DatabaseConnected || s.SetupRequired)
            page.Items.Add(Local(T("Set up MongoDB"), () => { runSetup = true; deferSetup = false; }));

        page.Items.Add(Entry.Header(T("Backups")));
        if (s.DatabaseConnected && !s.Scanning)
            page.Items.Add(Action("Create backup", async () =>
            {
                if (!await Read<bool>(nameof(Admin.BackupDatabase))) throw new IOException(T("Backup failed."));
                Say(T("Backup created."));
            }));
        foreach (var name in backups.OrderByDescending(n => n))
            page.Items.Add(new Entry(name, async () =>
            {
                var choices = s.DatabaseConnected && !s.Scanning ? new[] { "Open backup folder", "Restore" } : ["Open backup folder"];
                if (await Choose("Backups", choices) == "Restore")
                { await Confirm("Restore backup?", name, true); await Write(nameof(Admin.RestoreBackup), new { name }); Say(T("Backup restored.")); }
                else await Platform.Open(Path.Combine(s.DataDirectory, "Exports", "DbBackups", name));
            }));

        page.Items.Add(Entry.Header(T("Maintenance")));
        page.Items.Add(Setting("Queue cleanup frequency (hours)", settings.QueueCleanupHours < 0 ? T("Off") : settings.QueueCleanupHours.ToString(CultureInfo.CurrentCulture), async () =>
        {
            var value = await ui.Input(T("Queue cleanup frequency (hours)"), settings.QueueCleanupHours.ToString(CultureInfo.CurrentCulture), help: T("Use −1 to disable cleanup."));
            if (!double.TryParse(value, out double hours) || !double.IsFinite(hours)) throw new ArgumentException(T("Enter valid hours."));
            await Write(nameof(Admin.SetQueueCleanupHours), new { hours });
        }));
        if (!s.DatabaseConnected) return;
        page.Items.Add(Entry.Header(T("Transfer")));
        page.Items.Add(Action("Import playlists and collections", Import));
        page.Items.Add(Action("Export playlist or collection", Export));
        if (!s.Scanning)
        {
            page.Items.Add(Entry.Header(T("Danger zone")));
            page.Items.Add(Action("Reset database", async () =>
            {
                await Confirm("Reset the database?", T("This deletes library data, playlists, collections, play history, and artwork."), true);
                await Write(nameof(Admin.ResetDatabase)); Say(T("Database reset."), Tone.Warn);
            }, Tone.Danger));
        }
    }

    private async Task Import()
    {
        var owners = await Read<PlaylistOwner[]>(nameof(Admin.GetPlaylistOwners));
        var owner = await Pick("Owner", owners, o => o.Username);
        var kind = await Choose("Import as", ["Playlist", "Collection"]);
        string path = await ui.PickPath(T("Choose file"), false, "", ".m3u", ".m3u8", ".pls", ".xml", ".xspf");
        if (!File.Exists(path)) throw new FileNotFoundException(path);
        int count = await Read<int>(kind == "Playlist" ? nameof(Admin.ImportPlaylists) : nameof(Admin.ImportCollections), new { path, userId = owner.Id });
        if (count > 0) Say(T("Import complete.")); else Say(T("No entries could be imported."), Tone.Warn);
    }

    private async Task<TItem> Pick<TItem>(string title, IEnumerable<TItem> values, Func<TItem, string> label, Func<TItem, bool>? current = null)
    {
        var page = new Screen(T(title));
        TItem? selected = default;
        foreach (var item in values)
            page.Items.Add(Local(label(item), () => selected = item) with { Glyph = current?.Invoke(item) == true ? "●" : null });
        if (page.Items.Count == 0) throw new InvalidOperationException(T("No entries."));
        await (await ui.Menu(page) ?? throw new Back()).Run!();
        return selected!;
    }

    private async Task Export()
    {
        string query = await Ask("Search by name");
        var matches = await Read<TransferItem[]>(nameof(Admin.SearchTransferItems), new { query });
        var item = await Pick("Export playlist or collection", matches, v => $"{v.Name}  ·  {T(v.Kind)}");
        var kind = await Choose("Format", ["M3U", "PLS", "XML"]);
        var format = "." + kind.ToLowerInvariant();
        string destination = Path.GetFullPath(await Ask("Save as", Path.Combine(Environment.CurrentDirectory, Path.GetFileName(item.Name) + format)));
        if (File.Exists(destination)) await Confirm("Replace file?", destination);
        var source = await Read<string>(nameof(Admin.ExportPlaylist), new { id = item.Id, format });
        if (!string.Equals(Path.GetFullPath(source), destination, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            File.Copy(source, destination, true);
        Say(T("Export complete.") + "  ·  " + destination);
    }
}
