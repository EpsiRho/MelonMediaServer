using System.Drawing;
using MelonUI.Base;

namespace MelonCLI;

/// <summary>A vertically stacked piece of screen content. Measure must return the height Render uses.</summary>
internal abstract class Block
{
    public abstract int Measure(int width);
    /// <param name="height">The allotted height; at least <see cref="Measure"/>, larger when stretched to match a sibling.</param>
    public abstract void Render(ConsoleBuffer b, Palette p, Color bg, int x, int y, int width, int height);
}

internal sealed class Gap(int rows = 1) : Block
{
    public override int Measure(int width) => rows;
    public override void Render(ConsoleBuffer b, Palette p, Color bg, int x, int y, int width, int height) { }
}

internal sealed class Para(string text, Tone tone = Tone.Normal) : Block
{
    public override int Measure(int width) => Draw.Wrap(text, width).Count();
    public override void Render(ConsoleBuffer b, Palette p, Color bg, int x, int y, int width, int height)
    {
        foreach (var line in Draw.Wrap(text, width)) Draw.Text(b, x, y++, line, p.Of(tone), bg, width);
    }
}

/// <summary>Vertical composition with blank rows between children.</summary>
internal sealed class Stack(IEnumerable<Block> children, int gap = 1) : Block
{
    private readonly Block[] items = children.ToArray();
    public override int Measure(int width) => items.Sum(i => i.Measure(width)) + Math.Max(0, items.Length - 1) * gap;
    public override void Render(ConsoleBuffer b, Palette p, Color bg, int x, int y, int width, int height)
    {
        foreach (var item in items) { int h = item.Measure(width); item.Render(b, p, bg, x, y, width, h); y += h + gap; }
    }
}

/// <summary>A flexible left block beside a fixed-width right block; stacks when there is not room for both.</summary>
internal sealed class Split(Block left, Block right, int rightWidth, int minLeft) : Block
{
    private bool Fits(int width) => width >= rightWidth + 2 + minLeft;
    public override int Measure(int width) => Fits(width)
        ? Math.Max(left.Measure(width - rightWidth - 2), right.Measure(rightWidth))
        : left.Measure(width) + 1 + right.Measure(width);
    public override void Render(ConsoleBuffer b, Palette p, Color bg, int x, int y, int width, int height)
    {
        if (!Fits(width)) { new Stack([left, right]).Render(b, p, bg, x, y, width, height); return; }
        int tall = Measure(width);
        left.Render(b, p, bg, x, y, width - rightWidth - 2, tall);
        right.Render(b, p, bg, x + width - rightWidth, y, rightWidth, tall);
    }
}

/// <summary>Side-by-side children that fall back to a stack on narrow terminals. Panels stretch to equal height.</summary>
internal sealed class Columns(int minWidth, params Block[] children) : Block
{
    private bool Fits(int width) => width >= children.Length * minWidth + (children.Length - 1) * 2;
    private int Each(int width) => (width - (children.Length - 1) * 2) / children.Length;
    public override int Measure(int width) => Fits(width)
        ? children.Max(c => c.Measure(Each(width)))
        : children.Sum(c => c.Measure(width)) + children.Length - 1;
    public override void Render(ConsoleBuffer b, Palette p, Color bg, int x, int y, int width, int height)
    {
        if (!Fits(width)) { new Stack(children).Render(b, p, bg, x, y, width, height); return; }
        int each = Each(width), tall = Measure(width);
        for (int i = 0; i < children.Length; i++)
        {
            int w = i == children.Length - 1 ? width - (each + 2) * i : each;
            children[i].Render(b, p, bg, x + (each + 2) * i, y, w, tall);
        }
    }
}

