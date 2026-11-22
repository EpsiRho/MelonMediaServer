using Melon.Interface;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MelonGUI;

public sealed partial class MainWindow
{
    private static string Megabytes(long bytes) => bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.0} GB" : $"{bytes / (double)(1L << 20):0.#} MB";

    private async Task VisualsSection(StackPanel page)
    {
        var v = await Read<VisualsStatus>(nameof(IMelonAdministration.GetVisuals));
        var s = v.Settings;
        var d = s.Defaults;
        var l = s.Limits;
        Section(page, "Visualizations");

        var enabled = new ToggleSwitch { IsOn = s.Enabled, OnContent = T("On"), OffContent = T("Off") };
        enabled.Toggled += async (_, _) => await Act(() => Command(nameof(IMelonAdministration.SetVisualsEnabled), new { enabled = enabled.IsOn }));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(enabled, T("Visualizations"));
        page.Children.Add(Card(Row(Stack(Text(T("Visualizations")),
            Text(T("Clients download pre-computed frequency frames, beat times and moments instead of analysing audio live, which saves battery. Files are made the first time a client asks and kept in the cache."), "CaptionTextBlockStyle")),
            enabled)));

        string format = $"{d.Bands} {T("bands")} · {d.Fps} fps · {d.QuantizeBits}-bit · {T(d.Mapping)}"
            + (d.Stereo ? $" · {T("Stereo")}" : "") + (d.DecayPerSecond > 0 ? $" · {T("Fall rate")} {d.DecayPerSecond:0.##}/s" : "");
        string limits = $"{T("Bands")} {l.MinBands}–{l.MaxBands} · {l.MinFps}–{l.MaxFps} fps · {l.MinQuantizeBits}–{l.MaxQuantizeBits}-bit · FFT {l.MinFftResolution}–{l.MaxFftResolution}"
            + (l.AllowStereo ? "" : $" · {T("No stereo")}");
        page.Children.Add(Card(Stack(
            Row(Stack(Text(T("Default format")), Text(format, "CaptionTextBlockStyle")), ButtonFor("Edit", () => EditVisualDefaults(d, l))),
            Row(Stack(Text(T("Client limits")), Text(T("Clients may ask for any format in this range, such as fewer bands for a watch or more for a TV."), "CaptionTextBlockStyle"),
                Text(limits, "CaptionTextBlockStyle")), ButtonFor("Edit", () => EditVisualLimits(l))))));

        var cacheButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        cacheButtons.Children.Add(ButtonFor("Edit", () => EditVisualCache(s)));
        if (v.CacheFiles > 0) cacheButtons.Children.Add(ButtonFor("Clear cache", ClearVisualCache));
        page.Children.Add(Card(Row(Stack(Text(T("Cache")),
            Text($"{Megabytes(v.CacheBytes)} / {Megabytes(s.CacheMegabytes * 1024L * 1024L)} · {v.CacheFiles:N0} {T("files")} · {T("Tracks analysed at once")}: {s.MaxConcurrentJobs}"
                + (v.Running > 0 ? $" · {T("Analysing")} {v.Running}" : ""), "CaptionTextBlockStyle")), cacheButtons)));
    }

    private NumberBox Number(string header, double value, double min, double max, double step = 1) => new()
    {
        Header = T(header), Value = value, Minimum = min, Maximum = max, SmallChange = step,
        SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    private ComboBox Pick(string header, IEnumerable<string> values, string current, bool translate = true)
    {
        var box = new ComboBox { Header = T(header), HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var value in values) box.Items.Add(new ComboBoxItem { Content = translate ? T(value) : value, Tag = value });
        box.SelectedIndex = Math.Max(0, values.ToList().IndexOf(current));
        return box;
    }

    private static IEnumerable<string> FftSizes(int min, int max)
    {
        for (int v = min; v <= max; v *= 2) yield return v.ToString();
    }

    private static Grid Pair(FrameworkElement a, FrameworkElement b)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(b, 1);
        grid.Children.Add(a); grid.Children.Add(b);
        return grid;
    }

    private static int Int(NumberBox box) => (int)Math.Round(double.IsNaN(box.Value) ? box.Minimum : box.Value);
    private static double Dbl(NumberBox box) => double.IsNaN(box.Value) ? box.Minimum : box.Value;

    private async Task EditVisualDefaults(VisualFormat d, VisualLimits l)
    {
        var bands = Number("Bands", d.Bands, l.MinBands, l.MaxBands);
        var fps = Number("Frame rate", d.Fps, l.MinFps, l.MaxFps);
        var bits = Number("Bit depth", d.QuantizeBits, l.MinQuantizeBits, l.MaxQuantizeBits);
        var decay = Number("Fall rate", d.DecayPerSecond, 0, 100, 0.5);
        var mapping = Pick("Frequency mapping", ["Normalized", "Mel", "Log10"], d.Mapping);
        var fft = Pick("FFT size", FftSizes(l.MinFftResolution, l.MaxFftResolution).ToList(), d.FftResolution.ToString(), translate: false);
        var floor = Number("Floor (dB)", d.DbFloor, -200, -1);
        var range = Number("Range (dB)", d.DbRange, 1, 200);
        var fmin = Number("Lowest frequency (Hz)", d.FrequencyMin, 1, 20000);
        var fmax = Number("Highest frequency (Hz)", d.FrequencyMax, 2, 96000);
        var smooth = Number("Smoothing", d.Smoothness, 0, 16);
        var codec = Pick("Compression", ["Brotli", "Zstd", "None"], d.Codec, translate: false);
        var stereo = new ToggleSwitch { Header = T("Stereo"), IsOn = d.Stereo && l.AllowStereo, IsEnabled = l.AllowStereo, OnContent = T("On"), OffContent = T("Off") };
        var content = Stack(Pair(bands, fps), Pair(bits, decay), Text(T("0 = off · 1.5–3 looks natural"), "CaptionTextBlockStyle"),
            Pair(mapping, fft), Pair(floor, range), Pair(fmin, fmax), Pair(smooth, codec), stereo);
        var scroll = new ScrollViewer { Content = content, MaxHeight = 520 };
        if (!await Dialog("Default format", scroll, "Save")) return;
        var format = d with
        {
            Bands = Int(bands), Fps = Int(fps), QuantizeBits = Int(bits), DecayPerSecond = Dbl(decay),
            Mapping = (string)((ComboBoxItem)mapping.SelectedItem).Tag, FftResolution = int.Parse((string)((ComboBoxItem)fft.SelectedItem).Tag),
            DbFloor = Dbl(floor), DbRange = Dbl(range), FrequencyMin = Dbl(fmin), FrequencyMax = Dbl(fmax), Smoothness = Int(smooth),
            Codec = (string)((ComboBoxItem)codec.SelectedItem).Tag, Stereo = stereo.IsOn,
        };
        await Command(nameof(IMelonAdministration.SetVisualDefaults), new { format });
    }

    private async Task EditVisualLimits(VisualLimits l)
    {
        var minBands = Number("Fewest bands", l.MinBands, 1, 1024);
        var maxBands = Number("Most bands", l.MaxBands, 1, 1024);
        var minFps = Number("Lowest frame rate", l.MinFps, 1, 480);
        var maxFps = Number("Highest frame rate", l.MaxFps, 1, 480);
        var minBits = Number("Lowest bit depth", l.MinQuantizeBits, 4, 16);
        var maxBits = Number("Highest bit depth", l.MaxQuantizeBits, 4, 16);
        var sizes = FftSizes(256, 32768).ToList();
        var minFft = Pick("Smallest FFT size", sizes, l.MinFftResolution.ToString(), translate: false);
        var maxFft = Pick("Largest FFT size", sizes, l.MaxFftResolution.ToString(), translate: false);
        var stereo = new ToggleSwitch { Header = T("Allow stereo"), IsOn = l.AllowStereo, OnContent = T("On"), OffContent = T("Off") };
        var content = Stack(Text(T("Clients may ask for any format in this range, such as fewer bands for a watch or more for a TV."), "CaptionTextBlockStyle"),
            Pair(minBands, maxBands), Pair(minFps, maxFps), Pair(minBits, maxBits), Pair(minFft, maxFft), stereo);
        if (!await Dialog("Client limits", content, "Save")) return;
        var limits = new VisualLimits(Int(minBands), Int(maxBands), Int(minFps), Int(maxFps),
            int.Parse((string)((ComboBoxItem)minFft.SelectedItem).Tag), int.Parse((string)((ComboBoxItem)maxFft.SelectedItem).Tag),
            Int(minBits), Int(maxBits), stereo.IsOn);
        await Command(nameof(IMelonAdministration.SetVisualLimits), new { limits });
    }

    private async Task EditVisualCache(VisualsSettings s)
    {
        var size = Number("Cache size (MB)", s.CacheMegabytes, 64, 102_400, 256);
        var jobs = Number("Tracks analysed at once", s.MaxConcurrentJobs, 1, 16);
        var content = Stack(size, jobs, Text(T("Each track being analysed uses several CPU cores for a second or two. Lower this on small servers."), "CaptionTextBlockStyle"));
        if (!await Dialog("Cache", content, "Save")) return;
        await Command(nameof(IMelonAdministration.SetVisualsCache), new { megabytes = Int(size), maxConcurrentJobs = Int(jobs) });
    }

    private async Task ClearVisualCache()
    {
        if (!await Dialog("Clear cache", Text(T("Visualizations are made again the next time clients ask for them.")), "Clear cache", destructive: true)) return;
        var freed = await Read<long>(nameof(IMelonAdministration.ClearVisualsCache));
        Notice($"{T("Cache cleared.")} {Megabytes(freed)}", InfoBarSeverity.Success);
    }
}
