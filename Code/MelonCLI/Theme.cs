using System.Drawing;
using System.Globalization;
using System.Text.Json;

namespace MelonCLI;

internal enum Tone { Normal, Muted, Faint, Accent, Good, Warn, Danger, Info, Flesh }

/// <summary>Screen colours. Data series use Accent (plays), Flesh (skips) and Info (time).</summary>
internal sealed record Palette
{
    public Color Bg { get; init; }
    public Color Surface { get; init; }
    public Color Raised { get; init; }
    public Color Border { get; init; }
    public Color Ink { get; init; }
    public Color Muted { get; init; }
    public Color Faint { get; init; }
    public Color Accent { get; init; }
    public Color AccentInk { get; init; }
    public Color Flesh { get; init; }
    public Color Good { get; init; }
    public Color Warn { get; init; }
    public Color Danger { get; init; }
    public Color Info { get; init; }

    public Color Of(Tone tone) => tone switch
    {
        Tone.Muted => Muted, Tone.Faint => Faint, Tone.Accent => Accent, Tone.Good => Good, Tone.Warn => Warn,
        Tone.Danger => Danger, Tone.Info => Info, Tone.Flesh => Flesh, _ => Ink
    };

    /// <summary>Heat ramp from the raised surface to the accent, used by the calendar.</summary>
    public Color Heat(double t) => t <= 0 ? Raised : Mix(Mix(Raised, Accent, 0.25), Accent, Math.Clamp(t, 0, 1));

    public static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb((int)Math.Round(a.R + (b.R - a.R) * t), (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t));
    }

    private static Color C(int rgb) => Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);

    /// <summary>Editable colour slots, in the order the theme editor lists them.</summary>
    public static readonly (string Name, Func<Palette, Color> Get, Func<Palette, Color, Palette> Set)[] Tokens =
    [
        ("Background", p => p.Bg, (p, c) => p with { Bg = c }),
        ("Surface", p => p.Surface, (p, c) => p with { Surface = c }),
        ("Raised surface", p => p.Raised, (p, c) => p with { Raised = c }),
        ("Border", p => p.Border, (p, c) => p with { Border = c }),
        ("Text", p => p.Ink, (p, c) => p with { Ink = c }),
        ("Muted text", p => p.Muted, (p, c) => p with { Muted = c }),
        ("Faint text", p => p.Faint, (p, c) => p with { Faint = c }),
        ("Accent", p => p.Accent, (p, c) => p with { Accent = c }),
        ("Text on accent", p => p.AccentInk, (p, c) => p with { AccentInk = c }),
        ("Secondary", p => p.Flesh, (p, c) => p with { Flesh = c }),
        ("Positive", p => p.Good, (p, c) => p with { Good = c }),
        ("Warning", p => p.Warn, (p, c) => p with { Warn = c }),
        ("Danger", p => p.Danger, (p, c) => p with { Danger = c }),
        ("Info", p => p.Info, (p, c) => p with { Info = c }),
    ];

    public static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static readonly Palette MelonDark = new()
    {
        Bg = C(0x0D1512), Surface = C(0x15211C), Raised = C(0x1F2F28), Border = C(0x32483D), Ink = C(0xE2EEE5),
        Muted = C(0x8AA395), Faint = C(0x546A5F), Accent = C(0x84DE9A), AccentInk = C(0x0A1C12), Flesh = C(0xFF7A8C),
        Good = C(0x84DE9A), Warn = C(0xF0C45C), Danger = C(0xFF6E6E), Info = C(0x7CB6FF)
    };

    public static readonly Palette MelonLight = new()
    {
        Bg = C(0xF6FAF5), Surface = C(0xEAF2E9), Raised = C(0xDDE9DC), Border = C(0xBACEBE), Ink = C(0x16261D),
        Muted = C(0x546B5E), Faint = C(0x92A79B), Accent = C(0x1A7846), AccentInk = C(0xF6FAF5), Flesh = C(0xD23256),
        Good = C(0x1A7846), Warn = C(0xA06A00), Danger = C(0xC02626), Info = C(0x1E62C8)
    };

    public static readonly Palette Midnight = new()
    {
        Bg = C(0x0E1220), Surface = C(0x161B2E), Raised = C(0x20273F), Border = C(0x343E5E), Ink = C(0xE4E8F5),
        Muted = C(0x8F99B8), Faint = C(0x56607E), Accent = C(0x8AB4FF), AccentInk = C(0x0B1022), Flesh = C(0xF59AC8),
        Good = C(0x7EDBA6), Warn = C(0xF2C46D), Danger = C(0xFF7A85), Info = C(0x7FD8E8)
    };

    public static readonly Palette Ember = new()
    {
        Bg = C(0x1A1311), Surface = C(0x241A17), Raised = C(0x322420), Border = C(0x4D3830), Ink = C(0xF3E6DD),
        Muted = C(0xB39A8B), Faint = C(0x725C50), Accent = C(0xFFA764), AccentInk = C(0x1E120A), Flesh = C(0xE86A8E),
        Good = C(0x9FD88A), Warn = C(0xF5D06A), Danger = C(0xFF6B5E), Info = C(0x8CC4E8)
    };

    public static readonly Palette Mono = new()
    {
        Bg = C(0x111111), Surface = C(0x1B1B1B), Raised = C(0x2A2A2A), Border = C(0x444444), Ink = C(0xF2F2F2),
        Muted = C(0xA6A6A6), Faint = C(0x6B6B6B), Accent = C(0xFFFFFF), AccentInk = C(0x111111), Flesh = C(0x9C9C9C),
        Good = C(0xFFFFFF), Warn = C(0xD6D6D6), Danger = C(0xFF8A8A), Info = C(0xBDBDBD)
    };

    public static readonly (string Name, Palette Palette)[] Presets =
        [("Melon Dark", MelonDark), ("Melon Light", MelonLight), ("Midnight", Midnight), ("Ember", Ember), ("Mono", Mono)];
}

