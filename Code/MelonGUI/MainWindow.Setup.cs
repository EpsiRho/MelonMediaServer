using Melon.Interface;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MelonGUI;

public sealed partial class MainWindow
{
    private bool manualSetup;

    private void SetupPage(StackPanel page)
    {
        var user = new TextBox { Header = T("Admin username") };
        var password = new PasswordBox { Header = T("Admin password") };
        page.Children.Add(Card(Stack(user, password)));

        var folders = new List<string>(settings?.LibraryPaths ?? []);
        var list = new ListView { MaxHeight = 110, SelectionMode = ListViewSelectionMode.Single };
        void UpdateFolders() => list.ItemsSource = folders.ToArray();
        UpdateFolders();
        var add = new Button { Content = T("Add folder") };
        add.Click += async (_, _) =>
        {
            try { if (await PickFolder() is string path && !folders.Contains(path, StringComparer.OrdinalIgnoreCase)) folders.Add(path); UpdateFolders(); }
            catch (Exception ex) { Error(ex); }
        };
        var remove = new Button { Content = T("Remove"), IsEnabled = false };
        list.SelectionChanged += (_, _) => remove.IsEnabled = list.SelectedItem != null;
        remove.Click += (_, _) => { if (list.SelectedItem is string path) folders.Remove(path); UpdateFolders(); };
        Section(page, "Library folders"); page.Children.Add(Card(Stack(list, Row(add, remove))));

        Section(page, "MongoDB");
        var choice = Choices("", "I have MongoDB running locally", "I have MongoDB running on another system", "I need to install MongoDB");
        var currentHosts = settings?.MongoServers ?? [];
        bool remote = currentHosts.Any(host => Uri.TryCreate("http://" + host, UriKind.Absolute, out var endpoint) && !endpoint.IsLoopback);
        choice.SelectedIndex = remote ? 1 : 0;
        bool UseCurrent() => status?.DatabaseConnected == true && choice.SelectedIndex == (remote ? 1 : 0);
        var uri = new PasswordBox { Header = T("MongoDB connection URL"), PasswordRevealMode = PasswordRevealMode.Peek,
            PlaceholderText = status?.DatabaseConnected == true ? string.Join(", ", settings!.MongoServers) : "mongodb://localhost:27017" };
        choice.SelectionChanged += (_, _) =>
        {
            uri.Visibility = choice.SelectedIndex == 2 ? Visibility.Collapsed : Visibility.Visible;
            uri.Password = "";
            uri.PlaceholderText = UseCurrent() ? string.Join(", ", currentHosts) : choice.SelectedIndex == 1 ? "mongodb://server:27017" : "mongodb://localhost:27017";
        };
        page.Children.Add(Card(Stack(choice, uri)));
        page.Children.Add(Row(ButtonFor("Database settings", ShowDatabase), ButtonFor("Finish setup", async () =>
        {
            if (string.IsNullOrWhiteSpace(user.Text) || password.Password.Length == 0)
                throw new ArgumentException(T("Enter an admin username and password."));
            if (choice.SelectedIndex == 2)
            {
                Notice("Installing MongoDB…", InfoBarSeverity.Informational);
                await DesktopHost.InstallMongo(lifetime.Token);
                await Command(nameof(IMelonAdministration.ConnectManagedMongo));
                await RestartHost();
            }
            else if (uri.Password.Length != 0 || !UseCurrent())
            {
                var address = uri.Password.Trim();
                if (address.Length == 0 && choice.SelectedIndex == 1) throw new ArgumentException(T("Enter the remote MongoDB URL."));
                await Command(nameof(IMelonAdministration.SetMongoConnection), new { connectionString = address.Length == 0 ? "mongodb://localhost:27017" : address });
                await RestartHost();
            }
            var current = await Read<ServerStatus>(nameof(IMelonAdministration.GetStatus));
            if (!current.DatabaseConnected) throw new InvalidOperationException(T("MongoDB is disconnected"));
            if (current.PendingConversions.Length > 0) { manualSetup = false; return; }
            if (current.SetupRequired)
            {
                await Command(nameof(IMelonAdministration.SetLibraryPaths), new { paths = folders.ToArray() });
                await Command(nameof(IMelonAdministration.CreateFirstAdmin), new { username = user.Text.Trim(), password = password.Password });
            }
            password.Password = uri.Password = "";
            manualSetup = deferSetup = false; tab = "Library"; BuildNavigation();
            Notice("Setup complete.");
        }, true)));
    }

    private void RecoveryPage(StackPanel page)
    {
        page.Children.Add(Card(Stack(Text(T("Collections to convert"), "BodyStrongTextBlockStyle"),
            Text(string.Join(", ", status!.PendingConversions)), Text(T("Melon creates a backup before converting.")),
            ButtonFor("Back up and convert", async () =>
            {
                await Command(nameof(IMelonAdministration.ConvertDatabase)); Notice("Conversion complete.");
            }, true))));
    }
}
