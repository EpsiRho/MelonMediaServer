using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FFTVIS;
using Melon.Interface;
using Melon.Models;
using NAudio.Wave;

namespace Melon.LocalClasses;

/// <summary>Saved visualization settings.</summary>
internal sealed class VisualsConfig
{
    public bool Enabled { get; set; } = true;
    public VisualFormat Defaults { get; set; } = new();
    public VisualLimits Limits { get; set; } = new();
    public int CacheMegabytes { get; set; } = 2048;
    public int MaxConcurrentJobs { get; set; } = 2;
}

/// <summary>
/// Generates visualization data on demand and keeps it in a size-capped disk cache: FFTVIS frames in whatever format a
/// client asks for (within the limits), and beat times and moments from Nova. Concurrent requests for the same file
/// share one job; a semaphore caps how many tracks are analysed at once so playback and transcodes keep their CPU.
/// </summary>
public sealed class VisualsService
{
    public static VisualsService Instance { get; } = new();
    private const string ConfigFile = "Visuals";
    public const int MaxPrefetch = 8;

    // Hard bounds on what an administrator can allow; the format and encoder support more, but nothing a client needs.
    private const int HardMaxBands = 1024, HardMaxFps = 480, HardMinFft = 256, HardMaxFft = 32768;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly object gate = new();
    private readonly ConcurrentDictionary<string, Lazy<Task>> inflight = new();
    private VisualsConfig config = new();
    private SemaphoreSlim jobs = new(2);
    private int running;
    private int prefetchQueued;

    private VisualsService() { }

    public event Action<string>? Changed;

    internal static string CacheDirectory => Path.Combine(StateManager.melonPath, "Cache", "Visuals");

    // ── Settings ─────────────────────────────────────────────────────────────

    public void LoadConfig()
    {
        lock (gate)
        {
            VisualsConfig? loaded = null;
            try { loaded = Storage.LoadConfigFile<VisualsConfig>(ConfigFile, null, out _); }
            catch (Exception ex) { Serilog.Log.Warning(ex, "Visualization settings could not be read; using defaults."); }
            config = loaded ?? new VisualsConfig();
            config.Defaults ??= new();
            config.Limits ??= new();
            try
            {
                config.Limits = ValidateLimits(config.Limits);
                config.Defaults = Clamp(config.Defaults, config.Limits);
                config.CacheMegabytes = Math.Clamp(config.CacheMegabytes, 64, 102_400);
                config.MaxConcurrentJobs = Math.Clamp(config.MaxConcurrentJobs, 1, 16);
            }
            catch (ArgumentException ex)
            {
                Serilog.Log.Warning(ex, "Visualization settings were invalid; using defaults.");
                config = new VisualsConfig();
            }
            jobs = new SemaphoreSlim(config.MaxConcurrentJobs);
            if (loaded == null) Save();
        }
    }

    private void Save() => Storage.SaveConfigFile(ConfigFile, config, null);

    public VisualsSettings GetSettings()
    {
        lock (gate) return new VisualsSettings(config.Enabled, config.Defaults, config.Limits, config.CacheMegabytes, config.MaxConcurrentJobs);
    }

    public VisualsStatus GetStatus()
    {
        long bytes = 0;
        int files = 0;
        try
        {
            if (Directory.Exists(CacheDirectory))
                foreach (var f in new DirectoryInfo(CacheDirectory).EnumerateFiles())
                {
                    if (f.Name.EndsWith(".part", StringComparison.Ordinal)) continue;
                    bytes += f.Length;
                    files++;
                }
        }
        catch (IOException) { }
        return new VisualsStatus(GetSettings(), bytes, files, Volatile.Read(ref running));
    }

    public void SetEnabled(bool enabled) => Change(() => config.Enabled = enabled, enabled ? "Visualizations enabled." : "Visualizations disabled.");

    public void SetDefaults(VisualFormat format)
    {
        var checkedFormat = Validate(format);
        Change(() =>
        {
            if (!Within(checkedFormat, config.Limits))
                throw new ArgumentException("The default format must be within the client limits.");
            config.Defaults = checkedFormat;
        }, "Visualization defaults changed.");
    }

    public void SetLimits(VisualLimits limits)
    {
        var checkedLimits = ValidateLimits(limits);
        Change(() =>
        {
            config.Limits = checkedLimits;
            config.Defaults = Clamp(config.Defaults, checkedLimits);
        }, "Visualization limits changed.");
    }

