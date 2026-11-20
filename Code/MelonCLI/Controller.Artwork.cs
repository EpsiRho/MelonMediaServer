using Melon.Interface;
using Admin = Melon.Interface.IMelonAdministration;

namespace MelonCLI;

internal sealed partial class Controller
{
    private const string TestArtist = "Radiohead";

    private async Task ArtworkSettings()
    {
        while (true)
        {
            var art = await Read<ArtworkStatus>(nameof(Admin.GetArtwork));
            var page = new Screen(T("Artist artwork"));
            int enabled = art.Providers.Count(p => p.Enabled);
            page.Body.Add(new Cards([
                new Card(T("Providers"), $"{enabled:N0} / {art.Providers.Length:N0}", T("Enabled"), Dot: enabled > 0 ? Tone.Good : Tone.Warn),
                new Card(T("After scans"), T(art.FetchAfterScan ? "On" : "Off"), T("Fetch missing artwork")),
                new Card(T("Status"), art.Running ? $"{art.Processed:N0} / {art.Total:N0}" : T("Idle"),
                    art.Running ? T("Fetching artwork…") : null, Tone.Info, Dot: art.Running ? Tone.Info : null)], 22));
            if (!art.Running && art.LastResult != null) page.Body.Add(new Callout(T(art.LastResult), Tone.Info, T("Last fetch")));
            if (enabled == 0) page.Body.Add(new Callout(T("Enable a provider to fill in missing artist pictures."), Tone.Warn, T("No artwork providers are enabled.")));
            page.Body.Add(new Panel(T("Priority"), new Para(T("Melon asks enabled providers from the top down and keeps the first image each one finds."), Tone.Muted)));

            page.Items.Add(Entry.Header(T("Providers")));
            foreach (var provider in art.Providers)
            {
                var current = provider;
                page.Items.Add(new Entry(provider.Name, () => ArtworkProvider(current.Id))
                {
                    Glyph = provider.Enabled ? "●" : "○", GlyphTone = provider.Enabled ? Tone.Good : Tone.Faint,
                    Value = $"#{provider.Priority + 1}" + (!provider.Installed ? " · " + T("Not loaded")
                        : provider.RequiresApiKey && !provider.HasApiKey ? " · " + T("Needs API key") : ""),
                    Tone = provider.Enabled ? Tone.Normal : Tone.Muted
                });
            }
            page.Items.Add(Entry.Header(T("Fetching")));
            page.Items.Add(Setting("Fetch after library scans", T(art.FetchAfterScan ? "On" : "Off"),
                () => Write(nameof(Admin.SetArtworkFetchAfterScan), new { enabled = !art.FetchAfterScan })));
            if (!art.Running)
            {
                page.Items.Add(Action("Fetch missing artwork", () => FetchArtwork(false)));
                page.Items.Add(Action("Retry artists checked recently", () => FetchArtwork(true)));
            }
            page.Notice = notice; page.NoticeTone = noticeTone;
            var row = await ui.Menu(page);
            if (row == null) return;
            notice = "";
            try { await row.Run!(); } catch (Back) { }
        }
    }

    private async Task FetchArtwork(bool force)
    {
        if (await Read<bool>(nameof(Admin.FetchArtistArtwork), new { force })) Say(T("Fetching artwork…"), Tone.Info);
        else Say(T("Artwork can’t be fetched while a scan or another fetch is running."), Tone.Warn);
    }

