using Melon.Models;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Nova.Audio;
using Nova.Dsp;
using Nova.Features;
using Nova.Temporal;

namespace Melon.LocalClasses;

/// <summary>
/// Turns Nova's rhythm and structure analysis into the beat-times and moments clients react to.
/// Everything here is deterministic for a given file, so results are cached per file version.
/// </summary>
internal static class VisualAnalysis
{
    /// <summary>Bump when the output changes meaning, so cached JSON from older versions is regenerated.</summary>
    public const int Version = 1;

    /// <summary>Tracks longer than this (DJ mixes, audiobooks) get beats but no structure; Nova's cost grows with length.</summary>
    private const double MaxStructureSeconds = 30 * 60;

    public static (BeatTimes Beats, Moments Moments) Analyse(string trackId, string path, CancellationToken token)
    {
        var decoded = DecodeMono(path, AudioDecoder.AnalysisSampleRate, token);
        // Nova's descriptors expect the replay-gain-normalised signal, as in its own FeatureExtractor.
        var gain = ReplayGain.FromEqualLoudness(EqualLoudness.Apply(decoded.Samples), decoded.SampleRate);
        var signal = ReplayGain.Normalize(decoded, gain);
        token.ThrowIfCancellationRequested();

        var onsetsTask = Task.Run(() => BandOnsets.Compute(signal), token);
        TemporalFeatures? temporal = null;
        double[] beats;
        if (signal.DurationSeconds <= MaxStructureSeconds)
        {
            temporal = TemporalFeatures.Compute(signal);
            beats = temporal.Grid.Beats;
        }
        else
        {
            beats = Tempo.Compute(signal).Beats;
        }
        var onsets = onsetsTask.Result;
        token.ThrowIfCancellationRequested();

        return (BuildBeats(trackId, signal.DurationSeconds, beats, temporal, onsets),
                BuildMoments(trackId, signal.DurationSeconds, temporal, beats));
    }

