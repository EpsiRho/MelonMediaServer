using System.Drawing;
using MelonUI.Base;

namespace MelonCLI;

internal record Entry(string Label, Func<Task>? Run)
{
    public string? Value { get; init; }
    public Tone Tone { get; init; }
    public Tone ValueTone { get; init; } = Tone.Muted;
    public string? Glyph { get; init; }
    public Tone GlyphTone { get; init; } = Tone.Accent;
    public Color? GlyphColor { get; init; }
    public bool IsBack { get; init; }
    public bool IsHeader => Run == null;
    public static Entry Header(string label) => new(label, null);
}

internal sealed record Shortcut(ConsoleKey Key, string Keys, string Hint, Entry Entry);

internal sealed class Screen(string title)
{
    public string Title { get; set; } = title;
    public string Notice { get; set; } = "";
    public Tone NoticeTone { get; set; } = Tone.Info;
    /// <summary>Main content on the left.</summary>
    public List<Block> Body { get; } = [];
    /// <summary>Content shown inside the side panel, above its items (prompts, warnings, help).</summary>
    public List<Block> PanelBlocks { get; } = [];
    public List<Entry> Items { get; } = [];
    public List<Entry> Sections { get; } = [];
    public string ActiveSection { get; set; } = "";
    public List<Shortcut> Shortcuts { get; } = [];
    public string ActionsTitle { get; set; } = "";
    /// <summary>Set by the terminal when this screen is shown in the side panel over a section page.</summary>
    public bool Sub { get; set; }
    /// <summary>The side panel hosts a MelonUI control (text box, picker) instead of items.</summary>
    public bool Overlay { get; set; }
    public string Crumb { get; set; } = "";
    public List<(string Keys, string Label)>? Hints { get; set; }

    public IEnumerable<Block> AllBlocks()
    {
        if (Notice.Length > 0) yield return new Callout(Notice, NoticeTone);
        foreach (var block in Body) yield return block;
    }
}

/// <summary>Shared header and footer so every screen has the same frame.</summary>
internal static class Chrome
{
    public static void Header(ConsoleBuffer b, Terminal t, string crumb)
    {
        var p = t.P;
        Draw.Fill(b, 0, 0, b.Width, 1, p.Surface);
        int x = Draw.Text(b, 0, 0, " ◆ Melon ", p.AccentInk, p.Accent);
        int statusWidth = Draw.Width(t.StatusText) + 2 + (t.StatusDetail.Length > 0 ? Draw.Width(t.StatusDetail) + 3 : 0) + 1;
        bool roomForStatus = b.Width - x - statusWidth > 12;
        if (crumb.Length > 0)
            Draw.Text(b, x + 2, 0, crumb, p.Muted, p.Surface, b.Width - x - 3 - (roomForStatus ? statusWidth + 2 : 0));
        if (!roomForStatus || t.StatusText.Length == 0) return;
        int right = b.Width - 1;
        if (t.StatusDetail.Length > 0)
        {
            Draw.TextRight(b, right, 0, t.StatusDetail, p.Muted, p.Surface);
            right -= Draw.Width(t.StatusDetail) + 3;
            Draw.Text(b, right + 1, 0, "·", p.Faint, p.Surface);
        }
        Draw.TextRight(b, right, 0, "● " + t.StatusText, p.Of(t.StatusTone), p.Surface);
    }

    public static void Footer(ConsoleBuffer b, Palette p, IEnumerable<(string Keys, string Label)> hints, string? right = null, Tone rightTone = Tone.Faint)
    {
        int y = b.Height - 1;
        Draw.Fill(b, 0, y, b.Width, 1, p.Surface);
        int x = 1, limit = b.Width - 1 - (right == null ? 0 : Draw.Width(right) + 2);
        foreach (var (keys, label) in hints)
        {
            int size = Draw.Width(keys) + Draw.Width(label) + 4;
            if (x + size > limit) break;
            x += Draw.Text(b, x, y, " " + keys + " ", p.Ink, p.Raised);
            x += Draw.Text(b, x, y, " " + label + "  ", p.Muted, p.Surface);
        }
        if (right != null) Draw.TextRight(b, b.Width - 1, y, right, p.Of(rightTone), p.Surface);
    }
}

