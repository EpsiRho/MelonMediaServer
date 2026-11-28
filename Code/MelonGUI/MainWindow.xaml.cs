using Melon.Controllers;
using Melon.Interface;
using MelonLib.API;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Text.Json;
using WinUIEx;

namespace MelonGUI;

public sealed partial class MainWindow : WindowEx
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly LocalControllerClient client;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private ServerStatus? status;
    private ControllerSettings? settings;
    private string tab = "Library", language = "EN";
    private bool busy, polling, rebuilding, deferSetup, stopRequested;
    private bool refreshLibrary, refreshPlugins, refreshUsers, serviceRunning;
    private int generation;
    private string? selectedPlugin, selectedAuthor;
    private string pluginOrder = "Name";
    private ReleaseInfo? release;
    private readonly string? dataPath = Environment.GetEnvironmentVariable("MELON_CONTROLLER_DATA_PATH");

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Melon.ico"));
        client = new LocalControllerClient(dataPath);
        Root.RequestedTheme = DesktopHost.ReadTheme();
        BuildNavigation();
        Closed += (_, _) => { timer.Stop(); lifetime.Cancel(); };
        timer.Tick += async (_, _) => await Poll();
        Root.Loaded += async (_, _) =>
        {
            Heading.Text = "Melon";
            Body.Content = new ProgressRing { IsActive = true, HorizontalAlignment = HorizontalAlignment.Left };
            _ = FollowServerChanges();
            await Poll(startIfStopped: true);
            timer.Start();
        };
    }

    private void BuildNavigation()
    {
        Root.Language = language.Equals("ES", StringComparison.OrdinalIgnoreCase) ? "es" : "en";
        rebuilding = true;
        Sidebar.MenuItems.Clear();
        foreach (var (key, icon) in new[] { ("Library", "\uE8F1"), ("Database", "\uEDA2"), ("Networking", "\uE968"),
                                            ("Users", "\uE716"), ("Stats", "\uE9D9"), ("Plugins", "\uEA86"), ("System", "\uE770") })
        {
            var item = new NavigationViewItem { Content = T(key), Tag = key, Icon = new FontIcon { Glyph = icon } };
            Sidebar.MenuItems.Add(item);
            if (key == tab) Sidebar.SelectedItem = item;
        }
        rebuilding = false;
    }

    private async void Navigate(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (rebuilding || args.SelectedItem is not NavigationViewItem { Tag: string key }) return;
        tab = key;
        selectedPlugin = null;
        Feedback.IsOpen = false;
        await Render();
    }

    private Task<JsonElement> Command(string method, object? args = null) => client.CallAsync(method, args, lifetime.Token);
    private async Task<TValue> Read<TValue>(string method, object? args = null) =>
        (await Command(method, args)).Deserialize<TValue>(Json) ?? throw new InvalidDataException(method);

    private async Task FollowServerChanges()
    {
        while (!lifetime.IsCancellationRequested)
        {
            try
            {
                await foreach (var message in client.SubscribeAsync(lifetime.Token))
                {
                    refreshLibrary |= message.Topic is "scan" or "connected" or "settings" or "artwork";
                    refreshUsers |= message.Topic is "users" or "connected";
                    refreshPlugins |= message.Topic is "plugins" or "connected";
                }
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException) { }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return; }
            try { await Task.Delay(TimeSpan.FromSeconds(2), lifetime.Token); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task Poll(bool startIfStopped = false)
    {
        if (polling || busy || lifetime.IsCancellationRequested) return;
        polling = true;
        try
        {
            var previous = status;
            var previousSettings = settings;
            var oldLanguage = language;
            try { status = await Read<ServerStatus>(nameof(IMelonAdministration.GetStatus)); }
            catch (TimeoutException) when (startIfStopped && !stopRequested)
            {
                await StartServer();
                status = await Read<ServerStatus>(nameof(IMelonAdministration.GetStatus));
            }
            settings = await Read<ControllerSettings>(nameof(IMelonAdministration.GetSettings));
            language = settings.Appearance.Language;
            if (oldLanguage != language) BuildNavigation();
            if (previous?.RunId != status.RunId) release = null;
            UpdateWarning();
            bool libraryChanged = !Setup && !Recovery && tab == "Library" && refreshLibrary;
            bool pluginsChanged = !Setup && !Recovery && tab == "Plugins" && refreshPlugins;
            if (libraryChanged || pluginsChanged || tab == "Users" && refreshUsers || previous == null || previous.RunId != status.RunId || oldLanguage != language ||
                previous.SetupRequired != status.SetupRequired || previous.DatabaseConnected != status.DatabaseConnected ||
                !previous.PendingConversions.SequenceEqual(status.PendingConversions) ||
                previous.Scanning != status.Scanning || previousSettings?.RestartRequired != settings.RestartRequired)
                await Render();
            else if (tab == "Users" && !Setup && !Recovery) await RefreshInvites();
            else { UpdateProgress(); await UpdateArtworkProgress(); }
        }
        catch (Exception ex) when (!lifetime.IsCancellationRequested)
        {
            bool running = dataPath == null && DesktopHost.IsServiceRunning();
            bool changed = serviceRunning != running || startIfStopped || status != null || Body.Content == null;
            status = null;
            serviceRunning = running;
            UpdateWarning();
            if (changed) await Render();
            if (startIfStopped && ex is not TimeoutException) Error(ex);
        }
        catch (OperationCanceledException) { }
        finally { polling = false; }
    }

    private bool Recovery => status?.DatabaseConnected == true && status.PendingConversions.Length > 0;
    private bool Setup => status != null && !deferSetup && (status.SetupRequired || manualSetup);
    private async Task Render()
    {
        int view = ++generation;
        if (tab == "Users") refreshUsers = false;
        if (tab == "Library") refreshLibrary = false;
        if (tab == "Plugins") refreshPlugins = false;
        var page = new StackPanel { Spacing = 10 };
        try
        {
            bool intro = networkIntro && !Recovery && !Setup && status != null;
            Sidebar.IsPaneVisible = !Recovery && !Setup && !intro;
            Heading.Text = T(Recovery ? "Database recovery" : Setup ? "Set up Melon" : intro ? NetworkText.IntroTitle : tab);
            progressCard = null;
            artworkBar = null; artworkText = null;
            if (Recovery) RecoveryPage(page);
            else if (Setup) SetupPage(page);
            else if (intro) await NetworkIntroPage(page);
            else if (tab == "System") await SystemPage(page);
            else if (status == null || settings == null) page.Children.Add(serviceRunning ? ButtonFor("System", () => { tab = "System"; BuildNavigation(); return Task.CompletedTask; }) : ButtonFor("Start Melon", StartHost));
            else switch (tab)
            {
                case "Library": await LibraryPage(page); break;
                case "Database": await DatabasePage(page); break;
                case "Networking": await NetworkingPage(page); break;
                case "Users": await UsersPage(page); break;
                case "Stats": await StatsPage(page); break;
                case "Plugins": await PluginsPage(page); break;
            }
        }
        catch (Exception ex) when (!lifetime.IsCancellationRequested) { if (view == generation) Error(ex); }
        catch (OperationCanceledException) { }
        if (view != generation || lifetime.IsCancellationRequested) return;
        Body.Content = page;
        Scroller.ChangeView(null, 0, null, true);
        UpdateProgress();
    }

    private async Task Act(Func<Task> action, bool refresh = true)
    {
        if (busy) return;
        busy = true;

        Body.IsEnabled = false;
        try
        {
            await action();
            if (refresh && !lifetime.IsCancellationRequested)
            {
                try { status = await Read<ServerStatus>(nameof(IMelonAdministration.GetStatus)); settings = await Read<ControllerSettings>(nameof(IMelonAdministration.GetSettings)); }
                catch (TimeoutException) { status = null; }
                if (settings != null && language != settings.Appearance.Language) { language = settings.Appearance.Language; BuildNavigation(); }
                UpdateWarning();
                await Render();
            }
        }
        catch (Exception ex) when (!lifetime.IsCancellationRequested) { Error(ex); }
        catch (OperationCanceledException) { }
        finally { busy = false; Body.IsEnabled = true; }
    }

    private void UpdateWarning()
    {
        bool offline = status == null, database = status?.DatabaseConnected == false;
        bool restart = settings?.RestartRequired == true && !offline;
        ConnectionBar.Title = T(offline ? serviceRunning ? "Melon service running. Controller disconnected." : "Melon is stopped or unavailable" : database ? "MongoDB is disconnected" : "Restart to apply settings");
        ConnectionBar.ActionButton = restart ? ButtonFor("Restart Melon", RestartHost) :
            database && tab != "Database" ? ButtonFor("Database", ShowDatabase) : null;
        ConnectionBar.IsOpen = offline || database || restart;
    }

    private Task ShowDatabase()
    {
        deferSetup = true; manualSetup = false; tab = "Database"; BuildNavigation();
        return Task.CompletedTask;
    }

    private async Task StartHost()
    {
        stopRequested = false;
        await StartServer();
        await WaitForHost(null);
    }

    /// <summary>Starts Melon. If its server isn't next to MelonGUI or in the default folder, asks where Melon is installed.</summary>
    private async Task StartServer()
    {
        try { await DesktopHost.StartAsync(dataPath, lifetime.Token); }
        catch (Melon.Controllers.ServerNotFoundException)
        {
            var message = new TextBlock { TextWrapping = TextWrapping.Wrap,
                Text = T("Melon's server wasn't found next to MelonGUI. Choose the folder Melon is installed in, the one with the Server folder inside it. You only need to do this once.") };
            if (!await Dialog("Where is Melon installed?", message, "Choose folder")) throw;
            var exe = Melon.Controllers.ServerLocator.Resolve(await PickFolder());
            if (exe == null) throw new IOException(T("Melon's server wasn't found in that folder."));
            Melon.Controllers.ServerLocator.Save(exe);
            await DesktopHost.StartAsync(dataPath, lifetime.Token);
        }
    }

    private async Task RestartHost()
    {
        var before = status!.RunId;
        await Command(nameof(IMelonAdministration.Restart));
        await WaitForHost(before);
    }

    private async Task WaitForHost(Guid? previous)
    {
        var end = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < end)
        {
            try
            {
                var state = await Read<ServerStatus>(nameof(IMelonAdministration.GetStatus));
                if (state.RunId != previous) { status = state; return; }
            }
            catch (Exception ex) when (ex is IOException or TimeoutException) { }
            await Task.Delay(300, lifetime.Token);
        }
        throw new TimeoutException(T("Melon did not start in time."));
    }

    private void Error(Exception ex) => Notice(ex.Message, InfoBarSeverity.Error);
    private void Notice(string text, InfoBarSeverity severity = InfoBarSeverity.Success)
    {
        Feedback.Severity = severity; Feedback.Title = T(text); Feedback.IsOpen = true;
    }

    private string T(string key) => Melon.Controllers.UiStrings.Get(language, key);
}