    private async Task ArtworkProvider(string id)
    {
        while (true)
        {
            var art = await Read<ArtworkStatus>(nameof(Admin.GetArtwork));
            var all = art.Providers;
            var p = all.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException(T("Plugin unavailable."));
            var page = new Screen(p.Name);
            page.Body.Add(new Cards([
                new Card(T("Status"), T(p.Enabled ? "Enabled" : "Disabled"), Dot: p.Enabled ? Tone.Good : Tone.Faint),
                new Card(T("Priority"), $"#{p.Priority + 1}", $"{T("of")} {all.Length}"),
                new Card(T("API key"), T(!p.RequiresApiKey ? "Not needed" : p.HasApiKey ? "Saved" : "Not set"),
                    Dot: p.RequiresApiKey ? p.HasApiKey ? Tone.Good : Tone.Warn : null)], 20));
            if (!p.Installed)
                page.Body.Add(new Callout(T("Its settings are kept until the plugin is back."), Tone.Warn, T("This provider’s plugin is not loaded.")));
            var about = new List<(string, string, Tone)> { (T("Source"), T(p.BuiltIn ? "Built in" : "Plugin"), Tone.Normal) };
            if (p.Kinds.Length > 0) about.Add((T("Provides"), string.Join(", ", p.Kinds.Select(k => T(k == nameof(ArtworkKind.ArtistPicture) ? "Artist pictures" : "Artist banners"))), Tone.Normal));
            if (p.ApiKeyUrl != null) about.Add((T("Get a key"), p.ApiKeyUrl, Tone.Accent));
            page.Body.Add(new Panel(T("About"), new Stack([new Para(p.Description), new KeyValues(about)])));

            if (p.Installed)
            {
                page.Items.Add(Action(p.Enabled ? "Disable" : "Enable", async () =>
                {
                    if (!p.Enabled && p.RequiresApiKey && !p.HasApiKey) { await SetupKey(p); return; }
                    await Write(nameof(Admin.SetArtworkProviderEnabled), new { id, enabled = !p.Enabled });
                }, p.Enabled ? Tone.Warn : Tone.Normal));
            }
            page.Items.Add(Entry.Header(T("Priority")));
            if (p.Priority > 0) page.Items.Add(Action("Move up", () => MoveProvider(all, id, -1)));
            if (p.Priority < all.Length - 1) page.Items.Add(Action("Move down", () => MoveProvider(all, id, 1)));
            if (p.Priority > 0) page.Items.Add(Action("Make first", () => Write(nameof(Admin.SetArtworkProviderOrder), new { ids = new[] { id } })));
            if (p.RequiresApiKey)
            {
                page.Items.Add(Entry.Header(T("API key")));
                page.Items.Add(Action(p.HasApiKey ? "Change API key" : "Set up", () => SetupKey(p)));
                if (p.ApiKeyUrl != null) page.Items.Add(Action("Get an API key", () => Platform.Launch(p.ApiKeyUrl)) with { Value = new Uri(p.ApiKeyUrl).Host });
                if (p.HasApiKey) page.Items.Add(Action("Remove API key", async () =>
                {
                    await Confirm("Remove API key", T("The provider is disabled until you add a key again."));
                    await Write(nameof(Admin.SetArtworkProviderKey), new { id, apiKey = "" });
                }, Tone.Danger));
            }
            if (p.Installed && (!p.RequiresApiKey || p.HasApiKey))
            {
                page.Items.Add(Entry.Header(T("Check")));
                page.Items.Add(Action("Test lookup", async () =>
                {
                    var artistName = await Ask("Artist name", TestArtist);
                    await ui.Busy(T("Looking up artwork…"));
                    Say(await Read<string>(nameof(Admin.TestArtworkProvider), new { id, artistName }), Tone.Info);
                }));
            }
            page.Notice = notice; page.NoticeTone = noticeTone;
            var row = await ui.Menu(page);
            if (row == null) return;
            notice = "";
            try { await row.Run!(); } catch (Back) { }
        }
    }

    /// <summary>Saves a key, which also enables the provider, then proves it works with a real lookup.</summary>
    private async Task SetupKey(ArtworkProviderInfo p)
    {
        var help = p.ApiKeyUrl == null ? "" : $"{T("Get a free key at")} {p.ApiKeyUrl}";
        var apiKey = (await ui.Input($"{p.Name} · {T("API key")}", "", secret: true, help: help)).Trim();
        if (apiKey.Length == 0) throw new ArgumentException(T("Enter an API key."));
        await Write(nameof(Admin.SetArtworkProviderKey), new { id = p.Id, apiKey });
        await ui.Busy(T("Checking the key…"));
        try { Say(await Read<string>(nameof(Admin.TestArtworkProvider), new { id = p.Id, artistName = TestArtist }), Tone.Good); }
        catch (InvalidOperationException ex) { Say(ex.Message, Tone.Warn); }
    }

    private async Task MoveProvider(ArtworkProviderInfo[] all, string id, int step)
    {
        var ids = all.OrderBy(x => x.Priority).Select(x => x.Id).ToList();
        int from = ids.IndexOf(id), to = Math.Clamp(from + step, 0, ids.Count - 1);
        (ids[from], ids[to]) = (ids[to], ids[from]);
        await Write(nameof(Admin.SetArtworkProviderOrder), new { ids });
    }
}
