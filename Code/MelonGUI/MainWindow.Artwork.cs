using Melon.Interface;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MelonGUI;

public sealed partial class MainWindow
{
    private const string TestArtist = "Radiohead";
    private ProgressBar? artworkBar;
    private TextBlock? artworkText;

    /// <summary>Called from the poll timer. Fetches publish no per-artist messages, so progress is read here.</summary>
    private async Task UpdateArtworkProgress()
    {
        if (artworkBar == null || artworkText == null || tab != "Library") return;
        var art = await Read<ArtworkStatus>(nameof(IMelonAdministration.GetArtwork));
        if (!art.Running) { await Render(); return; } // Finished: show the result and the fetch buttons again.
        ShowArtworkProgress(art);
    }

    private void ShowArtworkProgress(ArtworkStatus art)
    {
        if (artworkBar == null || artworkText == null) return;
        artworkText.Text = art.Total > 0 ? $"{T("Fetching artwork…")}   {art.Processed:N0} / {art.Total:N0}" : T("Fetching artwork…");
        artworkBar.IsIndeterminate = art.Total == 0;
        artworkBar.Maximum = Math.Max(1, art.Total);
        artworkBar.Value = Math.Min(art.Processed, art.Total);
    }

    private async Task ArtworkSection(StackPanel page)
    {
        var art = await Read<ArtworkStatus>(nameof(IMelonAdministration.GetArtwork));
        Section(page, "Artist artwork");

        var afterScan = new ToggleSwitch { IsOn = art.FetchAfterScan, OnContent = T("On"), OffContent = T("Off") };
        afterScan.Toggled += async (_, _) => await Act(() => Command(nameof(IMelonAdministration.SetArtworkFetchAfterScan), new { enabled = afterScan.IsOn }));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(afterScan, T("Fetch after library scans"));
        var fetchState = art.LastResult is { } last ? T(last) : T("Idle");
        var fetchButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (!art.Running)
        {
            fetchButtons.Children.Add(ButtonFor("Fetch missing artwork", () => FetchArtwork(false)));
            fetchButtons.Children.Add(ButtonFor("Retry artists checked recently", () => FetchArtwork(true)));
        }
        var stateText = Text(fetchState, "CaptionTextBlockStyle");
        var summary = Stack(Row(Stack(Text(T("Fetch after library scans")), Text(T("Fills in missing artist pictures and banners."), "CaptionTextBlockStyle")), afterScan),
            Row(stateText, fetchButtons));
        if (art.Running)
        {
            artworkText = stateText;
            artworkBar = new ProgressBar { MinHeight = 5 };
            summary.Children.Add(artworkBar);
            ShowArtworkProgress(art);
        }
        page.Children.Add(Card(summary));

        page.Children.Add(Text(T("Melon asks enabled providers from the top down and keeps the first image each one finds."), "CaptionTextBlockStyle"));
        var ids = art.Providers.Select(p => p.Id).ToList();
        foreach (var provider in art.Providers)
            page.Children.Add(Card(ProviderRow(provider, ids)));
    }

