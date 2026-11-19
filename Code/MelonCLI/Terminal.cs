using System.Drawing;
using MelonUI.Base;
using MelonUI.Default;
using MelonUI.Managers;

namespace MelonCLI;

internal sealed class Back : Exception { }

internal sealed class Terminal(ConsoleWindowManager window, CancellationToken token, Func<string, string> translate)
{
    private Action? escape, relayout;
    private MUIPage? root;
    private MenuView? current;
    private int generation;
    private bool railFocus;
    private Entry? queued;
    private DateTime quitArmed;
    /// <summary>The last section page, drawn behind side-panel prompts.</summary>
    private Screen? main;
    /// <summary>Screens with their own body opened since the section page, so nested prompts keep their context.</summary>
    private sealed record Context(string Title, string Crumb, List<Block> Body);
    private readonly List<Context> trail = [];

    public Palette P { get; set; } = Palette.MelonDark;
    public string StatusText { get; set; } = "";
    public Tone StatusTone { get; set; } = Tone.Muted;
    public string StatusDetail { get; set; } = "";
    public int Generation => Volatile.Read(ref generation);
    public bool QuitArmed => DateTime.UtcNow - quitArmed < TimeSpan.FromSeconds(3);
    public Dictionary<string, int> ScrollMemory { get; } = [];
    public Dictionary<string, string> SelectionMemory { get; } = [];
    public string Translate(string key) => translate(key);

    /// <summary>A section the user moved to while the previous one was still loading.</summary>
    public Entry? TakeQueued() => Interlocked.Exchange(ref queued, null);

    public void BindKeys()
    {
        window.RegisterKeyboardControl(ConsoleKey.Escape, () => escape?.Invoke(), "Back");
        window.Updating += (_, _) => relayout?.Invoke();
    }

    /// <summary>Repaints the current screen, e.g. after a theme change.</summary>
    public Task Repaint() => window.InvokeAsync(() => current?.Invalidate());

    private T Style<T>(T element) where T : UIElement
    {
        var p = P;
        element.Foreground = element.FocusedForeground = p.Ink;
        element.Background = element.FocusedBackground = p.Surface;
        element.BorderColor = p.Border;
        element.FocusedBorderColor = p.Accent;
        if (element is TextBox box) { box.CursorColorBackground = p.Accent; box.CursorColorForeground = p.AccentInk; }
        return element;
    }

    private static void Place(UIElement e, int x, int y, int w, int h)
    {
        string sx = x.ToString(), sy = y.ToString(), sw = Math.Max(1, w).ToString(), sh = Math.Max(1, h).ToString();
        if (e.X != sx) e.X = sx;
        if (e.Y != sy) e.Y = sy;
        if (e.Width != sw) e.Width = sw;
        if (e.Height != sh) e.Height = sh;
    }

    private MUIPage Begin()
    {
        Interlocked.Increment(ref generation);
        escape = null; relayout = null; current = null;
        root?.Dispose();
        root = new MUIPage { ShowBorder = false, ConsiderForFocus = false, Background = P.Bg, Foreground = P.Ink };
        window.Elements.Add(root);
        return root;
    }

    /// <summary>Shows a prompt or detail page in the side panel, over the section page it was opened from.</summary>
    private Screen Compose(Screen sub)
    {
        // Re-showing a screen already in the trail (a refresh, or returning to a page loop) goes back to its place.
        int at = trail.FindIndex(c => c.Title == sub.Title);
        if (at >= 0) trail.RemoveRange(at, trail.Count - at);
        string parent = trail.Count > 0 ? trail[^1].Crumb : main?.ActiveSection ?? "";
        var behind = trail.Count > 0 ? trail[^1].Body : main?.Body ?? [];
        string crumb = parent.Length == 0 ? sub.Title : parent + "  ›  " + sub.Title;
        var shown = new Screen(sub.Title)
        {
            Sub = true, Crumb = crumb, ActiveSection = main?.ActiveSection ?? "", ActionsTitle = sub.Title,
            Notice = sub.Notice, NoticeTone = sub.NoticeTone, Overlay = sub.Overlay, Hints = sub.Hints
        };
        if (main != null) shown.Sections.AddRange(main.Sections);
        shown.Body.AddRange(sub.Body.Count > 0 ? sub.Body : behind);
        shown.PanelBlocks.AddRange(sub.PanelBlocks);
        if (!sub.Overlay) shown.Items.Add(new Entry(translate("Back"), () => Task.CompletedTask) { IsBack = true, Glyph = "←", GlyphTone = Tone.Muted });
        shown.Items.AddRange(sub.Items);
        shown.Shortcuts.AddRange(sub.Shortcuts);
        if (sub.Body.Count > 0) trail.Add(new Context(sub.Title, crumb, shown.Body));
        return shown;
    }

