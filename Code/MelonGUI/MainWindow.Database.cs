using Melon.Interface;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MelonGUI;

public sealed partial class MainWindow
{
    private async Task DatabasePage(StackPanel page)
    {
        var config = settings!;
        var connected = status!.DatabaseConnected;
        var hosts = string.Join(", ", config.MongoServers);
        page.Children.Add(Card(Row(Stack(Text(T(connected ? "Connected" : "Disconnected"), "BodyStrongTextBlockStyle"), Text(hosts)),
            ButtonFor("Change connection", async () =>
            {
                var uri = new PasswordBox { Header = T("MongoDB connection URL"), PlaceholderText = "mongodb://localhost:27017",
                    PasswordRevealMode = PasswordRevealMode.Peek };
                if (await Dialog("Change connection", uri, "Connect"))
                {
                    await Command(nameof(IMelonAdministration.SetMongoConnection), new { connectionString = uri.Password.Trim() });
                    await RestartHost();
                }
            }))));
        if (!connected || status.SetupRequired)
            page.Children.Add(ButtonFor("Set up MongoDB", () => { deferSetup = false; manualSetup = true; return Task.CompletedTask; }));

        Section(page, "Backups");
        var backups = Stack();
        var create = ButtonFor("Create backup", async () =>
        {
            if (!await Read<bool>(nameof(IMelonAdministration.BackupDatabase))) throw new IOException(T("Backup failed."));
            Notice("Backup created.");
        });
        create.IsEnabled = connected && !status.Scanning;
        backups.Children.Add(create);
        foreach (var name in await Read<string[]>(nameof(IMelonAdministration.ListBackups)))
        {
            var folder = Path.Combine(status.DataDirectory, "Exports", "DbBackups", name);
            var restore = ButtonFor("Restore", async () =>
            {
                if (await Dialog("Restore backup?", Text(name), "Restore", true))
                    await Command(nameof(IMelonAdministration.RestoreBackup), new { name });
            });
            restore.IsEnabled = connected && !status.Scanning;
            backups.Children.Add(Row(Text(name), Row(restore, IconAction("\uE8DA", T("Open backup folder"), () => DesktopHost.OpenFolder(folder)))));
        }
        page.Children.Add(Card(backups));

        Section(page, "Playlists and collections");
        var transfers = Row(ButtonFor("Import", ImportList), ButtonFor("Export", ExportList));
        foreach (Control control in transfers.Children) control.IsEnabled = connected;
        page.Children.Add(Card(transfers));

        Section(page, "Maintenance");
        var hours = new NumberBox { Value = config.QueueCleanupHours, Minimum = -1, Maximum = 1000000,
            Header = T("Queue cleanup frequency (hours)"), SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        page.Children.Add(Card(Row(Stack(hours, Text(T("Use −1 to disable cleanup."), "CaptionTextBlockStyle")),
            ButtonFor("Save", async () =>
            {
                if (!double.IsFinite(hours.Value) || hours.Value < 0 && hours.Value != -1) throw new ArgumentException(T("Enter valid hours."));
                await Command(nameof(IMelonAdministration.SetQueueCleanupHours), new { hours = hours.Value });
            }))));
        var reset = ButtonFor("Reset database", async () =>
        {
            if (await Dialog("Reset the database?", Text(T("This deletes library data, playlists, collections, play history, and artwork.")), "Reset database", true))
            {
                await Command(nameof(IMelonAdministration.ResetDatabase));
                Notice("Database reset.");
            }
        });
        reset.IsEnabled = connected && !status.Scanning;
        page.Children.Add(Card(Row(Text(T("Database reset")), reset)));
    }

    private async Task ImportList()
    {
        var owners = await Read<PlaylistOwner[]>(nameof(IMelonAdministration.GetPlaylistOwners));
        if (owners.Length == 0) throw new InvalidOperationException(T("Create a user first."));
        var owner = new ComboBox { Header = T("Owner"), ItemsSource = owners, DisplayMemberPath = "Username",
            SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var type = Choices("Import as", "Playlist", "Collection");
        if (!await Dialog("Import playlists and collections", Stack(type, owner), "Choose file")) return;
        var file = await PickFile(".m3u", ".m3u8", ".pls", ".xml", ".xspf");
        if (file == null) return;
        var method = Choice(type) == "Collection" ? nameof(IMelonAdministration.ImportCollections) : nameof(IMelonAdministration.ImportPlaylists);
        if (await Read<int>(method, new { path = file, userId = ((PlaylistOwner)owner.SelectedItem).Id }) == 0)
            throw new InvalidDataException(T("No entries could be imported."));
        Notice("Import complete.");
    }

    private async Task ExportList()
    {
        var search = new TextBox { PlaceholderText = T("Search by name") };
        var matches = new ListView { Height = 180, SelectionMode = ListViewSelectionMode.Single };
        var formats = Choices("Format", "M3U", "PLS", "XML");
        int request = 0;
        bool open = true;
        async Task Find()
        {
            int current = ++request;
            var query = search.Text;
            await Task.Delay(180, lifetime.Token);
            if (!open || current != request) return;
            var items = await Read<TransferItem[]>(nameof(IMelonAdministration.SearchTransferItems), new { query });
            if (!open || current != request) return;
            matches.Items.Clear();
            foreach (var item in items) matches.Items.Add(new ListViewItem { Content = item.Name + " · " + T(item.Kind), Tag = item });
            if (matches.Items.Count > 0) matches.SelectedIndex = 0;
        }
        search.TextChanged += async (_, _) => { try { await Find(); } catch (Exception ex) { if (open) Error(ex); } };
        await Find();
        bool export;
        try { export = await Dialog("Export playlist or collection", Stack(search, matches, formats), "Export"); }
        finally { open = false; }
        if (!export) return;
        if (matches.SelectedItem is not ListViewItem { Tag: TransferItem itemToExport }) throw new ArgumentException(T("Select a playlist or collection."));
        var format = "." + Choice(formats).ToLowerInvariant();
        var picker = new FileSavePicker { SuggestedFileName = string.Concat(itemToExport.Name.Split(Path.GetInvalidFileNameChars())) };
        picker.FileTypeChoices.Add(Choice(formats), new List<string> { format });
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        if (await picker.PickSaveFileAsync() is not {} destination) return;
        var source = await Read<string>(nameof(IMelonAdministration.ExportPlaylist), new { id = itemToExport.Id, format });
        if (!string.Equals(source, destination.Path, StringComparison.OrdinalIgnoreCase)) File.Copy(source, destination.Path, true);
        Notice("Export complete.");
    }
}
