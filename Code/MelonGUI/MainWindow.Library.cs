using Melon.Interface;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MelonGUI;

public sealed partial class MainWindow
{
    private Border? progressCard;
    private TextBlock? scanStage;
    private ProgressBar? scanProgress;

    private async Task LibraryPage(StackPanel page)
    {
        // Scan Section
        var config = settings!;
        var scan = new SplitButton { Content = T("Start Library Scan"), IsEnabled = status!.DatabaseConnected && !status.Scanning };
        var menu = new MenuFlyout();

        var scanMenuItem = new MenuFlyoutItem { Text = T("Start Library Scan") };
        scanMenuItem.Click += async (_, _) => await Act(() => Scan(true));
        var scanToolTip = new ToolTip
        {
            Content = "Scan files that haven't already been scanned"
        };
        ToolTipService.SetToolTip(scanMenuItem, scanToolTip);
        menu.Items.Add(scanMenuItem);

        var scanFullMenuItem = new MenuFlyoutItem { Text = T("Start Full Library Refresh") };
        scanFullMenuItem.Click += async (_, _) => await Act(() => Scan(false));
        var fullToolTip = new ToolTip
        {
            Content = "Scan all files"
        };
        ToolTipService.SetToolTip(scanFullMenuItem, fullToolTip);
        menu.Items.Add(scanFullMenuItem);

        scan.Flyout = menu;
        scan.Click += async (_, _) => await Act(() => Scan(true));
        page.Children.Add(Card(Row(Text(T("Library scan"), "BodyStrongTextBlockStyle"), scan)));
        scanStage = Text("");
        scanProgress = new ProgressBar { Maximum = 100, MinHeight = 5 };
        progressCard = Card(Stack(scanStage, scanProgress));
        page.Children.Add(progressCard);
        UpdateProgress();

        // Library Info
        if (status.DatabaseConnected)
        {
            var totals = await Read<LibraryTotals>(nameof(IMelonAdministration.GetLibraryTotals));
            var grid = new Grid { ColumnSpacing = 8 };
            foreach (var (label, value) in new[] { ("Songs", totals.Songs), ("Albums", totals.Albums), ("Artists", totals.Artists) })
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                var tile = Card(Stack(Text(value.ToString("N0"), "SubtitleTextBlockStyle"), Text(T(label))));
                Grid.SetColumn(tile, grid.Children.Count); grid.Children.Add(tile);
            }
            page.Children.Add(grid);
        }

        // Library folders
        Section(page, "Library folders");
        var folders = Stack();
        foreach (var folder in config.LibraryPaths)
            folders.Children.Add(Row(Text(folder), IconAction("\uE74D", T("Remove") + " " + folder, async () =>
            {
                var latest = await Read<ControllerSettings>(nameof(IMelonAdministration.GetSettings));
                await Command(nameof(IMelonAdministration.SetLibraryPaths), new { paths = latest.LibraryPaths.Where(p => p != folder) });
            })));
        folders.Children.Add(ButtonFor("Add folder", async () =>
        {
            if (await PickFolder() is not string folder) return;
            var latest = await Read<ControllerSettings>(nameof(IMelonAdministration.GetSettings));
            await Command(nameof(IMelonAdministration.SetLibraryPaths), new { paths = latest.LibraryPaths.Append(folder).Distinct(StringComparer.OrdinalIgnoreCase) });
        }));
        page.Children.Add(Card(folders));

        // Metadta settings
        Section(page, "Metadata");
        foreach (var (label, values, method) in new[] {
            ("Artist split indicators", config.ArtistSplitIndicators, nameof(IMelonAdministration.SetArtistSplitIndicators)),
            ("Genre split indicators", config.GenreSplitIndicators, nameof(IMelonAdministration.SetGenreSplitIndicators)) })
            page.Children.Add(Card(Row(Stack(Text(T(label)), Text(string.Join("    ", values), "CaptionTextBlockStyle")),
                ButtonFor("Edit", async () => { if (await EditList(label, values) is string[] indicators) await Command(method, new { indicators }); }))));
    }

    private async Task Scan(bool skip)
    {
        if (!await Read<bool>(nameof(IMelonAdministration.StartScan), new { skip })) Notice("A scan is already running.");
    }

    private void UpdateProgress()
    {
        if (progressCard == null || scanStage == null || scanProgress == null) return;
        progressCard.Visibility = status?.Scanning == true ? Visibility.Visible : Visibility.Collapsed;
        if (status?.Scanning != true) return;
        var scan = status.Scan;
        scanProgress.IsIndeterminate = scan.FoundFiles <= 0;
        scanProgress.Value = scan.FoundFiles > 0 ? Math.Clamp(100 * scan.ScannedFiles / scan.FoundFiles, 0, 100) : 0;
        scanStage.Text = T(scan.Status) + (scan.FoundFiles > 0 ? $"   {scan.ScannedFiles:N0} / {scan.FoundFiles:N0}" : "");
    }
}