internal sealed class Panel(string title, Block inner, string? right = null, Tone rightTone = Tone.Muted, Tone titleTone = Tone.Normal) : Block
{
    public override int Measure(int width) => inner.Measure(Math.Max(1, width - 4)) + 2;
    public override void Render(ConsoleBuffer b, Palette p, Color bg, int x, int y, int width, int height)
    {
        Draw.Box(b, x, y, width, height, p.Border, p.Surface, title, p.Of(titleTone), right, p.Of(rightTone));
        int inside = Math.Max(1, width - 4);
        inner.Render(b, p, p.Surface, x + 2, y + 1, inside, Math.Max(inner.Measure(inside), height - 2));
    }
}

internal sealed class Callout(string text, Tone tone = Tone.Info, string? title = null) : Block
{
    // Plain marks only: symbol glyphs vary in width between fonts and Unicode versions.
    private static string Icon(Tone tone) => tone switch
    { Tone.Warn or Tone.Danger => "!", Tone.Info => "i", _ => "•" };
    private int TextRows(int width) => text.Length == 0 ? 0 : Draw.Wrap(text, Math.Max(1, width - 5)).Count();
    public override int Measure(int width) => TextRows(width) + (title == null ? 0 : 1);
    public override void Render(ConsoleBuffer b, Palette p, Color bg, int x, int y, int width, int height)
    {
        var color = p.Of(tone);
        var shade = Palette.Mix(bg, color, 0.13);
        Draw.Fill(b, x, y, width, height, shade);
        for (int row = 0; row < height; row++) Draw.Cell(b, x, y + row, '▌', color, shade);
        Draw.Text(b, x + 2, y, Icon(tone), color, shade);
        int line = y;
        if (title != null) Draw.Text(b, x + 4, line++, title, color, shade, width - 5);
        foreach (var row in text.Length == 0 ? [] : Draw.Wrap(text, Math.Max(1, width - 5)))
            Draw.Text(b, x + 4, line++, row, title == null ? p.Ink : p.Muted, shade, width - 5);
    }
}

/// <summary>A KPI tile. Values are always plain text colour; <see cref="Dot"/> adds a coloured status dot.</summary>
internal sealed record Card(string Label, string Value, string? Sub = null, Tone SubTone = Tone.Muted,
    IReadOnlyList<double>? Spark = null, Tone SparkTone = Tone.Accent, Tone? Dot = null);

/// <summary>A wrapping grid of KPI tiles.</summary>
internal sealed class Cards(IEnumerable<Card> cards, int minWidth = 20) : Block
{
    private readonly Card[] items = cards.ToArray();
    private int Cols(int width)
    {
        // Balance the rows: six tiles that fit four across become two rows of three.
        int max = Math.Max(1, Math.Min(items.Length, (width + 1) / (minWidth + 1)));
        int rows = (int)Math.Ceiling(items.Length / (double)max);
        return Math.Max(1, (int)Math.Ceiling(items.Length / (double)rows));
    }
    private int RowHeight => items.Any(c => c.Spark != null) ? 6 : items.Any(c => c.Sub != null) ? 5 : 4;
    public override int Measure(int width) => items.Length == 0 ? 0 : (int)Math.Ceiling(items.Length / (double)Cols(width)) * RowHeight;
    public override void Render(ConsoleBuffer b, Palette p, Color bg, int x, int y, int width, int height)
    {
        int cols = Cols(width), each = (width - (cols - 1)) / cols;
        for (int i = 0; i < items.Length; i++)
        {
            int col = i % cols, row = i / cols;
            int cx = x + col * (each + 1), cy = y + row * RowHeight;
            int w = col == cols - 1 ? width - col * (each + 1) : each;
            var card = items[i];
            Draw.Box(b, cx, cy, w, RowHeight, p.Border, p.Surface);
            int inner = w - 4;
            Draw.Text(b, cx + 2, cy + 1, card.Label.ToUpperInvariant(), p.Muted, p.Surface, inner);
            int vx = cx + 2;
            if (card.Dot is Tone dot) vx += Draw.Text(b, vx, cy + 2, "● ", p.Of(dot), p.Surface);
            Draw.Text(b, vx, cy + 2, card.Value, p.Ink, p.Surface, cx + 2 + inner - vx);
            if (card.Sub != null) Draw.Text(b, cx + 2, cy + 3, card.Sub, p.Of(card.SubTone), p.Surface, inner);
            if (card.Spark is { Count: > 0 } spark)
            {
                var points = Resample(spark, inner);
                Draw.Text(b, cx + 2, cy + RowHeight - 2, Draw.Spark(points), p.Of(card.SparkTone), p.Surface, inner);
            }
        }
    }