/// <summary>The interactive screen: header, section rail, scrollable body, side panel and footer.
/// It is a single focus stop that owns Tab, so the rail and the panel behave like one control.</summary>
internal sealed class MenuView : UIElement
{
    private readonly Terminal term;
    private readonly Action<Entry, bool> pick;
    private Screen screen;
    private int rail, action, actionScroll, bodyScroll;
    private bool railFocus;
    private int bodyView = 1, bodyTotal, actionView = 1;
    private readonly string? memory;

    public MenuView(Terminal term, Screen screen, bool railFocus, Action<Entry, bool> pick, string? memoryKey = null)
    {
        this.term = term; this.pick = pick; this.screen = screen; memory = memoryKey;
        this.railFocus = railFocus && screen.Sections.Count > 0 && !screen.Sub;
        ShowBorder = false; ConsiderForFocus = !screen.Overlay; Width = "100%"; Height = "100%";
        rail = Math.Max(0, screen.Sections.FindIndex(s => s.Label == screen.ActiveSection));
        // Start on the current choice, else the first real option; dangerous confirmations start on Back.
        var options = Selectable;
        int marked = options.FirstOrDefault(i => screen.Items[i].Glyph == "●" && screen.Items[i].GlyphColor == null, -1);
        int first = options.FirstOrDefault(i => !screen.Items[i].IsBack, -1);
        bool risky = options.Any(i => screen.Items[i].Tone == Tone.Danger) && options.Count(i => !screen.Items[i].IsBack) == 1;
        action = risky ? options.FirstOrDefault(-1) : marked >= 0 ? marked : first >= 0 ? first : options.FirstOrDefault(-1);
        if (memory != null)
        {
            if (term.ScrollMemory.TryGetValue(memory, out var scroll)) bodyScroll = scroll;
            if (term.SelectionMemory.TryGetValue(memory, out var label))
            {
                int same = screen.Items.FindIndex(e => !e.IsHeader && e.Label == label);
                if (same >= 0) action = same;
            }
        }
    }

    private List<int> Selectable => screen.Items.Select((e, i) => (e, i)).Where(x => !x.e.IsHeader).Select(x => x.i).ToList();

    private void Remember()
    {
        if (memory == null) return;
        term.ScrollMemory[memory] = bodyScroll;
        if (action >= 0 && action < screen.Items.Count) term.SelectionMemory[memory] = screen.Items[action].Label;
    }

    public void Update(Screen next)
    {
        string? old = action >= 0 && action < screen.Items.Count ? screen.Items[action].Label : null;
        int position = Selectable.IndexOf(action);
        screen = next;
        var selectable = Selectable;
        int same = old == null ? -1 : next.Items.FindIndex(e => !e.IsHeader && e.Label == old);
        action = same >= 0 ? same : selectable.Count == 0 ? -1 : selectable[Math.Clamp(position, 0, selectable.Count - 1)];
        if (next.Sections.Count == 0 || next.Sub) railFocus = false;
        int active = next.Sections.FindIndex(s => s.Label == next.ActiveSection);
        if (active >= 0) rail = active;
        Invalidate();
    }