    /// <summary>Shows an animated wait screen. With <paramref name="onlyIfGeneration"/>, it is skipped when
    /// another screen has been shown since that generation, so slow actions never cover a prompt.</summary>
    public Task Busy(string text, int? onlyIfGeneration = null) => window.InvokeAsync(() =>
    {
        if (onlyIfGeneration is int expected && expected != Generation) return;
        var view = new BusyView(this, main?.ActiveSection ?? "", Draw.Clean(text)) { ShowBorder = false, ConsiderForFocus = false, Width = "100%", Height = "100%" };
        Begin().Children.Add(view);
    });

    /// <summary>Shows a section page (<paramref name="isMain"/>) or a side-panel screen and waits for a choice.
    /// Returns null for Back/Escape.</summary>
    public async Task<Entry?> Menu(Screen screen, Func<Task<Screen>>? refresh = null, bool isMain = false)
    {
        var answer = new TaskCompletionSource<Entry?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancel = token.Register(() => answer.TrySetCanceled(token));
        MenuView? view = null;
        void Pick(Entry entry, bool fromRail)
        {
            if (entry.IsBack) { answer.TrySetResult(null); return; }
            railFocus = fromRail;
            if (!answer.TrySetResult(entry) && fromRail) Interlocked.Exchange(ref queued, entry);
        }
        Screen Prepare(Screen s)
        {
            if (!isMain) return Compose(s);
            main = s; trail.Clear(); return s;
        }
        var shown = Prepare(screen);
        await window.InvokeAsync(() =>
        {
            var page = Begin();
            view = new MenuView(this, shown, railFocus, Pick, isMain ? "main:" + shown.ActiveSection : null);
            page.Children.Add(view);
            current = view;
            window.Focus(view);
            escape = isMain
                ? () => { if (QuitArmed) answer.TrySetResult(null); else { quitArmed = DateTime.UtcNow; view.Invalidate(); } }
                : () => answer.TrySetResult(null);
        });
        while (!answer.Task.IsCompleted)
        {
            if (await Task.WhenAny(answer.Task, Task.Delay(2000, token)) == answer.Task) break;
            if (refresh == null) { await window.InvokeAsync(() => view!.Invalidate()); continue; }
            var next = await refresh();
            if (answer.Task.IsCompleted) break;
            await window.InvokeAsync(() => { if (!answer.Task.IsCompleted) view!.Update(Prepare(next)); });
        }
        return await answer.Task;
    }

    /// <summary>Docks MelonUI controls in the side panel over the current page.</summary>
    private void Overlay(Screen sub, Action<MUIPage> add, Action<(int X, int Y, int W, int H)> layout, UIElement focus, Action onEscape)
    {
        sub.Overlay = true;
        var page = Begin();
        var view = new MenuView(this, Compose(sub), false, (_, _) => { });
        page.Children.Add(view);
        current = view;
        add(page);
        relayout = () => layout(view.HostArea(window.ViewportWidth, window.ViewportHeight));
        relayout();
        window.Focus(focus);
        escape = onEscape;
    }