    /// <summary>Fits a series into a fixed number of cells by summing neighbours (or stretching short series).</summary>
    public static double[] Resample(IReadOnlyList<double> values, int cells)
    {
        if (cells <= 0 || values.Count == 0) return [];
        if (values.Count <= cells)
        {
            int repeat = Math.Max(1, cells / values.Count);
            return values.SelectMany(v => Enumerable.Repeat(v, repeat)).ToArray();
        }
        var output = new double[cells];
        for (int i = 0; i < values.Count; i++) output[(int)((long)i * cells / values.Count)] += values[i];
        return output;
    }
}

internal sealed class KeyValues(IEnumerable<(string Key, string Value, Tone Tone)> rows) : Block
{
    private readonly (string Key, string Value, Tone Tone)[] items = rows.ToArray();
    public override int Measure(int width) => items.Length;
    public override void Render(ConsoleBuffer b, Palette p, Color bg, int x, int y, int width, int height)
    {
        int keyWidth = Math.Min(items.Select(r => Draw.Width(r.Key)).DefaultIfEmpty(0).Max() + 3, Math.Max(8, width * 9 / 20));
        foreach (var (key, value, tone) in items)
        {
            Draw.Text(b, x, y, key, p.Muted, bg, keyWidth - 2);
            Draw.Text(b, x + keyWidth, y++, value, p.Of(tone), bg, width - keyWidth);
        }
    }
}

/// <summary>A pill strip of filter values, wrapping onto more lines when needed.</summary>
internal sealed class Chips(IEnumerable<(string Label, string Value)> chips) : Block
{
    private readonly (string Label, string Value)[] items = chips.ToArray();
    private static int Size((string Label, string Value) c) => Draw.Width(c.Label) + Draw.Width(c.Value) + 3;
    private IEnumerable<(int Line, int Offset, (string Label, string Value) Chip)> Place(int width)
    {
        int line = 0, used = 0;
        foreach (var chip in items)
        {
            int size = Size(chip);
            if (used > 0 && used + size > width) { line++; used = 0; }
            yield return (line, used, chip); used += size + 2;
        }
    }
    public override int Measure(int width) => items.Length == 0 ? 0 : Place(width).Max(c => c.Line) + 1;
    public override void Render(ConsoleBuffer b, Palette p, Color bg, int x, int y, int width, int height)
    {
        foreach (var (line, offset, (label, value)) in Place(width))
        {
            int cx = x + offset;
            cx += Draw.Text(b, cx, y + line, label.ToUpperInvariant() + " ", p.Muted, bg, width - offset);
            Draw.Text(b, cx, y + line, " " + value + " ", p.Ink, p.Raised, x + width - cx);
        }
    }
}

internal sealed class Meter(string label, double fraction, string right, Tone tone = Tone.Accent) : Block
{
    public override int Measure(int width) => 2;
    public override void Render(ConsoleBuffer b, Palette p, Color bg, int x, int y, int width, int height)
    {
        int used = Draw.Text(b, x, y, label, p.Ink, bg, width);
        Draw.TextRight(b, x + width, y, right, p.Muted, bg, Math.Max(0, width - used - 1));
        Draw.Bar(b, x, y + 1, width, fraction, p.Of(tone), p.Raised, bg, '━');
    }
}