    private Grid ProviderRow(ArtworkProviderInfo p, List<string> ids)
    {
        string state = !p.Installed ? T("This provider’s plugin is not loaded.")
            : p.RequiresApiKey && !p.HasApiKey ? T("Needs API key")
            : p.RequiresApiKey ? T("API key saved") : T("No API key needed");
        var info = Stack(Text($"{p.Priority + 1}.  {p.Name}", "BodyStrongTextBlockStyle"), Text(p.Description, "CaptionTextBlockStyle"),
            Text(state, "CaptionTextBlockStyle"));
        info.Spacing = 2;

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        var up = IconAction("", T("Move up") + " " + p.Name, () => Move(ids, p.Id, -1));
        var down = IconAction("", T("Move down") + " " + p.Name, () => Move(ids, p.Id, 1));
        up.IsEnabled = p.Priority > 0;
        down.IsEnabled = p.Priority < ids.Count - 1;
        actions.Children.Add(up); actions.Children.Add(down);

        if (p.RequiresApiKey && p.Installed)
            actions.Children.Add(ButtonFor(p.HasApiKey ? "API key" : "Set up", () => SetupProvider(p)));
        if (p.Installed && (!p.RequiresApiKey || p.HasApiKey))
            actions.Children.Add(ButtonFor("Test", () => TestProvider(p)));

        var toggle = new ToggleSwitch { IsOn = p.Enabled, OnContent = T("Enabled"), OffContent = T("Disabled"), IsEnabled = p.Installed, MinWidth = 0 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle, p.Name);
        toggle.Toggled += async (_, _) => await Act(async () =>
        {
            if (toggle.IsOn && p.RequiresApiKey && !p.HasApiKey) await SetupProvider(p); // Saving a key enables it.
            else await Command(nameof(IMelonAdministration.SetArtworkProviderEnabled), new { id = p.Id, enabled = toggle.IsOn });
        });
        actions.Children.Add(toggle);
        return Row(info, actions);
    }

    private Task Move(List<string> ids, string id, int step)
    {
        var order = ids.ToList();
        int from = order.IndexOf(id), to = Math.Clamp(from + step, 0, order.Count - 1);
        (order[from], order[to]) = (order[to], order[from]);
        return Command(nameof(IMelonAdministration.SetArtworkProviderOrder), new { ids = order });
    }

    private async Task FetchArtwork(bool force)
    {
        if (await Read<bool>(nameof(IMelonAdministration.FetchArtistArtwork), new { force })) Notice("Fetching artwork…", InfoBarSeverity.Informational);
        else Notice("Artwork can’t be fetched while a scan or another fetch is running.", InfoBarSeverity.Warning);
    }

    /// <summary>Asks for a key, saves it (which enables the provider), then proves it with a real lookup.</summary>
    private async Task SetupProvider(ArtworkProviderInfo p)
    {
        var key = new PasswordBox { Header = T("API key"), PlaceholderText = p.HasApiKey ? T("Saved. Enter a new key to replace it.") : "" };
        var content = Stack(Text(p.Description));
        if (p.ApiKeyUrl != null)
            content.Children.Add(new HyperlinkButton { Content = T("Get a free API key"), NavigateUri = new Uri(p.ApiKeyUrl), Padding = new Thickness(0) });
        content.Children.Add(key);
        bool remove = false;
        if (p.HasApiKey)
        {
            var removeBox = new CheckBox { Content = T("Remove the saved key and disable this provider") };
            removeBox.Checked += (_, _) => { remove = true; key.IsEnabled = false; };
            removeBox.Unchecked += (_, _) => { remove = false; key.IsEnabled = true; };
            content.Children.Add(removeBox);
        }
        if (!await Dialog($"{p.Name} · {T("API key")}", content, "Save")) return;
        if (remove)
        {
            await Command(nameof(IMelonAdministration.SetArtworkProviderKey), new { id = p.Id, apiKey = "" });
            return;
        }
        var apiKey = key.Password.Trim();
        if (apiKey.Length == 0) throw new ArgumentException(T("Enter an API key."));
        await Command(nameof(IMelonAdministration.SetArtworkProviderKey), new { id = p.Id, apiKey });
        try { Notice(await Read<string>(nameof(IMelonAdministration.TestArtworkProvider), new { id = p.Id, artistName = TestArtist })); }
        catch (InvalidOperationException ex) { Notice(ex.Message, InfoBarSeverity.Warning); }
    }

    private async Task TestProvider(ArtworkProviderInfo p)
    {
        var artist = new TextBox { Header = T("Artist name"), Text = TestArtist };
        if (!await Dialog($"{T("Test")} · {p.Name}", artist, "Test")) return;
        Notice(await Read<string>(nameof(IMelonAdministration.TestArtworkProvider), new { id = p.Id, artistName = artist.Text }),
            InfoBarSeverity.Informational);
    }
}