    public async Task<string> PickPath(string title, bool directory, string initial, params string[] extensions)
    {
        var answer = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancel = token.Register(() => answer.TrySetCanceled(token));
        await window.InvokeAsync(() =>
        {
            var sub = new Screen(Draw.Clean(title))
            {
                Hints = directory
                    ? [("Tab", translate("Browse")), ("Enter", translate("Open")), ("Space", translate("Select folder")), ("Esc", translate("Back"))]
                    : [("Tab", translate("Browse")), ("Enter", translate("Open or select")), ("Esc", translate("Back"))]
            };
            sub.PanelBlocks.Add(new Para(translate(directory ? "Type a folder path, or press Tab to browse." : "Type a file path, or press Tab to browse.")
                + (extensions.Length > 0 ? "  " + string.Join(" ", extensions) : ""), Tone.Muted));
            var picker = Style(new FilePicker { SelectionMode = directory ? FilePickerMode.Directory : FilePickerMode.File });
            foreach (var extension in extensions) picker.Extensions.Add(extension);
            picker.NavigateTo(Directory.Exists(initial) ? initial : Path.GetDirectoryName(initial) is { Length: > 0 } parent && Directory.Exists(parent) ? parent : Environment.CurrentDirectory);
            picker.SelectionConfirmed += path => answer.TrySetResult(path);
            var pathBox = Style(new TextBox { Text = initial, OnEnter = (path, _) => answer.TrySetResult(path) });
            Overlay(sub, page => { page.Children.Add(pathBox); page.Children.Add(picker); },
                a => { Place(pathBox, a.X, a.Y, a.W, 3); Place(picker, a.X, a.Y + 4, a.W, a.H - 4); },
                pathBox, () => answer.TrySetException(new Back()));
        });
        return Path.GetFullPath(await answer.Task);
    }

    /// <summary>RGB sliders and a hex field. <paramref name="live"/> sees each change so callers can preview it.</summary>
    public async Task<int> PickColor(string title, int argb, Action<int>? live = null)
    {
        var answer = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancel = token.Register(() => answer.TrySetCanceled(token));
        await window.InvokeAsync(() =>
        {
            var sub = new Screen(Draw.Clean(title))
            {
                Hints = [("↑↓", translate("Channel")), ("←→", translate("Adjust")), ("Tab", translate("Hex")), ("Enter", translate("Save")), ("Esc", translate("Back"))]
            };
            sub.PanelBlocks.Add(new Para(translate("Adjust each channel, or press Tab to type a hex value."), Tone.Muted));
            var picker = Style(new RgbColorPicker());
            var hex = Style(new TextBox { Label = "#RRGGBB", Text = $"#{argb & 0xFFFFFF:X6}" });
            hex.OnEnter = (value, _) =>
            {
                if (ThemeSettings.TryParse(value, out var color)) answer.TrySetResult(color.ToArgb());
            };
            picker.BeginEdit(Color.FromArgb(argb));
            picker.ColorChanged += color => { hex.Text = $"#{color.ToArgb() & 0xFFFFFF:X6}"; live?.Invoke(color.ToArgb()); current?.Invalidate(); };
            picker.Confirmed += color => answer.TrySetResult(color.ToArgb());
            picker.Cancelled += _ => answer.TrySetException(new Back());
            Overlay(sub, page => { page.Children.Add(picker); page.Children.Add(hex); },
                a => { Place(picker, a.X, a.Y, a.W, 8); Place(hex, a.X, a.Y + 9, Math.Min(a.W, 30), 5); },
                picker, () => answer.TrySetException(new Back()));
        });
        return await answer.Task;
    }

    public async Task<string> Input(string title, string value = "", bool secret = false, bool multiline = false, string help = "")
    {
        var answer = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancel = token.Register(() => answer.TrySetCanceled(token));
        await window.InvokeAsync(() =>
        {
            var hints = new List<(string, string)> { (multiline ? "Shift+Enter" : "Enter", translate("Save")) };
            if (multiline) hints.Add(("Enter", translate("New line")));
            if (secret) hints.Add(("Ctrl+R", translate("Reveal")));
            hints.Add(("Esc", translate("Back")));
            var sub = new Screen(Draw.Clean(title)) { Hints = hints };
            if (help.Length > 0) sub.PanelBlocks.Add(new Para(help, Tone.Muted));
            var box = Style(new TextBox { Text = value, HideCharacters = secret, Multiline = multiline, OnEnter = (text, _) => answer.TrySetResult(text) });
            Overlay(sub, page => page.Children.Add(box),
                a => Place(box, a.X, a.Y, a.W, multiline ? Math.Max(3, a.H) : 3),
                box, () => answer.TrySetException(new Back()));
        });
        return await answer.Task;
    }
}