internal sealed record HBar(string Label, double Value, string Text, Tone Tone = Tone.Accent, string? Prefix = null, bool Highlight = false);

/// <summary>Ranked horizontal bars: prefix · label · bar · value.</summary>
internal sealed class HBars(IEnumerable<HBar> rows, double? max = null) : Block
{
    private readonly HBar[] items = rows.ToArray();
    public override int Measure(int width) => items.Length;
    public override void Render(ConsoleBuffer b, Palette p, Color bg, int x, int y, int width, int height)
    {
        if (items.Length == 0) return;
        double peak = max ?? items.Max(r => r.Value);
        int prefix = items.Max(r => Draw.Width(r.Prefix)), text = items.Max(r => Draw.Width(r.Text));
        int fixedWidth = prefix + (prefix > 0 ? 1 : 0) + text + 2;
        int room = width - fixedWidth - 1, minBar = Math.Max(6, room / 3);
        int label = Math.Min(items.Max(r => Draw.Width(r.Label)), Math.Max(6, room - minBar));
        int bar = Math.Max(1, width - fixedWidth - label - 1);
        foreach (var row in items)
        {
            int cx = x;
            if (prefix > 0) { Draw.TextRight(b, cx + prefix, y, row.Prefix, p.Faint, bg); cx += prefix + 1; }
            Draw.Text(b, cx, y, row.Label, p.Ink, bg, label);
            cx += label + 1;
            Draw.Bar(b, cx, y, bar, peak <= 0 ? 0 : row.Value / peak, p.Of(row.Tone), p.Raised, bg, '─');
            Draw.TextRight(b, x + width, y, row.Text, row.Highlight ? p.Ink : p.Muted, bg);
            y++;
        }
    }
}

internal sealed record Bucket(string Label, double A, double B, double C = 0);

