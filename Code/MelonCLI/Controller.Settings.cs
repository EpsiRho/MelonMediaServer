using System.Globalization;
using System.Text.Json;
using Melon.Controllers;
using Melon.Interface;
using Admin = Melon.Interface.IMelonAdministration;

namespace MelonCLI;

internal sealed partial class Controller
{
    private ReleaseInfo? release;
    private string pluginSort = "Name";
    private void Networking(Screen page)
    {
        bool https = settings.ListeningUrl.StartsWith("https:", StringComparison.OrdinalIgnoreCase);
        page.Body.Add(new Cards([
            new Card(T("Listening URL"), settings.ListeningUrl),
            new Card("HTTPS", T(https ? "Enabled" : "Disabled"), https ? null : T("Traffic is not encrypted"), Tone.Warn, Dot: https ? Tone.Good : Tone.Warn)], 30));
        if (!string.IsNullOrEmpty(settings.CertificatePath))
            page.Body.Add(new Panel(T("Certificate (.pfx)"), new Para(settings.CertificatePath)));
        page.Items.Add(Entry.Header(T("Address")));
        page.Items.Add(Action("Change listening URL", async () =>
        {
            await Write(nameof(Admin.SetListeningUrl), new { url = await Ask("Listening URL", settings.ListeningUrl, help: "http://*:14524") });
        }));
        page.Items.Add(Entry.Header(T("Security")));
        page.Items.Add(Action("Apply HTTPS", async () =>
        {
            var path = await ui.PickPath(T("Certificate (.pfx)"), false, settings.CertificatePath ?? "", ".pfx", ".p12");
            var password = await Ask("Certificate password", secret: true);
            var url = await Port("https");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException(T("Certificate (.pfx)"));
            if (path != settings.CertificatePath || password.Length > 0)
                await Write(nameof(Admin.SetCertificate), new { path = Path.GetFullPath(path), password });
            await Write(nameof(Admin.SetListeningUrl), new { url });
        }));
        if (https || !string.IsNullOrEmpty(settings.CertificatePath))
            page.Items.Add(Action("Disable HTTPS", async () =>
            {
                var url = await Port("http");
                await Confirm("Disable HTTPS?", url); await Write(nameof(Admin.SetListeningUrl), new { url }); await Write(nameof(Admin.DisableCertificate));
            }, Tone.Warn));
    }
    private async Task<string> Port(string scheme)
    {
        var current = settings.ListeningUrl;
        var normalized = current.Replace("://*:", "://localhost:").Replace("://+:", "://localhost:");
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)) throw new ArgumentException(T("Invalid listening URL."));
        if (!int.TryParse(await Ask(scheme == "https" ? "HTTPS port" : "HTTP port", uri.Port.ToString()), out var port) || port is < 1 or > 65535)
            throw new ArgumentException(T("Enter a valid port."));
        string host = current.Contains("://*:") ? "*" : current.Contains("://+:") ? "+" : uri.Host;
        return $"{scheme}://{host}:{port}";
    }

    private async Task Plugins(Screen page)
    {
        var all = await Read<PluginInfo[]>(nameof(Admin.GetPlugins));
        int enabled = all.Count(p => p.IsEnabled);
        page.Body.Add(new Cards([
            new Card(T("Installed"), $"{all.Length:N0}"),
            new Card(T("Enabled"), $"{enabled:N0}"),
            new Card(T("Disabled"), $"{all.Length - enabled:N0}")], 16));
        if (all.Length == 0) page.Body.Add(new Callout(T("Drop plugin folders into the Plugins folder, then rescan."), Tone.Info, T("No plugins installed.")));
        page.Items.Add(Entry.Header(T("Installed")));
        foreach (var plugin in all.OrderBy(p => pluginSort == "Enabled first" ? !p.IsEnabled : pluginSort == "Disabled first" && p.IsEnabled).ThenBy(p => p.Name))
            page.Items.Add(new Entry(plugin.Name, () => Plugin(plugin))
            {
                Glyph = plugin.IsEnabled ? "●" : "○", GlyphTone = plugin.IsEnabled ? Tone.Good : Tone.Faint,
                Value = plugin.Version, Tone = plugin.IsEnabled ? Tone.Normal : Tone.Muted
            });
        page.Items.Add(Setting("Sort by", T(pluginSort), async () => pluginSort = await Choose("Sort by", ["Name", "Enabled first", "Disabled first"], current: pluginSort)));
        page.Items.Add(Entry.Header(T("Folder")));
        page.Items.Add(Action("Open folder", () => Platform.Open(Path.Combine(status!.DataDirectory, "Plugins"))));
        page.Items.Add(Action("Rescan folder", async () => { await Write(nameof(Admin.ReloadPlugins), new { rescan = true }); Say(T("Plugins rescanned.")); }));
        page.Items.Add(Action("Reload plugins", async () => { await Write(nameof(Admin.ReloadPlugins), new { rescan = false }); Say(T("Plugins reloaded.")); }));
    }

    private async Task Plugin(PluginInfo plugin)
    {
        var identity = new { name = plugin.Name, author = plugin.Authors };
        while (true)
        {
            var page = new Screen(plugin.Name);
            page.Body.Add(new Cards([
                new Card(T("Status"), T(plugin.IsEnabled ? "Enabled" : "Disabled"), Dot: plugin.IsEnabled ? Tone.Good : Tone.Faint),
                new Card(T("Version"), plugin.Version), new Card(T("Author"), plugin.Authors)], 18));
            if (!string.IsNullOrWhiteSpace(plugin.Description)) page.Body.Add(new Panel(T("About"), new Para(plugin.Description)));
            PluginPage? menu = null;
            if (plugin.IsEnabled)
                try { menu = await Read<PluginPage>(nameof(Admin.GetPluginPage), identity); }
                catch (InvalidOperationException ex) when (ex.Message == "Plugin has no active controller page.") { }
            var values = menu?.Fields.ToDictionary(f => f.Key, f => f.Value) ?? [];
            bool reload = false;
            while (!reload)
            {
                page.Items.Clear();
                page.Items.Add(Action(plugin.IsEnabled ? "Disable" : "Enable", async () =>
                {
                    if (!await Read<bool>(nameof(Admin.SetPluginEnabled), new { identity.name, identity.author, enabled = !plugin.IsEnabled }))
                        throw new InvalidOperationException(T("Plugin unavailable."));
                    plugin = plugin with { IsEnabled = !plugin.IsEnabled }; reload = true;
                }, plugin.IsEnabled ? Tone.Warn : Tone.Normal));
                if (menu != null)
                {
                    if (menu.Fields.Length > 0) page.Items.Add(Entry.Header(T("Settings")));
                    foreach (var field in menu.Fields)
                    {
                        var current = values[field.Key];
                        bool argb = field.Type == "argb";
                        int color = argb ? int.Parse(current.ToString()!, CultureInfo.InvariantCulture) : 0;
                        string shown = argb ? $"#{unchecked((uint)color):X8}" : Convert.ToString(current, CultureInfo.InvariantCulture) ?? "";
                        page.Items.Add(new Entry(field.Label, async () =>
                        {
                            values[field.Key] = field.Type switch
                            {
                                "boolean" => await Choose(field.Label, ["Enabled", "Disabled"]) == "Enabled",
                                "text" => await ui.Input(field.Label, shown),
                                "number" => double.Parse(await ui.Input(field.Label, shown), CultureInfo.InvariantCulture),
                                "argb" => await ui.PickColor(field.Label, color),
                                _ => current!
                            };
                        }) { Value = shown, Glyph = argb ? "■" : null, GlyphColor = argb ? System.Drawing.Color.FromArgb(255, System.Drawing.Color.FromArgb(color)) : null });
                    }
                    if (menu.Actions.Length > 0) page.Items.Add(Entry.Header(T("Actions")));
                    foreach (var action in menu.Actions)
                        page.Items.Add(new Entry(action.Label, async () =>
                        {
                            var result = await Call(nameof(Admin.InvokePlugin), new { identity.name, identity.author, action = action.Key, values });
                            if (result.ValueKind == JsonValueKind.String) await Show(plugin.Name, result.GetString()!);
                            reload = true;
                        }));
                }
                var row = await ui.Menu(page); if (row == null) return;
                try { await row.Run!(); } catch (Back) { }
            }
        }
    }

    private void SystemPage(Screen page)
    {
        page.Body.Add(new Cards([
            new Card(T("Version"), status!.Version, status.Branch),
            new Card(T("Updates"), release == null ? T("Not checked") : release.UpdateAvailable ? release.LatestVersion : T("Up to date"),
                release?.UpdateAvailable == true ? T("Update available") : null, Tone.Warn,
                Dot: release == null ? null : release.UpdateAvailable ? Tone.Warn : Tone.Good)], 22));
        page.Body.Add(new Panel(T("Environment"), new KeyValues([
            (T("Data folder"), status.DataDirectory, Tone.Normal),
            (T("Platform"), System.Runtime.InteropServices.RuntimeInformation.OSDescription, Tone.Normal)])));
        page.Items.Add(Entry.Header(T("Preferences")));
        page.Items.Add(new Entry(T("Server language"), async () =>
        {
            var codes = await Read<string[]>(nameof(Admin.GetLanguages));
            string chosen = await Pick("Server language", codes, NativeName, c => c.Equals(language, StringComparison.OrdinalIgnoreCase));
            await Write(nameof(Admin.SetDefaultLanguage), new { language = chosen });
        }) { Value = NativeName(language) });
        page.Items.Add(Setting("Controller theme", ThemeName(), Theme));
        page.Items.Add(Action("Open data folder", () => Platform.Open(status.DataDirectory)));
        page.Items.Add(Entry.Header(T("Updates")));
        page.Items.Add(Action("Check for updates", async () =>
        {
            release = await Read<ReleaseInfo>(nameof(Admin.CheckForUpdates));
            Say(T(release.UpdateAvailable ? "Update available" : "Up to date") + "  ·  " + release.LatestVersion, release.UpdateAvailable ? Tone.Warn : Tone.Good);
        }));
        page.Items.Add(Action("Latest changelog", async () =>
        {
            release ??= await Read<ReleaseInfo>(nameof(Admin.CheckForUpdates));
            await ShowBlocks("Latest changelog", new Cards([new Card(T("Latest"), release.LatestVersion), new Card(T("Installed"), release.CurrentVersion)], 20),
                new Panel(T("Release notes"), new Para(release.ReleaseNotes)));
        }));
        if (release?.UpdateAvailable == true) page.Items.Add(Action("Install update", async () =>
        {
            await Confirm("Install update", release.LatestVersion); exit = await Read<bool>(nameof(Admin.ApplyUpdate));
        }, Tone.Warn) with { Value = release.LatestVersion });
        page.Items.Add(Entry.Header(T("Server")));
        page.Items.Add(Action("Restart Melon", Restart));
        page.Items.Add(Action("Stop Melon", async () =>
        {
            await Confirm("Stop Melon", "Melon " + status.Version, true); await Write(nameof(Admin.Stop)); status = null;
        }, Tone.Danger));
    }

    private static string NativeName(string code)
    {
        try { return CultureInfo.GetCultureInfo(code).NativeName; } catch (CultureNotFoundException) { return code; }
    }

    private async Task Setup()
    {
        var username = await Ask("Admin username");
        var password = await Ask("Admin password", secret: true);
        if (string.IsNullOrWhiteSpace(username) || password.Length == 0) throw new ArgumentException(T("Enter an admin username and password."));
        var folders = (await ui.Input(T("Library folders"), string.Join("\n", settings.LibraryPaths), multiline: true, help: T("One per line")))
            .Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries).Select(Path.GetFullPath).ToArray();
        const string local = "I have MongoDB running locally", remote = "I have MongoDB running on another system", install = "I need to install MongoDB";
        var choices = new List<string>();
        if (status!.DatabaseConnected) choices.Add("Keep current connection");
        choices.Add(local); choices.Add(remote);
        if (OperatingSystem.IsWindows()) choices.Add(install);
        var mode = await Choose("MongoDB", choices);
        if (mode == install)
        {
            await Confirm(install, T("Install MongoDB on this PC?"));
            await ui.Busy(T("Installing MongoDB…")); await WindowsHost.InstallMongo(token);
            await Write(nameof(Admin.ConnectManagedMongo)); await Restart();
        }
        else if (mode == local || mode == remote)
        {
            var connectionString = await Ask("MongoDB connection URL", mode == local ? "mongodb://localhost:27017" : "", true);
            await Write(nameof(Admin.SetMongoConnection), new { connectionString }); await Restart();
        }
        var s = await Read<ServerStatus>(nameof(Admin.GetStatus));
        if (!s.DatabaseConnected) throw new IOException(T("MongoDB is disconnected"));
        if (s.PendingConversions.Length > 0) { runSetup = false; return; }
        if (s.SetupRequired)
        {
            await Write(nameof(Admin.SetLibraryPaths), new { paths = folders });
            await Write(nameof(Admin.CreateFirstAdmin), new { username, password });
        }
        section = "Library"; runSetup = deferSetup = false; Say(T("Setup complete."));
    }
}
