using System.Drawing;
using System.Globalization;
using System.Text;
using MelonUI.Base;

namespace MelonCLI;

/// <summary>Cell-level drawing helpers. All text is sanitized so server-, plugin-, and file-supplied
/// strings can never emit terminal control sequences.</summary>
internal static class Draw
{
    public const string Eighths = " ▁▂▃▄▅▆▇█";
    private const string Lefts = " ▏▎▍▌▋▊▉█";

    public static string Clean(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var output = new StringBuilder(value.Length);
        foreach (char c in value) output.Append(char.IsControl(c) ? ' ' : c);
        return output.ToString();
    }

    public static int Width(string? text) => string.IsNullOrEmpty(text) ? 0 : ConsoleBuffer.GetStringWidth(Clean(text));

    /// <summary>Clips by display width, ending with an ellipsis when something was cut.</summary>
    public static string Clip(string? text, int width, bool ellipsis = true)
    {
        text = Clean(text);
        if (width <= 0) return "";
        if (Width(text) <= width) return text;
        var output = new StringBuilder();
        int used = 0, room = ellipsis ? width - 1 : width;
        var parts = StringInfo.GetTextElementEnumerator(text);
        while (parts.MoveNext())
        {
            string part = parts.GetTextElement();
            int size = Math.Max(1, ConsoleBuffer.GetStringWidth(part));
            if (used + size > room) break;
            output.Append(part); used += size;
        }
        return ellipsis ? output.Append('…').ToString() : output.ToString();
    }

    public static string PadRight(string text, int width) => text + new string(' ', Math.Max(0, width - Width(text)));

    /// <summary>Writes clipped text and returns the number of cells used.</summary>
    public static int Text(ConsoleBuffer b, int x, int y, string? text, Color fg, Color bg, int max = int.MaxValue)
    {
        if (y < 0 || y >= b.Height || x >= b.Width) return 0;
        var clipped = Clip(text, Math.Min(max, b.Width - x));
        b.WriteString(x, y, clipped, fg, bg);
        return Width(clipped);
    }

    public static void TextRight(ConsoleBuffer b, int right, int y, string? text, Color fg, Color bg, int max = int.MaxValue)
    {
        var clipped = Clip(text, max);
        Text(b, right - Width(clipped), y, clipped, fg, bg);
    }

    public static void TextCenter(ConsoleBuffer b, int x, int y, int width, string? text, Color fg, Color bg)
    {
        var clipped = Clip(text, width);
        Text(b, x + Math.Max(0, (width - Width(clipped)) / 2), y, clipped, fg, bg);
    }

    public static void Cell(ConsoleBuffer b, int x, int y, char c, Color fg, Color bg)
    {
        if (x >= 0 && y >= 0 && x < b.Width && y < b.Height) b[y, x] = new ConsolePixel(c, fg, bg, false);
    }

    public static void Fill(ConsoleBuffer b, int x, int y, int w, int h, Color bg, char c = ' ', Color? fg = null)
    {
        for (int row = Math.Max(0, y); row < Math.Min(b.Height, y + h); row++)
            for (int col = Math.Max(0, x); col < Math.Min(b.Width, x + w); col++)
                b[row, col] = new ConsolePixel(c, fg ?? bg, bg, false);
    }

    public static void HLine(ConsoleBuffer b, int x, int y, int w, Color fg, Color bg, char c = '─')
    {
        for (int i = 0; i < w; i++) Cell(b, x + i, y, c, fg, bg);
    }

    /// <summary>Rounded box with an optional title set into the top edge and optional text on the right of it.</summary>
    public static void Box(ConsoleBuffer b, int x, int y, int w, int h, Color border, Color bg, string? title = null, Color? titleColor = null, string? right = null, Color? rightColor = null)
    {
        if (w < 2 || h < 2) return;
        Fill(b, x + 1, y + 1, w - 2, h - 2, bg);
        Cell(b, x, y, '╭', border, bg); Cell(b, x + w - 1, y, '╮', border, bg);
        Cell(b, x, y + h - 1, '╰', border, bg); Cell(b, x + w - 1, y + h - 1, '╯', border, bg);
        HLine(b, x + 1, y, w - 2, border, bg); HLine(b, x + 1, y + h - 1, w - 2, border, bg);
        for (int row = y + 1; row < y + h - 1; row++) { Cell(b, x, row, '│', border, bg); Cell(b, x + w - 1, row, '│', border, bg); }
        int used = 0;
        if (!string.IsNullOrEmpty(title) && w > 6)
            used = Text(b, x + 2, y, " " + title + " ", titleColor ?? border, bg, w - 4);
        if (!string.IsNullOrEmpty(right) && w - 6 - used > 4)
        {
            var text = " " + Clip(right, w - 8 - used) + " ";
            Text(b, x + w - 2 - Width(text), y, text, rightColor ?? border, bg);
        }
    }