/// <summary>Vertical stacked column chart (A at the bottom, B on top) with a y axis, gridlines,
/// x labels, a legend and an optional aligned sparkline for a third series.</summary>
internal sealed class ColumnChart(IReadOnlyList<Bucket> buckets, int rows, string aName, string bName, string? cName = null, string? note = null,
    Func<int, string>? merged = null) : Block
{
    public override int Measure(int width) => rows + 3 + (cName != null ? 1 : 0);

    public override void Render(ConsoleBuffer b, Palette p, Color bg, int x, int y, int width, int height)
    {
        var data = buckets;
        double peak = Math.Max(1, data.Max(d => d.A + d.B));
        string top = Fmt.Compact(peak), mid = Fmt.Compact(peak / 2);
        int axis = Math.Max(Math.Max(Draw.Width(top), Draw.Width(mid)), cName == null ? 1 : Math.Min(5, Draw.Width(cName))) + 2;
        int plot = width - axis;
        if (plot < 4 || data.Count == 0) return;
        if (data.Count > plot)
        {
            int k = (int)Math.Ceiling(data.Count / (double)plot);
            // Average rather than sum so the axis keeps the caller's unit (per day, per week).
            data = data.Chunk(k).Select(g => new Bucket(g[0].Label, g.Average(d => d.A), g.Average(d => d.B), g.Average(d => d.C))).ToArray();
            peak = Math.Max(1, data.Max(d => d.A + d.B)); top = Fmt.Compact(peak); mid = Fmt.Compact(peak / 2);
        }
        int n = data.Count;
        int group = (int)Math.Ceiling(buckets.Count / (double)n);
        string? caption = group > 1 && merged != null ? merged(group) + (note == null ? "" : "  ·  " + note) : note;
        int gap = plot / n >= 3 ? 1 : 0;
        int bw = Math.Clamp((plot + gap) / n - gap, 1, 8);
        int used = n * bw + (n - 1) * gap;
        int left = x + axis + Math.Max(0, (plot - used) / 2);

        // Legend
        int lx = x;
        lx += Draw.Text(b, lx, y, "■ ", p.Accent, bg); lx += Draw.Text(b, lx, y, aName + "   ", p.Muted, bg);
        lx += Draw.Text(b, lx, y, "■ ", p.Flesh, bg); lx += Draw.Text(b, lx, y, bName, p.Muted, bg);
        if (caption != null) Draw.TextRight(b, x + width, y, caption, p.Faint, bg, Math.Max(0, width - (lx - x) - 2));

        int baseY = y + rows; // row index of the bottom plot row is baseY
        // Axis and gridlines
        for (int r = 0; r < rows; r++)
        {
            int row = baseY - r;
            bool grid = r == rows - 1 || r == rows / 2;
            string label = r == rows - 1 ? top : r == rows / 2 ? mid : "";
            Draw.TextRight(b, x + axis - 2, row, label, p.Faint, bg);
            Draw.Cell(b, x + axis - 1, row, grid ? '┤' : '│', p.Border, bg);
            if (grid) for (int i = x + axis; i < x + width; i++) Draw.Cell(b, i, row, '┈', Palette.Mix(bg, p.Border, 0.6), bg);
        }
        Draw.TextRight(b, x + axis - 2, baseY + 1, "0", p.Faint, bg);
        Draw.Cell(b, x + axis - 1, baseY + 1, '└', p.Border, bg);
        Draw.HLine(b, x + axis, baseY + 1, width - axis, p.Border, bg);

        int units = rows * 8;
        int lastLabelEnd = int.MinValue;
        double cPeak = data.Max(d => d.C);
        for (int i = 0; i < n; i++)
        {
            var d = data[i];
            int cx = left + i * (bw + gap);
            int pa = (int)Math.Round(d.A / peak * units), pt = (int)Math.Round((d.A + d.B) / peak * units);
            if (d.A > 0) pa = Math.Max(pa, 1);
            if (d.B > 0) pt = Math.Max(pt, pa + 1);
            for (int r = 0; r < rows; r++)
            {
                int lo = r * 8;
                int inA = Math.Clamp(pa - lo, 0, 8), inT = Math.Clamp(pt - lo, 0, 8);
                char c; Color fg, back = bg;
                if (inA == 8) { c = '█'; fg = p.Accent; }
                else if (inA > 0) { c = Draw.Eighths[inA]; fg = p.Accent; if (inT - inA >= 4 || inT == 8) back = p.Flesh; }
                else if (inT > 0) { c = Draw.Eighths[inT]; fg = p.Flesh; }
                else continue;
                for (int k = 0; k < bw; k++) Draw.Cell(b, cx + k, baseY - r, c, fg, back);
            }
            if (d.A + d.B == 0) for (int k = 0; k < bw; k++) Draw.Cell(b, cx + k, baseY, '▁', p.Raised, bg);
            if (cName != null)
            {
                char s = d.C <= 0 || cPeak <= 0 ? '·' : Draw.Eighths[Math.Clamp((int)Math.Ceiling(8 * d.C / cPeak), 1, 8)];
                for (int k = 0; k < bw; k++) Draw.Cell(b, cx + k, baseY + 3, s, d.C <= 0 ? p.Faint : p.Info, bg);
            }
            // X labels: centred under the column, skipped when they would collide.
            var text = Draw.Clean(d.Label);
            int tw = Draw.Width(text), tx = cx + bw / 2 - tw / 2;
            tx = Math.Clamp(tx, x + axis, x + width - tw);
            if (tw > 0 && tx > lastLabelEnd + 2)
            {
                Draw.Text(b, tx, baseY + 2, text, p.Muted, bg);
                lastLabelEnd = tx + tw - 1;
            }
        }
        if (cName != null) Draw.TextRight(b, x + axis - 2, baseY + 3, Draw.Clip(cName, axis - 2, false), p.Info, bg);
    }
}