    public void SetCache(int megabytes, int maxConcurrentJobs)
    {
        if (megabytes is < 64 or > 102_400) throw new ArgumentException("Cache size must be 64–102,400 MB.");
        if (maxConcurrentJobs is < 1 or > 16) throw new ArgumentException("Concurrent jobs must be 1–16.");
        Change(() =>
        {
            config.CacheMegabytes = megabytes;
            if (config.MaxConcurrentJobs != maxConcurrentJobs) jobs = new SemaphoreSlim(maxConcurrentJobs); // running jobs finish on the old one
            config.MaxConcurrentJobs = maxConcurrentJobs;
        }, "Visualization cache settings changed.");
        _ = Task.Run(() => Trim());
    }

    public long ClearCache()
    {
        long freed = 0;
        if (!Directory.Exists(CacheDirectory)) return 0;
        foreach (var f in new DirectoryInfo(CacheDirectory).EnumerateFiles())
        {
            if (f.Name.EndsWith(".part", StringComparison.Ordinal)) continue; // being written
            try { long size = f.Length; f.Delete(); freed += size; } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        Changed?.Invoke("Visualization cache cleared.");
        return freed;
    }

    private void Change(Action change, string message)
    {
        lock (gate)
        {
            var before = JsonSerializer.Serialize(config, Json);
            change();
            try { Save(); }
            catch
            {
                config = JsonSerializer.Deserialize<VisualsConfig>(before, Json)!;
                throw;
            }
        }
        Changed?.Invoke(message);
    }

    // ── Formats ──────────────────────────────────────────────────────────────

    private static readonly string[] Mappings = Enum.GetNames<SpectrogramMapping>();
    private static readonly string[] Codecs = Enum.GetNames<FvzCodec>();

    /// <summary>Checks a format on its own (not against the limits) and normalises the names of enums.</summary>
    private static VisualFormat Validate(VisualFormat f)
    {
        static void Check(bool ok, string message) { if (!ok) throw new ArgumentException(message); }
        Check(f.Bands is >= 1 and <= HardMaxBands, $"Bands must be 1–{HardMaxBands}.");
        Check(f.Fps is >= 1 and <= HardMaxFps, $"Frame rate must be 1–{HardMaxFps}.");
        Check(IsPow2(f.FftResolution) && f.FftResolution is >= HardMinFft and <= HardMaxFft, $"FFT resolution must be a power of two from {HardMinFft} to {HardMaxFft}.");
        Check(f.DbRange is > 0 and <= 200, "dB range must be 1–200.");
        Check(f.DbFloor is >= -200 and < 0, "dB floor must be below 0.");
        Check(f.FrequencyMin is >= 1 and <= 20000 && f.FrequencyMax > f.FrequencyMin && f.FrequencyMax <= 96000, "Frequency range is invalid.");
        Check(f.Smoothness is >= 0 and <= 16, "Smoothness must be 0–16.");
        Check(f.QuantizeBits is >= FvzFormat.MinQuantizeBits and <= FvzFormat.MaxQuantizeBits, "Quantize bits must be 4–16.");
        Check(f.DecayPerSecond is >= 0 and <= 100, "Decay must be 0–100 per second.");
        var mapping = Mappings.FirstOrDefault(m => m.Equals(f.Mapping, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Mapping must be Normalized, Mel or Log10.");
        var codec = Codecs.FirstOrDefault(c => c.Equals(f.Codec, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Codec must be Brotli, Zstd or None.");
        return f with { Mapping = mapping, Codec = codec };
    }

    private static VisualLimits ValidateLimits(VisualLimits l)
    {
        static void Check(bool ok, string message) { if (!ok) throw new ArgumentException(message); }
        Check(l.MinBands >= 1 && l.MaxBands <= HardMaxBands && l.MinBands <= l.MaxBands, $"Band limits must satisfy 1 ≤ min ≤ max ≤ {HardMaxBands}.");
        Check(l.MinFps >= 1 && l.MaxFps <= HardMaxFps && l.MinFps <= l.MaxFps, $"Frame rate limits must satisfy 1 ≤ min ≤ max ≤ {HardMaxFps}.");
        Check(IsPow2(l.MinFftResolution) && IsPow2(l.MaxFftResolution) && l.MinFftResolution >= HardMinFft
              && l.MaxFftResolution <= HardMaxFft && l.MinFftResolution <= l.MaxFftResolution, "FFT limits must be powers of two, min ≤ max.");
        Check(l.MinQuantizeBits >= FvzFormat.MinQuantizeBits && l.MaxQuantizeBits <= FvzFormat.MaxQuantizeBits
              && l.MinQuantizeBits <= l.MaxQuantizeBits, "Quantize bit limits must satisfy 4 ≤ min ≤ max ≤ 16.");
        return l;
    }

    private static bool IsPow2(int v) => v > 0 && (v & (v - 1)) == 0;

    private static bool Within(VisualFormat f, VisualLimits l) =>
        f.Bands >= l.MinBands && f.Bands <= l.MaxBands && f.Fps >= l.MinFps && f.Fps <= l.MaxFps
        && f.FftResolution >= l.MinFftResolution && f.FftResolution <= l.MaxFftResolution
        && f.QuantizeBits >= l.MinQuantizeBits && f.QuantizeBits <= l.MaxQuantizeBits && (l.AllowStereo || !f.Stereo);

    private static VisualFormat Clamp(VisualFormat f, VisualLimits l) => f with
    {
        Bands = Math.Clamp(f.Bands, l.MinBands, l.MaxBands),
        Fps = Math.Clamp(f.Fps, l.MinFps, l.MaxFps),
        FftResolution = Math.Clamp(f.FftResolution, l.MinFftResolution, l.MaxFftResolution),
        QuantizeBits = Math.Clamp(f.QuantizeBits, l.MinQuantizeBits, l.MaxQuantizeBits),
        Stereo = f.Stereo && l.AllowStereo,
    };

    /// <summary>
    /// The format to generate for a client request: the defaults with the request's fields on top, clamped to the
    /// limits. Throws <see cref="ArgumentException"/> for values that make no sense at all (not merely out of range).
    /// </summary>
    public VisualFormat Resolve(VisualFormatRequest? request)
    {
        VisualFormat defaults;
        VisualLimits limits;
        lock (gate) { defaults = config.Defaults; limits = config.Limits; }
        if (request == null) return defaults;

        int fft = request.FftResolution ?? defaults.FftResolution;
        if (fft > 0 && !IsPow2(fft)) fft = 1 << (int)Math.Round(Math.Log2(fft)); // nearest power of two
        var merged = defaults with
        {
            Bands = request.Bands ?? defaults.Bands,
            Fps = request.Fps ?? defaults.Fps,
            FftResolution = fft,
            Mapping = request.Mapping ?? defaults.Mapping,
            DbFloor = request.DbFloor ?? defaults.DbFloor,
            DbRange = request.DbRange ?? defaults.DbRange,
            FrequencyMin = request.FrequencyMin ?? defaults.FrequencyMin,
            FrequencyMax = request.FrequencyMax ?? defaults.FrequencyMax,
            Smoothness = request.Smoothness ?? defaults.Smoothness,
            Stereo = request.Stereo ?? defaults.Stereo,
            QuantizeBits = request.QuantizeBits ?? defaults.QuantizeBits,
            DecayPerSecond = request.DecayPerSecond ?? defaults.DecayPerSecond,
            Codec = request.Codec ?? defaults.Codec,
        };
        // Clamp the ranged values first so a request like bands=4000 becomes the maximum instead of an error.
        merged = Clamp(merged with
        {
            Bands = Math.Clamp(merged.Bands, 1, HardMaxBands),
            Fps = Math.Clamp(merged.Fps, 1, HardMaxFps),
            FftResolution = Math.Clamp(merged.FftResolution, HardMinFft, HardMaxFft),
            QuantizeBits = Math.Clamp(merged.QuantizeBits, FvzFormat.MinQuantizeBits, FvzFormat.MaxQuantizeBits),
        }, limits);
        return Validate(merged);
    }

    /// <summary>A short stable hash of everything that affects the file's bytes, for cache file names and ETags.</summary>
    public static string FormatKey(VisualFormat f)
    {
        var text = string.Join('|', FvzFormat.CurrentVersion, f.Bands, f.Fps, f.FftResolution, f.Mapping, f.DbFloor.ToString("R"),
            f.DbRange.ToString("R"), f.FrequencyMin.ToString("R"), f.FrequencyMax.ToString("R"), f.Smoothness, f.Stereo,
            f.QuantizeBits, f.DecayPerSecond.ToString("R"), f.Codec);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
    }

    // ── Generation ───────────────────────────────────────────────────────────

    public bool Enabled { get { lock (gate) return config.Enabled; } }

    private static string Stamp(string sourcePath) => File.GetLastWriteTimeUtc(sourcePath).Ticks.ToString();

    /// <summary>Path of the cached .fvz for a track in a format, generating it first if needed.</summary>
    public async Task<string> GetFvzAsync(string trackId, string sourcePath, VisualFormat format, CancellationToken token)
    {
        var dest = Path.Combine(CacheDirectory, $"{SafeId(trackId)}_{Stamp(sourcePath)}_{FormatKey(format)}.fvz");
        await Ensure(dest, () => GenerateFvz(sourcePath, dest, format), token);
        return dest;
    }

    /// <summary>Paths of the cached beat-times and moments JSON for a track (one analysis produces both).</summary>
    public async Task<(string Beats, string Moments)> GetAnalysisAsync(string trackId, string sourcePath, CancellationToken token)
    {
        var stem = Path.Combine(CacheDirectory, $"{SafeId(trackId)}_{Stamp(sourcePath)}_a{VisualAnalysis.Version}");
        string beats = stem + ".beats.json", moments = stem + ".moments.json";
        await Ensure(beats, () => GenerateAnalysis(trackId, sourcePath, beats, moments), token, also: moments);
        return (beats, moments);
    }

    /// <summary>
    /// Prepares data for upcoming tracks in the background, one at a time so prefetching never takes more than one of
    /// the analysis slots. Returns how many tracks were queued (requests beyond the backlog limit are dropped).
    /// </summary>
    public int Prefetch(IReadOnlyList<(string Id, string Path)> tracks, VisualFormat format, bool fvz, bool analysis)
    {
        var batch = new List<(string Id, string Path)>();
        foreach (var t in tracks.Take(MaxPrefetch))
        {
            if (Interlocked.Increment(ref prefetchQueued) > 4 * MaxPrefetch) { Interlocked.Decrement(ref prefetchQueued); break; }
            batch.Add(t);
        }
        if (batch.Count == 0) return 0;

        // Frames for every track first (quick, and what a client needs the moment a song starts), then the slower beat
        // analysis. Each job still waits on the shared job gate, so this runs as wide as MaxConcurrentJobs allows. It
        // used to run as one chain, one track at a time, so the next song's frames could wait behind other songs'
        // analysis and arrive after the song had started.
        _ = Task.Run(async () =>
        {
            async Task Run(Func<Task> job, string id)
            {
                try { await job(); }
                catch (Exception ex) { Serilog.Log.Debug(ex, "Visualization prefetch failed for {Id}", id); }
            }
            try
            {
                if (fvz) await Task.WhenAll(batch.Select(t => Run(() => GetFvzAsync(t.Id, t.Path, format, CancellationToken.None), t.Id)));
                if (analysis) await Task.WhenAll(batch.Select(t => Run(() => GetAnalysisAsync(t.Id, t.Path, CancellationToken.None), t.Id)));
            }
            finally { Interlocked.Add(ref prefetchQueued, -batch.Count); }
        });
        return batch.Count;
    }

    /// <summary>
    /// Returns once <paramref name="dest"/> exists, running <paramref name="produce"/> if it doesn't. Concurrent callers
    /// for the same file share one run. A caller giving up (token) doesn't cancel the run for the others.
    /// </summary>
    private async Task Ensure(string dest, Action produce, CancellationToken token, string? also = null)
    {
        if (File.Exists(dest) && (also == null || File.Exists(also)))
        {
            Touch(dest);
            if (also != null) Touch(also);
            return;
        }
        Directory.CreateDirectory(CacheDirectory);
        var lazy = inflight.GetOrAdd(dest, key => new Lazy<Task>(() => Task.Run(async () =>
        {
            var gateForJob = jobs;
            await gateForJob.WaitAsync();
            Interlocked.Increment(ref running);
            try
            {
                if (!File.Exists(dest) || (also != null && !File.Exists(also))) produce();
            }
            finally
            {
                Interlocked.Decrement(ref running);
                gateForJob.Release();
            }
            _ = Task.Run(() => Trim());
        })));
        try { await lazy.Value.WaitAsync(token); }
        finally { if (lazy.Value.IsCompleted) inflight.TryRemove(dest, out _); }
    }

    private void GenerateFvz(string sourcePath, string dest, VisualFormat f)
    {
        int threads;
        lock (gate) threads = Math.Max(1, Environment.ProcessorCount / config.MaxConcurrentJobs);
        int rate;
        using (var probe = AudioFiles.OpenReader(sourcePath)) rate = probe.WaveFormat.SampleRate;

        var encoder = new AudioEncoder(new EncoderOptions
        {
            BarCount = f.Bands,
            FPS = f.Fps,
            FFTResolution = f.FftResolution,
            BinMap = Enum.Parse<SpectrogramMapping>(f.Mapping),
            DbFloor = f.DbFloor,
            DbRange = f.DbRange,
            FrequencyMin = f.FrequencyMin,
            FrequencyMax = f.FrequencyMax,
            Smoothness = f.Smoothness,
            Stereo = f.Stereo,
            QuantizeBits = f.QuantizeBits,
            DecayPerSecond = f.DecayPerSecond,
            Codec = Enum.Parse<FvzCodec>(f.Codec),
            MaxThreads = threads,
            // Hi-res files are analysed at 48 kHz so the FFT window covers the same time as for CD audio.
            TargetSampleRate = rate > 48000 ? 48000 : 0,
        });
        if (AudioFiles.NeedsTrim(sourcePath))
        {
            // FFTVIS opens files with Media Foundation itself, which can't open this one; hand it decoded samples.
            int target = rate > 48000 ? 48000 : rate;
            encoder.LoadSamples(DecodeChannels(sourcePath, target), target);
        }
        else if (!encoder.LoadAudio(sourcePath)) throw new InvalidOperationException("The track could not be decoded.");
        encoder.GenerateFrames();
        WriteAtomically(dest, stream => encoder.Save(stream));
    }

    /// <summary>One float array per channel at <paramref name="rate"/>.</summary>
    private static float[][] DecodeChannels(string path, int rate)
    {
        using var reader = AudioFiles.OpenReader(path);
        int channels = reader.WaveFormat.Channels;
        ISampleProvider provider = reader.WaveFormat.SampleRate == rate ? reader : new NAudio.Wave.SampleProviders.WdlResamplingSampleProvider(reader, rate);
        var lists = Enumerable.Range(0, channels).Select(_ => new List<float>((int)Math.Min(int.MaxValue / 4, reader.TotalTime.TotalSeconds * rate + 4096))).ToArray();
        var buffer = new float[16384 * channels];
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
            for (int i = 0; i + channels <= read; i += channels)
                for (int c = 0; c < channels; c++) lists[c].Add(buffer[i + c]);
        return lists.Select(l => l.ToArray()).ToArray();
    }

    private static void GenerateAnalysis(string trackId, string sourcePath, string beatsPath, string momentsPath)
    {
        var (beats, moments) = VisualAnalysis.Analyse(trackId, sourcePath, CancellationToken.None);
        WriteAtomically(momentsPath, s => JsonSerializer.Serialize(s, moments, Json));
        WriteAtomically(beatsPath, s => JsonSerializer.Serialize(s, beats, Json)); // written last: Ensure checks it first
    }

    private static void WriteAtomically(string dest, Action<Stream> write)
    {
        var part = dest + "." + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            using (var fs = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16)) write(fs);
            File.Move(part, dest, true);
        }
        catch
        {
            try { File.Delete(part); } catch { }
            throw;
        }
    }

    private static void Touch(string path)
    {
        try { File.SetLastAccessTimeUtc(path, DateTime.UtcNow); } catch { }
    }

    // Track ids are Mongo ObjectIds; anything else is reduced to safe characters so it can't escape the cache folder.
    private static string SafeId(string id) => new(id.Where(char.IsAsciiLetterOrDigit).Take(64).ToArray());

    private void Trim()
    {
        try
        {
            long max;
            lock (gate) max = config.CacheMegabytes * 1024L * 1024L;
            var files = new DirectoryInfo(CacheDirectory).EnumerateFiles()
                .Where(f => !f.Name.EndsWith(".part", StringComparison.Ordinal))
                .OrderBy(f => f.LastAccessTimeUtc).ToList();
            long total = files.Sum(f => f.Length);
            foreach (var f in files)
            {
                if (total <= max) break;
                try { total -= f.Length; f.Delete(); } catch { }
            }
            // Clean up partial files left by a crash.
            foreach (var f in new DirectoryInfo(CacheDirectory).EnumerateFiles("*.part"))
                if (f.LastWriteTimeUtc < DateTime.UtcNow.AddHours(-1)) try { f.Delete(); } catch { }
        }
        catch { }
    }
}