    public override bool TryHandleKey(ConsoleKeyInfo key)
    {
        if (base.TryHandleKey(key)) return true;
        if ((key.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) != 0) return false;
        bool arrows = key.Key is ConsoleKey.UpArrow or ConsoleKey.DownArrow or ConsoleKey.LeftArrow or ConsoleKey.RightArrow;
        foreach (var shortcut in screen.Shortcuts)
            if (shortcut.Key == key.Key && !(railFocus && arrows)) { pick(shortcut.Entry, false); return true; }
        bool hasRail = screen.Sections.Count > 0 && !screen.Sub;
        var selectable = Selectable;
        bool bodyScrolls = bodyTotal > bodyView;
        switch (key.Key)
        {
            case ConsoleKey.Tab:
                if (hasRail) { railFocus = !railFocus; Invalidate(); }
                return true;
            case ConsoleKey.LeftArrow when hasRail && !railFocus:
                railFocus = true; Invalidate(); return true;
            case ConsoleKey.RightArrow when railFocus:
                railFocus = false; Invalidate(); return true;
            case ConsoleKey.UpArrow or ConsoleKey.DownArrow when railFocus:
            {
                int next = (rail + (key.Key == ConsoleKey.UpArrow ? -1 : 1) + screen.Sections.Count) % screen.Sections.Count;
                rail = next; Invalidate();
                pick(screen.Sections[next], true); return true;
            }
            case ConsoleKey.Enter when railFocus:
                railFocus = false; Invalidate(); return true;
            case ConsoleKey.UpArrow or ConsoleKey.DownArrow when selectable.Count <= 1 && bodyScrolls:
                Scroll(key.Key == ConsoleKey.UpArrow ? -1 : 1); return true;
            case ConsoleKey.UpArrow: Move(-1); return true;
            case ConsoleKey.DownArrow: Move(1); return true;
            case ConsoleKey.Home: if (selectable.Count > 0) action = selectable[0]; Remember(); Invalidate(); return true;
            case ConsoleKey.End: if (selectable.Count > 0) action = selectable[^1]; Remember(); Invalidate(); return true;
            case ConsoleKey.PageUp or ConsoleKey.PageDown:
                int sign = key.Key == ConsoleKey.PageUp ? -1 : 1;
                if (bodyScrolls) Scroll(sign * Math.Max(1, bodyView - 2)); else Move(sign * Math.Max(1, actionView - 1));
                return true;
            case ConsoleKey.Enter or ConsoleKey.Spacebar:
                if (action >= 0 && action < screen.Items.Count && !screen.Items[action].IsHeader) { Remember(); pick(screen.Items[action], false); }
                return true;
        }
        if (char.IsLetterOrDigit(key.KeyChar) && selectable.Count > 0)
        {
            int start = Math.Max(0, selectable.IndexOf(action));
            for (int offset = 1; offset <= selectable.Count; offset++)
            {
                int index = selectable[(start + offset) % selectable.Count];
                if (screen.Items[index].Label.StartsWith(key.KeyChar.ToString(), StringComparison.CurrentCultureIgnoreCase))
                { action = index; railFocus = false; Remember(); Invalidate(); break; }
            }
            return true;
        }
        return false;
    }

    private void Scroll(int delta) { bodyScroll = Math.Clamp(bodyScroll + delta, 0, Math.Max(0, bodyTotal - bodyView)); Remember(); Invalidate(); }

    private void Move(int delta)
    {
        var selectable = Selectable;
        if (selectable.Count == 0) return;
        int position = Math.Max(0, selectable.IndexOf(action));
        if (Math.Abs(delta) == 1) position = (position + delta + selectable.Count) % selectable.Count;
        else position = Math.Clamp(position + delta, 0, selectable.Count - 1);
        action = selectable[position]; Remember(); Invalidate();
    }

    private readonly record struct Frame(int Rail, int X0, int Top, int BodyW, int BodyH, int Ax, int Ay, int Aw, int Ah, int BlocksH);