/// <summary>GitHub-style calendar: one column per week, Monday at the top.</summary>
internal sealed class Heatmap(IReadOnlyList<(DateTime Date, double Value)> days, string less, string more, string? caption = null) : Block
{
    public override int Measure(int width) => 9;
    public override void Render(ConsoleBuffer b, Palette p, Color bg, int x, int y, int width, int height)
    {
        if (days.Count == 0) return;
        var first = days[0].Date.Date; var last = days[^1].Date.Date;
        var start = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
        int weeks = (int)((last - start).TotalDays / 7) + 1;
        const int labels = 4;
        int cell = (width - labels) / Math.Max(1, weeks) >= 2 ? 2 : 1;
        int fit = Math.Max(1, (width - labels) / cell);
        if (weeks > fit) { start = start.AddDays(7 * (weeks - fit)); weeks = fit; }
        var values = days.ToDictionary(d => d.Date.Date, d => d.Value);
        double peak = Math.Max(1, days.Max(d => d.Value));
        string[] names = [.. Enumerable.Range(0, 7).Select(i => System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[(i + 1) % 7])];
        for (int d = 0; d < 7; d += 2) Draw.Text(b, x, y + 1 + d, names[d], p.Faint, bg, labels - 1);
        int lastMonthEnd = int.MinValue;
        for (int w = 0; w < weeks; w++)
        {
            int cx = x + labels + w * cell;
            var monday = start.AddDays(7 * w);
            if ((w == 0 || monday.Month != monday.AddDays(-7).Month) && cx > lastMonthEnd)
                lastMonthEnd = cx + Draw.Text(b, cx, y, monday.ToString("MMM"), p.Muted, bg);
            for (int d = 0; d < 7; d++)
            {
                var day = monday.AddDays(d);
                if (day < first || day > last) continue;
                double v = values.GetValueOrDefault(day);
                double level = v <= 0 ? 0 : Math.Ceiling(4 * v / peak) / 4;
                Draw.Cell(b, cx, y + 1 + d, '■', p.Heat(level), bg);
            }
        }
        int ly = y + 8;
        int legend = Draw.Width(less) + Draw.Width(more) + 12;
        if (caption != null) Draw.Text(b, x + labels, ly, caption, p.Muted, bg, width - labels - legend - 1);
        int rx = x + width;
        rx -= Draw.Width(more); Draw.Text(b, rx, ly, more, p.Faint, bg);
        for (int level = 4; level >= 0; level--) { rx -= 2; Draw.Cell(b, rx, ly, '■', p.Heat(level / 4d), bg); }
        rx -= Draw.Width(less) + 1; Draw.Text(b, rx, ly, less, p.Faint, bg);
    }
}

/// <summary>Columns of text with an optional trailing proportional bar.</summary>
internal sealed class Table(string[] headers, bool[] right, IEnumerable<(string[] Cells, double Bar, Tone Tone)> rows, Tone barTone = Tone.Accent) : Block
{
    private readonly (string[] Cells, double Bar, Tone Tone)[] items = rows.ToArray();
    public override int Measure(int width) => items.Length + 2;
    public override void Render(ConsoleBuffer b, Palette p, Color bg, int x, int y, int width, int height)
    {
        int cols = headers.Length;
        var widths = Enumerable.Range(0, cols).Select(c => items.Select(r => Draw.Width(r.Cells[c])).Prepend(Draw.Width(headers[c])).Max()).ToArray();
        double peak = items.Select(r => r.Bar).DefaultIfEmpty(0).Max();
        void Row(int ry, string[] cells, Color fg, Tone tone, double? bar)
        {
            int cx = x;
            for (int c = 0; c < cols && cx < x + width; c++)
            {
                if (right[c]) Draw.TextRight(b, cx + widths[c], ry, cells[c], fg, bg, x + width - cx);
                else Draw.Text(b, cx, ry, cells[c], fg, bg, Math.Min(widths[c], x + width - cx));
                cx += widths[c] + 3;
            }
            if (bar is double value && peak > 0 && x + width - cx >= 4)
                Draw.Bar(b, cx, ry, x + width - cx, value / peak, p.Of(tone == Tone.Normal ? barTone : tone), p.Raised, bg, ' ');
        }
        Row(y, headers.Select(h => h.ToUpperInvariant()).ToArray(), p.Muted, Tone.Normal, null);
        Draw.HLine(b, x, y + 1, width, p.Border, bg);
        for (int i = 0; i < items.Length; i++)
            Row(y + 2 + i, items[i].Cells, items[i].Cells.Skip(1).All(c => c is "0" or "—" or "") ? p.Faint : p.Ink, items[i].Tone, items[i].Bar);
    }
}