    /// <summary>A horizontal bar with eighth-cell resolution.</summary>
    public static void Bar(ConsoleBuffer b, int x, int y, int width, double fraction, Color fg, Color track, Color bg, char trackChar = ' ')
    {
        if (width <= 0) return;
        double cells = Math.Clamp(fraction, 0, 1) * width;
        int full = (int)Math.Floor(cells);
        int part = (int)Math.Round((cells - full) * 8);
        for (int i = 0; i < width; i++)
        {
            if (i < full) Cell(b, x + i, y, '█', fg, bg);
            else if (i == full && part > 0) Cell(b, x + i, y, Lefts[part], fg, trackChar == ' ' ? bg : track);
            else Cell(b, x + i, y, trackChar, track, bg);
        }
    }

    public static IEnumerable<string> Wrap(string? text, int width)
    {
        width = Math.Max(1, width);
        foreach (var raw in Clean((text ?? "").Replace("\r", "").Replace('\n', '\u0001')).Split('\u0001'))
        {
            if (raw.Length == 0) { yield return ""; continue; }
            var line = new StringBuilder();
            int used = 0;
            foreach (var word in raw.Split(' '))
            {
                int size = Width(word);
                if (used > 0 && used + 1 + size > width) { yield return line.ToString(); line.Clear(); used = 0; }
                if (used > 0) { line.Append(' '); used++; }
                var rest = word;
                while (Width(rest) > width)
                {
                    var head = Clip(rest, width - used, false);
                    if (head.Length == 0 && used > 0) { yield return line.ToString(); line.Clear(); used = 0; continue; }
                    if (head.Length == 0) head = StringInfo.GetNextTextElement(rest);
                    line.Append(head); yield return line.ToString(); line.Clear(); used = 0;
                    rest = rest[head.Length..];
                }
                line.Append(rest); used += Width(rest);
            }
            yield return line.ToString();
        }
    }

    public static string Spark(IReadOnlyList<double> values)
    {
        double peak = values.Count == 0 ? 0 : values.Max();
        return new string(values.Select(v => v <= 0 || peak <= 0 ? '▁' : Eighths[Math.Clamp((int)Math.Ceiling(8 * v / peak), 1, 8)]).ToArray());
    }

    /// <summary>Copies a source buffer into the target, skipping rows above <paramref name="skip"/>.</summary>
    public static void Blit(ConsoleBuffer target, ConsoleBuffer source, int x, int y, int skip, int rows)
    {
        for (int row = 0; row < rows; row++)
        {
            int sy = row + skip, ty = y + row;
            if (sy < 0 || sy >= source.Height || ty < 0 || ty >= target.Height) continue;
            for (int col = 0; col < source.Width && x + col < target.Width; col++)
                if (x + col >= 0) target[ty, x + col] = source[sy, col];
        }
    }
}

internal static class Fmt
{
    public static string Duration(double milliseconds, bool seconds = false)
    {
        var time = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        if (time.TotalHours >= 100) return $"{(long)time.TotalHours:N0}h";
        if (time.TotalHours >= 1) return $"{(int)time.TotalHours}h {time.Minutes:00}m";
        if (time.TotalMinutes >= 1) return seconds ? $"{time.Minutes}m {time.Seconds:00}s" : $"{time.Minutes}m";
        return $"{time.Seconds}s";
    }

    public static string Compact(double value) => value switch
    {
        >= 1_000_000 => $"{value / 1_000_000:0.#}M",
        >= 10_000 => $"{value / 1_000:0}k",
        >= 1_000 => $"{value / 1_000:0.#}k",
        _ => $"{value:0}"
    };

    public static string Percent(double fraction) => fraction >= 0.995 || fraction == 0 ? $"{fraction:P0}" : $"{fraction:P1}";
}
