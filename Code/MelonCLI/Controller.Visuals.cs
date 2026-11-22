using System.Globalization;
using Melon.Interface;
using Admin = Melon.Interface.IMelonAdministration;

namespace MelonCLI;

internal sealed partial class Controller
{
    private static readonly string[] VisualMappings = ["Normalized", "Mel", "Log10"];
    private static readonly string[] VisualCodecs = ["Brotli", "Zstd", "None"];

    private static string Megabytes(long bytes) => bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.0} GB" : $"{bytes / (double)(1L << 20):0.#} MB";

    internal static string VisualSummary(VisualFormat f) =>
        $"{f.Bands} × {f.Fps} fps · {f.QuantizeBits}-bit{(f.Stereo ? " · stereo" : "")}";

    private async Task VisualsSettings()
    {
        while (true)
        {
            var v = await Read<VisualsStatus>(nameof(Admin.GetVisuals));
            var s = v.Settings;
            var d = s.Defaults;
            var l = s.Limits;
            var page = new Screen(T("Visualizations"));
            page.Body.Add(new Cards([
                new Card(T("Status"), T(s.Enabled ? "On" : "Off"), Dot: s.Enabled ? Tone.Good : Tone.Faint),
                new Card(T("Cache"), $"{Megabytes(v.CacheBytes)} / {Megabytes(s.CacheMegabytes * 1024L * 1024L)}", $"{v.CacheFiles:N0} {T("files")}"),
                new Card(T("Analysing"), $"{v.Running:N0} / {s.MaxConcurrentJobs:N0}", T("tracks at once"), Dot: v.Running > 0 ? Tone.Info : null)], 22));
            page.Body.Add(new Panel(T("About"), new Para(T("Clients download pre-computed frequency frames, beat times and moments instead of analysing audio live, which saves battery. Files are made the first time a client asks and kept in the cache."), Tone.Muted)));

            page.Items.Add(Setting("Visualizations", T(s.Enabled ? "On" : "Off"),
                () => Write(nameof(Admin.SetVisualsEnabled), new { enabled = !s.Enabled })));

            page.Items.Add(Entry.Header(T("Default format")));
            page.Items.Add(Setting("Bands", d.Bands.ToString(), async () => await SetDefaults(d with { Bands = await AskInt("Bands", d.Bands, l.MinBands, l.MaxBands) })));
            page.Items.Add(Setting("Frame rate", $"{d.Fps} fps", async () => await SetDefaults(d with { Fps = await AskInt("Frame rate", d.Fps, l.MinFps, l.MaxFps) })));
            page.Items.Add(Setting("Bit depth", $"{d.QuantizeBits}-bit", async () => await SetDefaults(d with { QuantizeBits = await AskInt("Bit depth", d.QuantizeBits, l.MinQuantizeBits, l.MaxQuantizeBits) })));
            page.Items.Add(Setting("Fall rate", d.DecayPerSecond == 0 ? T("Off") : $"{d.DecayPerSecond:0.##} / s", async () =>
                await SetDefaults(d with { DecayPerSecond = await AskDouble("Fall rate", d.DecayPerSecond, 0, 100, "0 = off · 1.5–3 looks natural") })));
            if (l.AllowStereo || d.Stereo)
                page.Items.Add(Setting("Stereo", T(d.Stereo ? "On" : "Off"), () => SetDefaults(d with { Stereo = !d.Stereo })));
            page.Items.Add(Setting("Frequency mapping", T(d.Mapping), async () => await SetDefaults(d with { Mapping = await Choose("Frequency mapping", VisualMappings, current: d.Mapping) })));
            page.Items.Add(Setting("FFT size", d.FftResolution.ToString(), async () =>
                await SetDefaults(d with { FftResolution = int.Parse(await Choose("FFT size", PowersOfTwo(l.MinFftResolution, l.MaxFftResolution), false, d.FftResolution.ToString())) })));
            page.Items.Add(Setting("Level range", $"{d.DbFloor:0} dB + {d.DbRange:0} dB", async () =>
            {
                var floor = await AskDouble("Floor (dB)", d.DbFloor, -200, -1);
                var range = await AskDouble("Range (dB)", d.DbRange, 1, 200);
                await SetDefaults(d with { DbFloor = floor, DbRange = range });
            }));
            page.Items.Add(Setting("Frequency range", $"{d.FrequencyMin:0}–{d.FrequencyMax:0} Hz", async () =>
            {
                var min = await AskDouble("Lowest frequency (Hz)", d.FrequencyMin, 1, 20000);
                var max = await AskDouble("Highest frequency (Hz)", d.FrequencyMax, min + 1, 96000);
                await SetDefaults(d with { FrequencyMin = min, FrequencyMax = max });
            }));
            page.Items.Add(Setting("Smoothing", d.Smoothness == 0 ? T("Off") : $"± {d.Smoothness}", async () => await SetDefaults(d with { Smoothness = await AskInt("Smoothing", d.Smoothness, 0, 16) })));
            page.Items.Add(Setting("Compression", d.Codec, async () => await SetDefaults(d with { Codec = await Choose("Compression", VisualCodecs, false, d.Codec) })));

            page.Items.Add(Entry.Header(T("Client limits")));
            page.Items.Add(Setting("Bands", $"{l.MinBands}–{l.MaxBands}", async () =>
            {
                var (min, max) = await AskRange("Bands", l.MinBands, l.MaxBands, 1, 1024);
                await SetLimits(l with { MinBands = min, MaxBands = max });
            }));
            page.Items.Add(Setting("Frame rate", $"{l.MinFps}–{l.MaxFps} fps", async () =>
            {
                var (min, max) = await AskRange("Frame rate", l.MinFps, l.MaxFps, 1, 480);
                await SetLimits(l with { MinFps = min, MaxFps = max });
            }));
            page.Items.Add(Setting("Bit depth", $"{l.MinQuantizeBits}–{l.MaxQuantizeBits}", async () =>
            {
                var (min, max) = await AskRange("Bit depth", l.MinQuantizeBits, l.MaxQuantizeBits, 4, 16);
                await SetLimits(l with { MinQuantizeBits = min, MaxQuantizeBits = max });
            }));
            page.Items.Add(Setting("FFT size", $"{l.MinFftResolution}–{l.MaxFftResolution}", async () =>
            {
                var sizes = PowersOfTwo(256, 32768);
                var min = int.Parse(await Choose("Smallest FFT size", sizes, false, l.MinFftResolution.ToString()));
                var max = int.Parse(await Choose("Largest FFT size", sizes.Where(x => int.Parse(x) >= min), false, l.MaxFftResolution.ToString()));
                await SetLimits(l with { MinFftResolution = min, MaxFftResolution = max });
            }));
            page.Items.Add(Setting("Allow stereo", T(l.AllowStereo ? "On" : "Off"), () => SetLimits(l with { AllowStereo = !l.AllowStereo })));

            page.Items.Add(Entry.Header(T("Cache")));
            page.Items.Add(Setting("Cache size", Megabytes(s.CacheMegabytes * 1024L * 1024L), async () =>
                await Write(nameof(Admin.SetVisualsCache), new { megabytes = await AskInt("Cache size (MB)", s.CacheMegabytes, 64, 102_400), maxConcurrentJobs = s.MaxConcurrentJobs })));
            page.Items.Add(Setting("Tracks analysed at once", s.MaxConcurrentJobs.ToString(), async () =>
                await Write(nameof(Admin.SetVisualsCache), new { megabytes = s.CacheMegabytes, maxConcurrentJobs = await AskInt("Tracks analysed at once", s.MaxConcurrentJobs, 1, 16) })));
            if (v.CacheFiles > 0)
                page.Items.Add(Action("Clear cache", async () =>
                {
                    await Confirm("Clear cache", T("Visualizations are made again the next time clients ask for them."));
                    var freed = await Read<long>(nameof(Admin.ClearVisualsCache));
                    Say($"{T("Cache cleared.")} {Megabytes(freed)}");
                }, Tone.Warn));

            page.Notice = notice; page.NoticeTone = noticeTone;
            var row = await ui.Menu(page);
            if (row == null) return;
            notice = "";
            try { await row.Run!(); } catch (Back) { }
        }
    }

    private Task SetDefaults(VisualFormat format) => Write(nameof(Admin.SetVisualDefaults), new { format });
    private Task SetLimits(VisualLimits limits) => Write(nameof(Admin.SetVisualLimits), new { limits });

    private static IEnumerable<string> PowersOfTwo(int min, int max)
    {
        for (int v = min; v <= max; v *= 2) yield return v.ToString();
    }

    private async Task<int> AskInt(string key, int current, int min, int max)
    {
        var text = await Ask(key, current.ToString(), help: $"{min}–{max}");
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value) || value < min || value > max)
            throw new ArgumentException($"{T("Enter a number from")} {min} {T("to")} {max}.");
        return value;
    }

    private async Task<double> AskDouble(string key, double current, double min, double max, string? help = null)
    {
        var text = await Ask(key, current.ToString(CultureInfo.CurrentCulture), help: help != null ? T(help) : $"{min}–{max}");
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) || value < min || value > max)
            throw new ArgumentException($"{T("Enter a number from")} {min} {T("to")} {max}.");
        return value;
    }

    private async Task<(int Min, int Max)> AskRange(string key, int currentMin, int currentMax, int floor, int ceiling)
    {
        var min = await AskInt($"{key} · {T("minimum")}", currentMin, floor, ceiling);
        var max = await AskInt($"{key} · {T("maximum")}", Math.Max(min, currentMax), min, ceiling);
        return (min, max);
    }
}