    private Frame Layout(int W, int H)
    {
        bool hasRail = screen.Sections.Count > 0;
        int R = hasRail && W >= 72 ? Math.Clamp(screen.Sections.Max(s => Draw.Width(s.Label)) + 6, 16, 22) : 0;
        int x0 = R + 2, cw = Math.Max(10, W - x0 - 2);
        int top = 2, areaH = Math.Max(1, H - 1 - top - 1);
        bool body = screen.AllBlocks().Any();
        int n = screen.Items.Count;
        int bodyW = cw, bodyH = areaH, ax = x0, ay = top, aw = cw, ah = areaH;
        if (!body) bodyH = 0;
        else if (cw >= 80)
        {
            // Side by side: the panel gets close to half the content width so labels and values fit.
            aw = Math.Clamp(cw * 45 / 100, 34, 90);
            bodyW = cw - aw - 2; ax = x0 + bodyW + 2;
        }
        else if (screen.Overlay) bodyH = 0;
        else
        {
            int want = n + 2 + PanelBlocksHeight(Math.Max(1, aw - 4));
            ah = Math.Min(want, Math.Max(6, areaH / 2));
            bodyH = areaH - ah - 1; ay = top + bodyH + 1;
            if (bodyH < 3) { bodyH = 0; ay = top; ah = areaH; }
        }
        if (n == 0 && !screen.Overlay && screen.PanelBlocks.Count == 0) { ah = 0; if (body) { bodyW = cw; bodyH = areaH; } }
        return new Frame(R, x0, top, bodyW, bodyH, ax, ay, aw, ah, PanelBlocksHeight(Math.Max(1, aw - 4)));
    }

    private int PanelBlocksHeight(int width) => screen.PanelBlocks.Count == 0 ? 0
        : screen.PanelBlocks.Sum(b => b.Measure(width)) + screen.PanelBlocks.Count;

    /// <summary>Where an overlay control (text box, picker) goes: inside the side panel, below its blocks.</summary>
    public (int X, int Y, int W, int H) HostArea(int W, int H)
    {
        var f = Layout(W, H);
        int y = f.Ay + 1 + f.BlocksH;
        return (f.Ax + 2, y, Math.Max(1, f.Aw - 4), Math.Max(1, f.Ay + f.Ah - 1 - y));
    }