    /// <summary>Decodes to mono at <paramref name="rate"/> with NAudio, so Nova never needs ffmpeg.</summary>
    internal static MonoSignal DecodeMono(string path, int rate, CancellationToken token)
    {
        using var reader = new AudioFileReader(path);
        ISampleProvider provider = reader;
        int channels = reader.WaveFormat.Channels;
        if (reader.WaveFormat.SampleRate != rate) provider = new WdlResamplingSampleProvider(provider, rate);
        var samples = new List<float>((int)Math.Min(int.MaxValue / 2, reader.TotalTime.TotalSeconds * rate + 4096));
        var buffer = new float[16384 * channels];
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            token.ThrowIfCancellationRequested();
            for (int i = 0; i + channels <= read; i += channels)
            {
                float sum = 0;
                for (int c = 0; c < channels; c++) sum += buffer[i + c];
                samples.Add(sum / channels);
            }
        }
        return new MonoSignal(samples.ToArray(), rate);
    }

    // ── Beats ────────────────────────────────────────────────────────────────

    private static BeatTimes BuildBeats(string trackId, double duration, double[] beats, TemporalFeatures? temporal, FrameSeries onsets)
    {
        int n = beats.Length;
        var downbeat = new bool[n];
        int perBar = BeatGrid.DefaultBeatsPerBar;
        double confidence = 0;
        string rhythm = "noPulse";
        if (temporal != null)
        {
            foreach (var start in temporal.Grid.BarStarts) if (start < n) downbeat[start] = true;
            perBar = temporal.Grid.BeatsPerBar;
            confidence = temporal.Grid.DownbeatConfidence;
            rhythm = temporal.Validity.Status switch
            {
                RhythmStatus.Pulse => "pulse",
                RhythmStatus.Uncertain => "uncertain",
                _ => "noPulse",
            };
        }
        else if (n >= 8)
        {
            rhythm = "uncertain";
            for (int i = 0; i < n; i += perBar) downbeat[i] = true;
        }

        // Onset strength per band around each beat: the strongest frame from a 32nd note before the beat to halfway to the next.
        var bands = new float[BandOnsets.Bands][];
        for (int b = 0; b < bands.Length; b++) bands[b] = new float[n];
        for (int i = 0; i < n; i++)
        {
            double next = i + 1 < n ? beats[i + 1] : beats[i] + (i > 0 ? beats[i] - beats[i - 1] : 0.5);
            double lead = (next - beats[i]) / 8;
            int from = Math.Max(0, (int)Math.Floor((beats[i] - lead) / onsets.HopSeconds));
            int to = Math.Min(onsets.Count, (int)Math.Ceiling((beats[i] + (next - beats[i]) / 2) / onsets.HopSeconds));
            for (int t = from; t < to; t++)
            {
                var frame = onsets.Frame(t);
                for (int b = 0; b < bands.Length; b++) bands[b][i] = Math.Max(bands[b][i], (float)frame[b]);
            }
        }

        // Scale each band by its own loud beats so a quiet hat pattern still registers next to a loud kick.
        var raw = bands.Select(b => (float[])b.Clone()).ToArray();
        foreach (var band in bands) Normalise(band, 0.95);
        var strength = new float[n];
        var sharpness = new float[n];
        for (int i = 0; i < n; i++)
        {
            float total = raw[0][i] + raw[1][i] + raw[2][i] + raw[3][i];
            strength[i] = total;
            // Weight bands by their normalised level so sharpness reflects what stands out on this beat.
            float lowSide = bands[0][i] + bands[1][i], highSide = bands[2][i] + bands[3][i];
            sharpness[i] = lowSide + highSide > 0 ? highSide / (lowSide + highSide) : 0.5f;
        }
        Normalise(strength, 0.95);

        return new BeatTimes(Version, trackId, Round(duration), Round(n >= 2 ? Tempo.BpmFromBeats(beats) : 0), rhythm,
            Round(confidence), perBar,
            new BeatList(beats.Select(Round).ToArray(), downbeat, Round(strength), Round(sharpness),
                Round(bands[0]), Round(bands[1]), Round(bands[2]), Round(bands[3])),
            FindHits(onsets));
    }

    /// <summary>
    /// Percussive hits: peaks in the kick (low), snare (low-mid + high-mid) and hat (high) onset curves that stand out
    /// from the surrounding second and are the largest within ±50 ms.
    /// </summary>
    private static HitList FindHits(FrameSeries onsets)
    {
        var times = new List<double>();
        var kinds = new List<string>();
        var strengths = new List<float>();
        int n = onsets.Count;
        double hop = onsets.HopSeconds;
        var kinds3 = new (string Kind, int[] Bands, double MinGap)[]
        {
            ("kick", [0], 0.09), ("snare", [1, 2], 0.09), ("hat", [3], 0.06),
        };
        foreach (var (kind, bandIndexes, minGap) in kinds3)
        {
            var x = new double[n];
            for (int t = 0; t < n; t++)
            {
                var frame = onsets.Frame(t);
                foreach (var b in bandIndexes) x[t] += frame[b];
                x[t] /= bandIndexes.Length;
            }
            double top = Percentile(x, 0.99);
            if (top <= 0) continue;

            // Running mean and deviation over ±1 s, from prefix sums.
            int radius = (int)Math.Round(1.0 / hop);
            var sum = new double[n + 1];
            var sq = new double[n + 1];
            for (int t = 0; t < n; t++) { sum[t + 1] = sum[t] + x[t]; sq[t + 1] = sq[t] + x[t] * x[t]; }
            int peakRadius = Math.Max(1, (int)Math.Round(0.05 / hop));
            double last = double.NegativeInfinity;
            for (int t = 0; t < n; t++)
            {
                if (x[t] < 0.15 * top) continue;
                int a = Math.Max(0, t - radius), b = Math.Min(n, t + radius + 1);
                double mean = (sum[b] - sum[a]) / (b - a);
                double sd = Math.Sqrt(Math.Max(0, (sq[b] - sq[a]) / (b - a) - mean * mean));
                if (x[t] < mean + 1.5 * sd) continue;
                bool isPeak = true;
                for (int k = Math.Max(0, t - peakRadius); k <= Math.Min(n - 1, t + peakRadius) && isPeak; k++)
                    if (x[k] > x[t] || (x[k] == x[t] && k < t)) isPeak = false;
                if (!isPeak) continue;
                double time = t * hop;
                if (time - last < minGap) continue;
                last = time;
                times.Add(time);
                kinds.Add(kind);
                strengths.Add((float)Math.Min(1, x[t] / top));
            }
        }
        var order = Enumerable.Range(0, times.Count).OrderBy(i => times[i]).ToArray();
        return new HitList(order.Select(i => Round(times[i])).ToArray(), order.Select(i => kinds[i]).ToArray(),
            order.Select(i => (float)Math.Round(strengths[i], 3)).ToArray());
    }

    // ── Moments ──────────────────────────────────────────────────────────────

    private const double SilenceDb = -55;
    private const double SoftEnergy = 0.35, EnergeticEnergy = 0.72, BeatlessPulse = RhythmValidity.SalientPulse;

    private static Moments BuildMoments(string trackId, double duration, TemporalFeatures? temporal, double[] beats)
    {
        if (temporal == null || temporal.Clock.Count == 0)
            return new Moments(Version, trackId, Round(duration), 1, [], [], [], []);

        var clock = temporal.Clock;
        int n = clock.Count;
        var loud = clock.Loudness;

        // Energy: loudness relative to the track's loud passages (a 20 dB window under its 95th percentile), blended
        // with onset activity relative to its busy passages, then smoothed over 3 seconds.
        var audible = loud.Where(l => l > SilenceDb).ToArray();
        double hi = audible.Length > 0 ? Percentile(audible, 0.95) : 0, lo = hi - 20;
        var activity = new double[n];
        for (int r = 0; r < n; r++) foreach (var v in clock.OnsetAt(r)) activity[r] += v;
        double busy = Math.Max(1e-9, Percentile(activity, 0.95));
        var raw = new double[n];
        for (int r = 0; r < n; r++)
        {
            double le = Math.Clamp((loud[r] - lo) / (hi - lo), 0, 1);
            double oe = Math.Clamp(activity[r] / busy, 0, 1);
            raw[r] = loud[r] <= SilenceDb ? 0 : 0.6 * le + 0.4 * oe;
        }
        var energy = Smooth(raw, 1);
        var pulse = clock.PulseSalience;

        var items = new List<Moment>();
        var silent = Enumerable.Range(0, n).Select(r => loud[r] <= SilenceDb).ToArray();
        AddRuns(items, "silence", silent, 2, energy, quiet: true);
        AddRuns(items, "soft", Enumerable.Range(0, n).Select(r => !silent[r] && energy[r] < SoftEnergy).ToArray(), 6, energy, quiet: true);
        var energetic = AddRuns(items, "energetic", energy.Select(e => e > EnergeticEnergy).ToArray(), 6, energy, quiet: false);
        AddRuns(items, "beatless", Enumerable.Range(0, n).Select(r => !silent[r] && pulse[r] < BeatlessPulse).ToArray(), 8, energy, quiet: false);

        if (energetic.Count > 0)
        {
            var peak = energetic.OrderByDescending(m => m.Intensity).First();
            items.Add(peak with { Kind = "peak" });
        }

        // Drops: energy jumps by 0.3 within two seconds of a calmer stretch, with the kick band rising with it.
        // Builds: the stretch of rising energy (at least 0.2 over 4–20 s, mostly upward) that leads into a drop or an energetic run.
        var lowOnset = Enumerable.Range(0, n).Select(r => clock.OnsetAt(r)[0]).ToArray();
        var targets = new SortedSet<int>(energetic.Select(m => (int)m.Start));
        double lastDrop = double.NegativeInfinity;
        for (int r = 3; r + 1 < n; r++)
        {
            double before = (energy[r - 3] + energy[r - 2] + energy[r - 1]) / 3;
            double after = Math.Max(energy[r], energy[r + 1]);
            double kickBefore = (lowOnset[r - 3] + lowOnset[r - 2] + lowOnset[r - 1]) / 3;
            double kickAfter = Math.Max(lowOnset[r], lowOnset[r + 1]);
            if (after - before >= 0.3 && kickAfter > 1.5 * kickBefore + 1e-6 && r - lastDrop >= 8)
            {
                double at = SnapToBeat(r - 0.5, beats, 1.0);
                items.Add(new Moment("drop", Round(at), Round(at), (float)Math.Round(Math.Min(1, after - before), 3)));
                targets.Add(r);
                lastDrop = r;
            }
        }
        foreach (var target in targets)
        {
            var build = FindBuild(energy, target);
            if (build is { } b) items.Add(new Moment("build", b.Start, target, (float)Math.Round(energy.Skip(b.Start).Take(target - b.Start).Average(), 3)));
        }

        var sections = temporal.Structure.Sections.OrderBy(s => s.Start)
            .Select(s => new MomentSection(Round(s.Start), Round(Math.Min(duration, s.Start + s.Length)))).ToArray();
        return new Moments(Version, trackId, Round(duration), ClockSeries.Hop,
            energy.Select(e => (float)Math.Round(e, 3)).ToArray(), pulse.Select(p => (float)Math.Round(p, 3)).ToArray(),
            items.OrderBy(m => m.Start).ThenBy(m => m.Kind, StringComparer.Ordinal).ToArray(), sections);
    }

    /// <summary>Adds runs of at least <paramref name="minSeconds"/> seconds, bridging gaps of up to 2 seconds.</summary>
    private static List<Moment> AddRuns(List<Moment> items, string kind, bool[] flags, int minSeconds, double[] energy, bool quiet)
    {
        var added = new List<Moment>();
        int n = flags.Length, r = 0;
        while (r < n)
        {
            if (!flags[r]) { r++; continue; }
            int start = r, end = r;
            int k = r;
            while (k < n)
            {
                if (flags[k]) { end = k + 1; k++; continue; }
                int gap = 0;
                while (k + gap < n && !flags[k + gap] && gap <= 2) gap++;
                if (gap <= 2 && k + gap < n && flags[k + gap]) { k += gap; continue; }
                break;
            }
            if (end - start >= minSeconds)
            {
                double mean = energy.Skip(start).Take(end - start).Average();
                var m = new Moment(kind, start, end, (float)Math.Round(quiet ? 1 - mean : mean, 3));
                items.Add(m);
                added.Add(m);
            }
            r = Math.Max(end, r + 1);
        }
        return added;
    }

    private static (int Start, int End)? FindBuild(double[] energy, int target)
    {
        if (target < 4) return null;
        int earliest = Math.Max(0, target - 20);
        int low = target - 1;
        for (int t = target - 2; t >= earliest; t--) if (energy[t] < energy[low]) low = t;
        if (target - low < 4 || energy[target - 1] - energy[low] < 0.2) return null;
        int rising = 0;
        for (int t = low + 1; t < target; t++) if (energy[t] >= energy[t - 1] - 0.01) rising++;
        return rising >= 0.7 * (target - low - 1) ? (low, target) : null;
    }

    private static double SnapToBeat(double time, double[] beats, double within)
    {
        if (beats.Length == 0) return time;
        int i = Array.BinarySearch(beats, time);
        if (i < 0) i = ~i;
        double best = time, bestGap = within;
        for (int k = Math.Max(0, i - 1); k <= Math.Min(beats.Length - 1, i); k++)
            if (Math.Abs(beats[k] - time) <= bestGap) { best = beats[k]; bestGap = Math.Abs(beats[k] - time); }
        return best;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static double[] Smooth(double[] x, int radius)
    {
        var y = new double[x.Length];
        for (int i = 0; i < x.Length; i++)
        {
            double s = 0; int c = 0;
            for (int k = Math.Max(0, i - radius); k <= Math.Min(x.Length - 1, i + radius); k++) { s += x[k]; c++; }
            y[i] = s / c;
        }
        return y;
    }

    private static void Normalise(float[] x, double percentile)
    {
        double top = Percentile(x.Select(v => (double)v).ToArray(), percentile);
        if (top <= 0) return;
        for (int i = 0; i < x.Length; i++) x[i] = (float)Math.Min(1, x[i] / top);
    }

    private static double Percentile(double[] x, double p)
    {
        if (x.Length == 0) return 0;
        var sorted = (double[])x.Clone();
        Array.Sort(sorted);
        return sorted[(int)Math.Clamp(Math.Round(p * (sorted.Length - 1)), 0, sorted.Length - 1)];
    }

    private static double Round(double v) => Math.Round(v, 3);
    private static float[] Round(float[] v) => v.Select(x => (float)Math.Round(x, 3)).ToArray();
}