/// <summary>The saved controller theme: a preset name, "Terminal default", or "Custom" with its colours.</summary>
internal sealed class ThemeSettings
{
    public const string Auto = "Terminal default", Custom = "Custom";
    public string Name { get; set; } = Auto;
    public string Base { get; set; } = "Melon Dark";
    public Dictionary<string, string> Colors { get; set; } = [];

    public Palette Resolve()
    {
        if (Name == Auto)
            return Console.BackgroundColor is ConsoleColor.White or ConsoleColor.Gray or ConsoleColor.Yellow ? Palette.MelonLight : Palette.MelonDark;
        if (Name != Custom) return Preset(Name);
        var palette = Preset(Base);
        foreach (var (name, _, set) in Palette.Tokens)
            if (Colors.TryGetValue(name, out var hex) && TryParse(hex, out var color)) palette = set(palette, color);
        return palette;
    }

    public void Store(Palette palette)
    {
        Colors = Palette.Tokens.ToDictionary(t => t.Name, t => Palette.Hex(t.Get(palette)));
    }

    public static Palette Preset(string name) => Palette.Presets.FirstOrDefault(p => p.Name == name).Palette ?? Palette.MelonDark;

    public static bool TryParse(string? text, out Color color)
    {
        color = default;
        var digits = (text ?? "").Trim().TrimStart('#');
        if (digits.Length == 8) digits = digits[2..];
        if (digits.Length != 6 || !int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)) return false;
        color = Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        return true;
    }

    public static ThemeSettings Load(string file)
    {
        try
        {
            if (!File.Exists(file)) return new();
            var text = File.ReadAllText(file).Trim();
            if (text.StartsWith('{')) return JsonSerializer.Deserialize<ThemeSettings>(text) ?? new();
            // Older versions stored only a name.
            return new() { Name = text switch { "Light" => "Melon Light", "Dark" => "Melon Dark", _ => Auto } };
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }

    public void Save(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