    protected override void RenderContent(ConsoleBuffer b)
    {
        var p = term.P;
        int W = b.Width, H = b.Height;
        Draw.Fill(b, 0, 0, W, H, p.Bg);
        if (W < 24 || H < 8) { Draw.Text(b, 0, 0, "Melon", p.Accent, p.Bg); return; }
        bool focused = IsFocused || screen.Overlay;
        bool hasRail = screen.Sections.Count > 0;
        var f = Layout(W, H);
        // The header names the page only when the rail cannot: sub-panels and narrow terminals.
        Chrome.Header(b, term, screen.Sub ? screen.Crumb : hasRail && f.Rail == 0 ? screen.ActiveSection : "");

        // Section rail: text only, so every row has a predictable width in any font.
        if (f.Rail > 0)
        {
            Draw.Fill(b, 0, 1, f.Rail, H - 2, p.Surface);
            int spacing = H - 6 >= screen.Sections.Count * 2 ? 2 : 1;
            for (int i = 0; i < screen.Sections.Count; i++)
            {
                var s = screen.Sections[i];
                int y = 2 + i * spacing;
                if (y >= H - 2) break;
                bool selected = i == rail;
                bool lit = selected && railFocus && focused && !screen.Sub;
                Color fg = screen.Sub ? p.Faint : p.Ink, bg = p.Surface;
                if (lit) { fg = p.AccentInk; bg = p.Accent; }
                else if (selected) { fg = screen.Sub ? p.Muted : p.Accent; bg = p.Raised; }
                Draw.Fill(b, 0, y, f.Rail, 1, bg);
                if (selected && !lit) Draw.Cell(b, 0, y, '▌', screen.Sub ? p.Faint : p.Accent, bg);
                Draw.Text(b, 3, y, s.Label, fg, bg, f.Rail - 4);
            }
        }

        // Body: render everything into a tall buffer, then copy the visible window.
        var blocks = screen.AllBlocks().ToList();
        bodyTotal = 0; bodyView = Math.Max(1, f.BodyH);
        if (f.BodyH > 0)
        {
            int Total(int width) => blocks.Sum(blk => blk.Measure(width)) + blocks.Count - 1;
            int inner = f.BodyW;
            bodyTotal = Total(inner);
            if (bodyTotal > f.BodyH) { inner = f.BodyW - 2; bodyTotal = Total(inner); }
            bodyScroll = Math.Clamp(bodyScroll, 0, Math.Max(0, bodyTotal - f.BodyH));
            var canvas = new ConsoleBuffer(Math.Max(1, inner), Math.Max(1, bodyTotal));
            Draw.Fill(canvas, 0, 0, canvas.Width, canvas.Height, p.Bg);
            int y = 0;
            foreach (var block in blocks)
            {
                int h = block.Measure(inner);
                block.Render(canvas, p, p.Bg, 0, y, inner, h);
                y += h + 1;
            }
            Draw.Blit(b, canvas, f.X0, f.Top, bodyScroll, f.BodyH);
            if (bodyTotal > f.BodyH)
            {
                int track = f.BodyH, thumb = Math.Max(1, track * f.BodyH / bodyTotal);
                int pos = (int)Math.Round((track - thumb) * (bodyScroll / (double)(bodyTotal - f.BodyH)));
                for (int i = 0; i < track; i++)
                {
                    bool on = i >= pos && i < pos + thumb;
                    Draw.Cell(b, f.X0 + f.BodyW - 1, f.Top + i, on ? '┃' : '│', on ? p.Accent : p.Border, p.Bg);
                }
            }
        }

        // Side panel
        int n = screen.Items.Count;
        actionView = Math.Max(1, f.Ah - 2 - f.BlocksH);
        if (f.Ah >= 3)
        {
            var title = screen.ActionsTitle.Length > 0 ? screen.ActionsTitle : term.Translate("Actions");
            var selectable = Selectable;
            bool active = (focused && !railFocus) || screen.Sub;
            Draw.Box(b, f.Ax, f.Ay, f.Aw, f.Ah, active ? Palette.Mix(p.Border, p.Accent, 0.45) : p.Border, p.Surface, title, screen.Sub ? p.Ink : p.Muted);
            int by = f.Ay + 1;
            foreach (var block in screen.PanelBlocks)
            {
                int h = block.Measure(f.Aw - 4);
                block.Render(b, p, p.Surface, f.Ax + 2, by, f.Aw - 4, h);
                by += h + 1;
            }
            int rows = f.Ah - 2 - f.BlocksH, iw = f.Aw - 2;
            if (selectable.Count > rows && rows > 0)
                Draw.TextRight(b, f.Ax + f.Aw - 2, f.Ay + f.Ah - 1, $" {Math.Max(0, selectable.IndexOf(action)) + 1}/{selectable.Count} ", p.Faint, p.Surface);
            if (action >= 0 && rows > 0)
            {
                if (action < actionScroll) actionScroll = action;
                if (action >= actionScroll + rows) actionScroll = action - rows + 1;
                if (actionScroll > 0 && screen.Items[actionScroll - 1].IsHeader && action - (actionScroll - 1) < rows) actionScroll--;
            }
            actionScroll = Math.Clamp(actionScroll, 0, Math.Max(0, n - rows));
            bool glyphs = screen.Items.Any(e => e.Glyph != null);
            for (int r = 0; r < rows && r + actionScroll < n; r++)
            {
                int index = r + actionScroll, y = f.Ay + 1 + f.BlocksH + r;
                var e = screen.Items[index];
                if (e.IsHeader)
                {
                    int used = Draw.Text(b, f.Ax + 2, y, e.Label.ToUpperInvariant(), p.Faint, p.Surface, iw - 2);
                    Draw.HLine(b, f.Ax + 3 + used, y, Math.Max(0, iw - used - 3), Palette.Mix(p.Surface, p.Border, 0.7), p.Surface);
                    continue;
                }
                bool selected = index == action;
                bool lit = selected && focused && !railFocus;
                Color fg = e.IsBack ? p.Muted : p.Of(e.Tone), bg = p.Surface;
                if (lit) { bg = e.Tone == Tone.Danger ? p.Danger : p.Accent; fg = e.Tone == Tone.Danger ? p.Bg : p.AccentInk; }
                else if (selected) bg = p.Raised;
                Draw.Fill(b, f.Ax + 1, y, iw, 1, bg);
                int cx = f.Ax + 1;
                cx += Draw.Text(b, cx, y, lit ? "›" : " ", fg, bg);
                cx++;
                if (glyphs)
                {
                    if (e.Glyph != null) Draw.Text(b, cx, y, e.Glyph, lit ? fg : e.GlyphColor ?? p.Of(e.GlyphTone), bg, 1);
                    cx += 2;
                }
                // The label wins: the value takes what is left, and disappears when that is too little to read.
                int avail = f.Ax + f.Aw - 2 - cx, labelWidth = Draw.Width(e.Label);
                int valueWidth = e.Value == null ? 0 : Math.Min(Draw.Width(e.Value), avail - Math.Min(labelWidth, avail * 3 / 5) - 2);
                if (valueWidth < 4 && Draw.Width(e.Value) > valueWidth) valueWidth = 0;
                Draw.Text(b, cx, y, e.Label, fg, bg, avail - (valueWidth > 0 ? valueWidth + 2 : 0));
                if (valueWidth > 0) Draw.TextRight(b, f.Ax + f.Aw - 2, y, Draw.Clip(e.Value, valueWidth), lit ? fg : p.Of(e.ValueTone), bg);
            }
        }

        // Footer
        string T(string key) => term.Translate(key);
        var hints = new List<(string, string)>();
        if (screen.Hints != null) hints.AddRange(screen.Hints);
        else if (railFocus) { hints.Add(("↑↓", T("Sections"))); hints.Add(("→", T("Actions"))); }
        else
        {
            if (Selectable.Count > 1) hints.Add(("↑↓", T("Move")));
            if (Selectable.Count > 0) hints.Add(("Enter", T("Select")));
            if (hasRail && !screen.Sub) hints.Add(("Tab", T("Sections")));
        }
        if (screen.Hints == null)
        {
            if (bodyTotal > bodyView) hints.Add((Selectable.Count <= 1 ? "↑↓ PgUp PgDn" : "PgUp PgDn", T("Scroll")));
            foreach (var shortcut in screen.Shortcuts.Where(s => s.Keys.Length > 0)) hints.Add((shortcut.Keys, shortcut.Hint));
            hints.Add(("Esc", T(screen.Sub ? "Back" : "Quit")));
        }
        bool armed = !screen.Sub && term.QuitArmed;
        Chrome.Footer(b, p, hints, armed ? T("Press Esc again to quit") : null, armed ? Tone.Warn : Tone.Faint);
    }
}

