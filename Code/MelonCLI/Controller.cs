using System.Text.Json;
using Melon.Controllers;
using Melon.Interface;
using MelonLib.API;
using MelonUI.Managers;
using Admin = Melon.Interface.IMelonAdministration;

namespace MelonCLI;

internal sealed partial class Controller
{
    private readonly LocalControllerClient client;
    private readonly string? dataPath;
    private readonly CancellationToken token;
    private readonly Terminal ui;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] Sections = ["Library", "Database", "Networking", "Users", "Stats", "Plugins", "System"];
    private string section = "Library", language = "EN", notice = "";
    private Tone noticeTone = Tone.Good;
    private ServerStatus? status;
    private ControllerSettings settings = null!;
    private bool deferSetup, runSetup, exit;
    private readonly string themeFile;
    private ThemeSettings theme;

    public Controller(LocalControllerClient client, string? dataPath, ConsoleWindowManager window, CancellationToken token)
    {
        this.client = client; this.dataPath = dataPath; this.token = token;
        ui = new Terminal(window, token, T);
        themeFile = Path.Combine(dataPath ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MelonCLI", "theme");
        theme = ThemeSettings.Load(themeFile);
        ui.P = theme.Resolve(); ui.BindKeys();
    }

    private string T(string key) => CliStrings.Get(language, key);
    private Task<JsonElement> Call(string method, object? args = null) => client.CallAsync(method, args, token);
    private async Task<T> Read<T>(string method, object? args = null) => (await Call(method, args)).Deserialize<T>(Json)!;
    private async Task Write(string method, object? args = null) { await Call(method, args); }
    private Entry Action(string label, Func<Task> act, Tone tone = Tone.Normal) => new(T(label), act) { Tone = tone };
    private static Entry Local(string label, Action act) => new(label, () => { act(); return Task.CompletedTask; });
    private Entry Setting(string label, string? value, Func<Task> act) => new(T(label), act) { Value = value };
    private void Say(string text, Tone tone = Tone.Good) { notice = text; noticeTone = tone; }
    private async Task<string> Ask(string key, string initial = "", bool secret = false, string help = "") => await ui.Input(T(key), initial, secret, help: help);

    private async Task<string> Choose(string key, IEnumerable<string> labels, bool translate = true, string? current = null)
    {
        string? selected = null;
        var screen = new Screen(T(key));
        foreach (string value in labels)
            screen.Items.Add(Local(translate ? T(value) : value, () => selected = value) with { Glyph = value == current ? "●" : null });
        if (screen.Items.Count == 0) throw new InvalidOperationException(T("No entries."));
        var item = await ui.Menu(screen) ?? throw new Back();
        await item.Run!(); return selected!;
    }

    private async Task Confirm(string key, string detail, bool danger = false)
    {
        var screen = new Screen(T(key));
        screen.PanelBlocks.Add(new Callout(detail, danger ? Tone.Danger : Tone.Warn, danger ? T("This cannot be undone.") : null));
        screen.Items.Add(Local(T("Confirm"), () => { }) with { Tone = danger ? Tone.Danger : Tone.Normal });
        await (await ui.Menu(screen) ?? throw new Back()).Run!();
    }

    /// <summary>Read-only content, such as release notes or plugin output, shown in the main area.</summary>
    private async Task Show(string title, string text) => await ShowBlocks(title, new Para(text));

    private async Task ShowBlocks(string title, params Block[] blocks)
    {
        var screen = new Screen(T(title));
        screen.Body.AddRange(blocks);
        await ui.Menu(screen);
    }

    public async Task Run()
    {
        ui.StatusText = T("Connecting…"); ui.StatusTone = Tone.Muted;
        await ui.Busy(T("Connecting…"));
        try { await Read<ServerStatus>(nameof(Admin.GetStatus)); }
        catch (TimeoutException)
        {
            await ui.Busy(T("Starting Melon…"));
            try { await Start(); } catch (Exception ex) when (!token.IsCancellationRequested) { Say(ex.Message, Tone.Danger); }
        }
        while (!exit && !token.IsCancellationRequested)
        {
            try
            {
                var page = await Build();
                var selected = await ui.Menu(page, Build, isMain: true);
                if (selected == null) return;
                notice = "";
                // Only show the wait screen when an action is slow and has not opened a prompt of its own.
                int generation = ui.Generation;
                var run = selected.Run!();
                if (await Task.WhenAny(run, Task.Delay(150, token)) != run) await ui.Busy(selected.Label, generation);
                await run;
                while (ui.TakeQueued() is { } next) await next.Run!();
            }
            catch (Back) { }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                var error = new Screen(T("Error"));
                error.PanelBlocks.Add(new Callout(T(ex.Message), Tone.Danger, T("Something went wrong")));
                await ui.Menu(error);
            }
        }
    }

    private void UpdateHeader()
    {
        if (status == null) { ui.StatusText = T("Offline"); ui.StatusTone = Tone.Danger; ui.StatusDetail = ""; return; }
        ui.StatusDetail = $"v{status.Version} · {status.Branch}";
        if (!status.DatabaseConnected) { ui.StatusText = T("Database offline"); ui.StatusTone = Tone.Warn; }
        else if (status.Scanning)
        {
            var pct = status.Scan.FoundFiles > 0 ? status.Scan.ScannedFiles / status.Scan.FoundFiles : 0;
            ui.StatusText = $"{T("Scanning")} {pct:P0}"; ui.StatusTone = Tone.Info;
        }
        else if (settings.RestartRequired) { ui.StatusText = T("Restart required"); ui.StatusTone = Tone.Warn; }
        else { ui.StatusText = T("Online"); ui.StatusTone = Tone.Good; }
    }

    private async Task<Screen> Build()
    {
        var old = status?.RunId;
        try
        {
            status = await Read<ServerStatus>(nameof(Admin.GetStatus));
            settings = await Read<ControllerSettings>(nameof(Admin.GetSettings));
            language = settings.Appearance.Language;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException) { status = null; }
        if (old != status?.RunId) { release = null; stats = null; }
        UpdateHeader();
        bool recovery = status?.DatabaseConnected == true && status.PendingConversions.Length > 0;
        bool setup = status != null && !deferSetup && (runSetup || status.SetupRequired);
        var screen = new Screen(T(recovery ? "Database recovery" : setup ? "Set up Melon" : section)) { ActiveSection = T(section) };
        if (!recovery && !setup)
            foreach (var name in Sections) screen.Sections.Add(Local(T(name), () => section = name));
        if (status == null)
        {
            screen.Body.Add(new Callout(T("Start Melon to manage your library, users and settings from here."), Tone.Danger, T("Melon is stopped or unavailable")));
            screen.Items.Add(Action("Start Melon", Start));
            if (OperatingSystem.IsWindows() && dataPath == null && WindowsHost.IsServiceRunning()) screen.Items.Add(Action("Stop Melon", () => WindowsHost.StopService(token), Tone.Danger));
            if (section == "System") screen.Items.Add(Setting("Controller theme", ThemeName(), Theme));
        }
        else if (recovery)
        {
            screen.ActiveSection = screen.Title;
            screen.Body.Add(new Callout(T("Melon creates a backup before converting."), Tone.Warn, T("The database needs to be converted before Melon can use it.")));
            screen.Body.Add(new Panel(T("Pending conversions"), new Para(string.Join("\n", status.PendingConversions.Select(c => "• " + c)))));
            screen.Items.Add(Action("Back up and convert", async () => { await Confirm("Database recovery", T("Melon creates a backup before converting.")); await Write(nameof(Admin.ConvertDatabase)); }));
        }
        else if (setup)
        {
            screen.ActiveSection = screen.Title;
            screen.Body.Add(new Callout(T("Create the first admin account, choose your library folders, and connect MongoDB."), Tone.Accent, T("Let’s get Melon ready")));
            screen.Items.Add(Action("Set up Melon", Setup));
            screen.Items.Add(Local(T("Database settings"), ToDatabase));
        }
        else
        {
            switch (section)
            {
                case "Library": await Library(screen); break;
                case "Database": await DatabasePage(screen); break;
                case "Networking": Networking(screen); break;
                case "Users": await Users(screen); break;
                case "Stats": await Stats(screen); break;
                case "Plugins": await Plugins(screen); break;
                case "System": SystemPage(screen); break;
            }
        }
        if (status?.DatabaseConnected == false)
        {
            screen.Body.Insert(0, new Callout(T("Library, users and stats are unavailable until the database reconnects."), Tone.Warn, T("MongoDB is disconnected")));
            if (!setup && section != "Database") screen.Items.Add(Local(T("Database settings"), ToDatabase));
        }
        if (status != null && settings.RestartRequired)
        {
            screen.Body.Insert(0, new Callout(T("Some settings take effect after Melon restarts."), Tone.Warn, T("Restart required")));
            screen.Items.Insert(0, Action("Restart to apply settings", Restart, Tone.Warn));
        }
        screen.Notice = notice; screen.NoticeTone = noticeTone;
        return screen;
    }

    private void ToDatabase() { deferSetup = true; runSetup = false; section = "Database"; }
    private async Task Start() { await Platform.Start(dataPath, token); await WaitForHost(null); }
    private async Task Restart()
    {
        var before = status?.RunId; await Write(nameof(Admin.Restart)); await WaitForHost(before);
        Say(T("Melon restarted."));
    }
    private async Task WaitForHost(Guid? before)
    {
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            try { var s = await Read<ServerStatus>(nameof(Admin.GetStatus)); if (s.RunId != before) { status = s; return; } }
            catch (Exception ex) when (ex is IOException or TimeoutException) { }
            await Task.Delay(250, token);
        }
        throw new TimeoutException(T("Melon did not start in time."));
    }

    // ── Theme ────────────────────────────────────────────────────────────────

    private string ThemeName() => T(theme.Name);

    private void ApplyTheme() { ui.P = theme.Resolve(); theme.Save(themeFile); }

    private Block ThemePreview() => new Stack([
        new Panel(T("Palette"), new Swatches(Palette.Tokens.Select(t => (T(t.Name), t.Get)))),
        new Panel(T("Sample"), new Stack([
            new Cards([new Card(T("Plays"), "1,284", "▲ 12% " + T("vs previous"), Tone.Good, [3, 5, 4, 7, 6, 9, 8, 11, 9, 12]),
                       new Card(T("Skips"), "96", "▼ 4% " + T("vs previous"), Tone.Good, [4, 3, 5, 2, 3, 2, 4, 2, 1, 2], Tone.Flesh),
                       new Card(T("Status"), T("Connected"), Dot: Tone.Good)], 20),
            new HBars([new HBar("Midnight City", 142, "142"), new HBar("Teardrop", 97, "97"), new HBar("Nightcall", 55, "55")]),
            new Callout(T("Some settings take effect after Melon restarts."), Tone.Warn, T("Restart required"))]))]);

    private async Task Theme()
    {
        while (true)
        {
            var screen = new Screen(T("Controller theme"));
            screen.Body.Add(ThemePreview());
            screen.Items.Add(Entry.Header(T("Presets")));
            foreach (var name in Palette.Presets.Select(p => p.Name).Prepend(ThemeSettings.Auto))
                screen.Items.Add(Local(T(name), () => { theme.Name = name; ApplyTheme(); }) with { Glyph = theme.Name == name ? "●" : null });
            screen.Items.Add(Entry.Header(T("Custom")));
            screen.Items.Add(new Entry(T("Customize colors"), CustomizeTheme) { Glyph = theme.Name == ThemeSettings.Custom ? "●" : null });
            var row = await ui.Menu(screen);
            if (row == null) return;
            try { await row.Run!(); } catch (Back) { }
        }
    }

    private async Task CustomizeTheme()
    {
        if (theme.Name != ThemeSettings.Custom)
        {
            // Start from whatever is showing now.
            theme.Base = theme.Name == ThemeSettings.Auto ? (ui.P == Palette.MelonLight ? "Melon Light" : "Melon Dark") : theme.Name;
            theme.Store(ui.P);
            theme.Name = ThemeSettings.Custom; ApplyTheme();
        }
        while (true)
        {
            var palette = ui.P;
            var screen = new Screen(T("Customize colors"));
            screen.Body.Add(ThemePreview());
            screen.Items.Add(Entry.Header(T("Colors")));
            foreach (var (name, get, set) in Palette.Tokens)
                screen.Items.Add(new Entry(T(name), async () =>
                {
                    var before = ui.P;
                    try
                    {
                        int argb = await ui.PickColor(T(name), get(before).ToArgb(), live => ui.P = set(ui.P, System.Drawing.Color.FromArgb(live)));
                        ui.P = set(before, System.Drawing.Color.FromArgb(255, System.Drawing.Color.FromArgb(argb)));
                        theme.Store(ui.P); theme.Save(themeFile);
                    }
                    catch (Back) { ui.P = before; throw; }
                }) { Glyph = "■", GlyphColor = get(palette), Value = Palette.Hex(get(palette)) });
            screen.Items.Add(Entry.Header(T("Start from")));
            foreach (var (name, preset) in Palette.Presets)
                screen.Items.Add(Local(T(name), () => { theme.Base = name; theme.Store(preset); ApplyTheme(); }) with { Glyph = theme.Base == name ? "●" : null });
            var row = await ui.Menu(screen);
            if (row == null) return;
            try { await row.Run!(); } catch (Back) { }
        }
    }
}