/// <summary>A QR code drawn with half blocks, always dark-on-light so phone cameras can read it.</summary>
internal sealed class QrBlock(Net.Codecrete.QrCodeGenerator.QrCode qr) : Block
{
    private int Size => qr.Size + 4;
    public override int Measure(int width) => (Size + 1) / 2;
    public override void Render(ConsoleBuffer b, Palette p, Color bg, int x, int y, int width, int height)
    {
        int left = x + Math.Max(0, (width - Size) / 2);
        bool Dark(int cx, int cy) => cx >= 0 && cy >= 0 && cx < qr.Size && cy < qr.Size && qr.GetModule(cx, cy);
        for (int row = 0; row < (Size + 1) / 2; row++)
            for (int col = 0; col < Size; col++)
            {
                int mx = col - 2, my = row * 2 - 2;
                char c = (Dark(mx, my), Dark(mx, my + 1)) switch { (true, true) => '█', (true, false) => '▀', (false, true) => '▄', _ => ' ' };
                Draw.Cell(b, left + col, y + row, c, Color.Black, Color.White);
            }
    }
}

/// <summary>A large, letter-spaced code in a box.</summary>
internal sealed class BigCode(string code, string? caption = null, Tone captionTone = Tone.Muted) : Block
{
    public override int Measure(int width) => caption == null ? 3 : 4;
    public override void Render(ConsoleBuffer b, Palette p, Color bg, int x, int y, int width, int height)
    {
        var spaced = string.Join(" ", Draw.Clean(code).ToCharArray());
        int w = Math.Min(width, Draw.Width(spaced) + 8);
        int left = x + (width - w) / 2;
        Draw.Box(b, left, y, w, 3, p.Accent, p.Surface);
        Draw.TextCenter(b, left, y + 1, w, spaced, p.Accent, p.Surface);
        if (caption != null) Draw.TextCenter(b, x, y + 3, width, caption, p.Of(captionTone), bg);
    }
}

/// <summary>The current palette as a grid of swatches; it re-reads colours on every paint, so edits preview live.</summary>
internal sealed class Swatches(IEnumerable<(string Name, Func<Palette, Color> Get)> tokens) : Block
{
    private readonly (string Name, Func<Palette, Color> Get)[] items = tokens.ToArray();
    private const int Cell = 30;
    private int Cols(int width) => Math.Max(1, (width + 2) / (Cell + 2));
    public override int Measure(int width) => (int)Math.Ceiling(items.Length / (double)Cols(width));
    public override void Render(ConsoleBuffer b, Palette p, Color bg, int x, int y, int width, int height)
    {
        int cols = Cols(width), each = (width - (cols - 1) * 2) / cols;
        for (int i = 0; i < items.Length; i++)
        {
            int cx = x + (i % cols) * (each + 2), cy = y + i / cols;
            var color = items[i].Get(p);
            Draw.Fill(b, cx, cy, 3, 1, color);
            Draw.Text(b, cx + 4, cy, items[i].Name, p.Ink, bg, each - 13);
            Draw.TextRight(b, cx + each, cy, Palette.Hex(color), p.Faint, bg);
        }
    }
}