/// <summary>A centred, animated progress message.</summary>
internal sealed class BusyView(Terminal term, string crumb, string message) : UIElement
{
    private static readonly string Frames = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏";
    private readonly DateTime started = DateTime.UtcNow;
    public override bool EnableCaching { get => false; set { } }
    protected override void RenderContent(ConsoleBuffer b)
    {
        var p = term.P;
        Draw.Fill(b, 0, 0, b.Width, b.Height, p.Bg);
        Chrome.Header(b, term, crumb);
        var elapsed = DateTime.UtcNow - started;
        char frame = Frames[(int)(elapsed.TotalMilliseconds / 80) % Frames.Length];
        int y = Math.Max(3, b.Height / 2 - 1);
        Draw.TextCenter(b, 0, y, b.Width, frame + "  " + message, p.Ink, p.Bg);
        if (elapsed.TotalSeconds >= 3) Draw.TextCenter(b, 0, y + 2, b.Width, $"{elapsed.TotalSeconds:0}s", p.Faint, p.Bg);
        int width = Math.Min(30, b.Width - 4), left = (b.Width - width) / 2;
        int head = (int)(elapsed.TotalMilliseconds / 40) % (width + 8) - 4;
        for (int i = 0; i < width; i++)
        {
            double d = Math.Abs(i - head);
            Draw.Cell(b, left + i, y + 1, '━', Palette.Mix(p.Raised, p.Accent, Math.Max(0, 1 - d / 5)), p.Bg);
        }
        Chrome.Footer(b, p, []);
    }
}
