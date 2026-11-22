using Nova.Audio;
using Nova.Dsp;
using Nova.Temporal;

namespace Nova.Features
{
    public sealed record KeyResult(string Key, string Scale, double Strength, double TuningFrequency, double[] MeanHpcp);

    /// <summary>
    /// Key estimation as AcousticBrainz did it (tonal.key_key / key_scale / key_strength):
    /// tuning frequency from spectral-peak deviations, a 36-bin HPCP per frame referenced to that tuning,
    /// averaged over the track and correlated against chord-based Temperley key profiles.
    /// AcousticBrainz's 434.19 Hz tuning and its F-heavy HPCPs come from how spectral peaks are picked (see
    /// SpectralPeaks.Find); reproducing that rule is what makes our HPCPs match theirs.
    /// </summary>
    public static class Key
    {
        private const int FrameSize = 4096;
        private const int HopSize = 2048;
        private const double MinFrequency = 40;
        private const double MaxFrequency = 5000;
        private const double PeakThreshold = 1e-5;
        private const int HpcpSize = 36;
        private const double HpcpWindowSemitones = 4.0 / 3.0;

        // Pitch class 0 is the HPCP reference (A), so names start at A.
        private static readonly string[] Names = { "A", "A#", "B", "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#" };

        // Temperley's key profiles, indexed by semitones above the tonic.
        private static readonly double[] TemperleyMajor = { 0.748, 0.060, 0.488, 0.082, 0.670, 0.460, 0.096, 0.715, 0.104, 0.366, 0.057, 0.400 };
        private static readonly double[] TemperleyMinor = { 0.712, 0.084, 0.474, 0.618, 0.049, 0.460, 0.105, 0.747, 0.404, 0.067, 0.133, 0.330 };

        private static readonly double[] MajorProfile = BuildProfile(TemperleyMajor, minor: false);
        private static readonly double[] MinorProfile = BuildProfile(TemperleyMinor, minor: true);

        /// <param name="picking">
        /// <see cref="PeakPicking.AcousticBrainz"/> reproduces AcousticBrainz's values (model features);
        /// <see cref="PeakPicking.Accurate"/> gives the musically correct key and tuning (key compatibility).
        /// </param>
        /// <param name="referenceFrequency">Tuning to reference the HPCP to; estimated from the audio when null.</param>
        public static KeyResult Compute(MonoSignal signal, PeakPicking picking = PeakPicking.AcousticBrainz, double? referenceFrequency = null)
            => Compute(signal, new[] { picking }, referenceFrequency)[picking];

        /// <summary>One estimate per peak-picking mode, sharing the FFTs so extra modes cost little.</summary>
        public static IReadOnlyDictionary<PeakPicking, KeyResult> Compute(MonoSignal signal, IReadOnlyCollection<PeakPicking> pickings, double? referenceFrequency = null)
            => ComputeWithChroma(signal, pickings, null, referenceFrequency).Keys;

        /// <summary>
        /// The same keys, plus a 12-bin chroma per frame from the <paramref name="chromaPicking"/> mode's peaks and tuning,
        /// for the temporal features: each frame's HPCP folded to semitones (bin 0 = A) and scaled so its largest bin is 1.
        /// Frame t is centred at t · 2048 samples.
        /// </summary>
        internal static (IReadOnlyDictionary<PeakPicking, KeyResult> Keys, FrameSeries? Chroma) ComputeWithChroma(
            MonoSignal signal, IReadOnlyCollection<PeakPicking> pickings, PeakPicking? chromaPicking, double? referenceFrequency = null)
        {
            var all = chromaPicking is { } c && !pickings.Contains(c) ? pickings.Append(c).ToList() : pickings;
            var spectra = new SpectrumFrames(signal, all);
            var results = new Dictionary<PeakPicking, KeyResult>();
            FrameSeries? chroma = null;
            foreach (var (picking, frames) in spectra.Frames)
            {
                var tuning = referenceFrequency ?? TuningFrequency(frames);
                if (picking == chromaPicking)
                {
                    chroma = FrameChroma(frames, tuning, (double)HopSize / signal.SampleRate);
                }
                if (!pickings.Contains(picking))
                {
                    continue;
                }
                var hpcp = MeanHpcp(frames, tuning);
                var (key, scale, strength) = FromHpcp(hpcp);
                results[picking] = new KeyResult(key, scale, strength, tuning, hpcp);
            }
            return (results, chroma);
        }

        private static FrameSeries FrameChroma(List<TonalFrame> frames, double reference, double hopSeconds)
        {
            var values = new double[frames.Count * 12];
            var frame = new double[HpcpSize];
            var perSemitone = HpcpSize / 12;
            for (var t = 0; t < frames.Count; t++)
            {
                FrameHpcp(frames[t].HpcpPeaks, reference, frame);
                var chroma = values.AsSpan(t * 12, 12);
                // Bin 3k sits on semitone k; its neighbours a third of a semitone either side belong to it too.
                for (var k = 0; k < 12; k++)
                {
                    for (var o = -1; o <= 1; o++)
                    {
                        chroma[k] += frame[((k * perSemitone + o) % HpcpSize + HpcpSize) % HpcpSize];
                    }
                }
                var max = 0.0;
                foreach (var v in chroma)
                {
                    max = Math.Max(max, v);
                }
                if (max > 0)
                {
                    for (var k = 0; k < 12; k++)
                    {
                        chroma[k] /= max;
                    }
                }
            }
            return new FrameSeries(values, 12, hopSeconds);
        }

        /// <summary>Key, scale and strength for a 36-bin HPCP whose bin 0 is A.</summary>
        public static (string Key, string Scale, double Strength) FromHpcp(double[] hpcp)
        {
            var (majorShift, majorCorr) = BestShift(hpcp, MajorProfile);
            var (minorShift, minorCorr) = BestShift(hpcp, MinorProfile);
            var isMajor = majorCorr >= minorCorr;
            var shift = isMajor ? majorShift : minorShift;
            var pitchClass = (int)Math.Round(shift * 12.0 / HpcpSize) % 12;
            return (Names[pitchClass], isMajor ? "major" : "minor", isMajor ? majorCorr : minorCorr);
        }

        /// <summary>
        /// Each frame votes, weighted by its spectral energy, for the cents offset (1-cent bins) that collects the
        /// most peak magnitude; the winning offset sets the tuning, kept in [-35, 65) cents of A440.
        /// </summary>
        private static double TuningFrequency(List<TonalFrame> frames)
        {
            const int bins = 100;
            var global = new double[bins];
            var local = new double[bins];
            foreach (var (_, peaks, energy) in frames)
            {
                if (peaks.Count == 0)
                {
                    continue;
                }

                Array.Clear(local);
                foreach (var peak in peaks)
                {
                    var semitones = 12 * Math.Log2(peak.Frequency / 440);
                    var cents = 100 * (semitones - Math.Round(semitones));
                    var bin = (int)Math.Round(50 + cents);
                    local[bin == bins ? 0 : bin] += peak.Magnitude;
                }
                global[ArgMax(local)] += energy;
            }

            if (global.Max() <= 0)
            {
                return 440;
            }
            var tau = ArgMax(global) - 50.0;
            if (tau < -35)
            {
                tau += 100;
            }
            return 440 * Math.Pow(2, tau / 1200);
        }

        /// <summary>
        /// Each peak adds its squared magnitude to the bins within ±2/3 semitone of its pitch, weighted by a squared
        /// cosine; each frame is scaled so its largest bin is 1, then frames are averaged.
        /// </summary>
        private static double[] MeanHpcp(List<TonalFrame> frames, double reference)
        {
            var mean = new double[HpcpSize];
            var frame = new double[HpcpSize];
            var count = 0;
            foreach (var (peaks, _, _) in frames)
            {
                FrameHpcp(peaks, reference, frame);

                var max = frame.Max();
                if (max > 0)
                {
                    for (var i = 0; i < HpcpSize; i++)
                    {
                        mean[i] += frame[i] / max;
                    }
                }
                count++;
            }

            if (count > 0)
            {
                for (var i = 0; i < HpcpSize; i++)
                {
                    mean[i] /= count;
                }
            }
            return mean;
        }

        /// <summary>One frame's unscaled HPCP: each peak's squared magnitude spread over the bins within ±2/3 semitone.</summary>
        private static void FrameHpcp(List<SpectralPeak> peaks, double reference, double[] frame)
        {
            var perSemitone = HpcpSize / 12.0;
            var reach = perSemitone * HpcpWindowSemitones / 2;
            Array.Clear(frame);
            foreach (var peak in peaks)
            {
                var position = HpcpSize * Math.Log2(peak.Frequency / reference);
                var weight2 = peak.Magnitude * peak.Magnitude;
                for (var k = (int)Math.Ceiling(position - reach); k <= (int)Math.Floor(position + reach); k++)
                {
                    var distance = Math.Abs(position - k) / perSemitone;
                    var w = Math.Cos(Math.PI * distance / HpcpWindowSemitones);
                    frame[((k % HpcpSize) + HpcpSize) % HpcpSize] += w * w * weight2;
                }
            }
        }

        /// <summary>
        /// "Polyphonic" profile: each degree's value becomes the weight of the triad built on it, only I, IV and V
        /// are kept (minor keys use the major V of the harmonic minor), and every chord tone gets its root's weight.
        /// The 12 values are then stretched to 36 bins by linear interpolation towards the next pitch class.
        /// Run on AcousticBrainz's own mean HPCPs this reproduces 59 of 62 submissions' keys. Adding harmonics to the
        /// chord tones, as Essentia's parameter list suggests, made the match worse, so none are added.
        /// </summary>
        private static double[] BuildProfile(double[] degrees, bool minor)
        {
            var chords = minor
                ? new[] { (0, 3), (5, 3), (7, 4) }
                : new[] { (0, 4), (5, 4), (7, 4) };

            var pc = new double[12];
            foreach (var (root, third) in chords)
            {
                foreach (var note in new[] { root, root + third, root + 7 })
                {
                    pc[note % 12] += degrees[root];
                }
            }

            var stretch = HpcpSize / 12;
            var profile = new double[HpcpSize];
            for (var i = 0; i < 12; i++)
            {
                var next = pc[(i + 1) % 12];
                for (var j = 0; j < stretch; j++)
                {
                    profile[i * stretch + j] = pc[i] + (next - pc[i]) * j / stretch;
                }
            }
            return profile;
        }

        private static (int shift, double correlation) BestShift(double[] hpcp, double[] profile)
        {
            var best = double.NegativeInfinity;
            var bestShift = 0;
            var rotated = new double[profile.Length];
            for (var s = 0; s < profile.Length; s++)
            {
                for (var i = 0; i < profile.Length; i++)
                {
                    rotated[i] = profile[((i - s) % profile.Length + profile.Length) % profile.Length];
                }
                var r = Pearson(hpcp, rotated);
                if (r > best)
                {
                    best = r;
                    bestShift = s;
                }
            }
            return (bestShift, best);
        }

        private static double Pearson(double[] a, double[] b)
        {
            var ma = a.Average();
            var mb = b.Average();
            double num = 0, da = 0, db = 0;
            for (var i = 0; i < a.Length; i++)
            {
                num += (a[i] - ma) * (b[i] - mb);
                da += (a[i] - ma) * (a[i] - ma);
                db += (b[i] - mb) * (b[i] - mb);
            }
            return da > 0 && db > 0 ? num / Math.Sqrt(da * db) : 0;
        }

        private static int ArgMax(double[] x)
        {
            var arg = 0;
            for (var i = 1; i < x.Length; i++)
            {
                if (x[i] > x[arg])
                {
                    arg = i;
                }
            }
            return arg;
        }

        private readonly record struct TonalFrame(List<SpectralPeak> HpcpPeaks, List<SpectralPeak> TuningPeaks, double Energy);

        /// <summary>
        /// Spectral peaks of every tonal frame (4096 / 2048, Blackman-Harris 62), one FFT per frame shared by every
        /// requested peak-picking mode and by both passes. The HPCP gets every peak above the magnitude threshold;
        /// tuning gets the 100 lowest-frequency peaks with no threshold, which is what SpectralPeaks' defaults give
        /// with orderBy "frequency".
        /// </summary>
        private sealed class SpectrumFrames
        {
            private const int TuningPeaks = 100;

            public Dictionary<PeakPicking, List<TonalFrame>> Frames { get; } = new();

            public SpectrumFrames(MonoSignal signal, IEnumerable<PeakPicking> pickings)
            {
                foreach (var picking in pickings)
                {
                    Frames[picking] = new();
                }

                var window = Window.Create(WindowType.BlackmanHarris62, FrameSize);
                var fft = new Fft(FrameSize);
                var frame = new double[FrameSize];
                var magnitude = new double[FrameSize / 2 + 1];
                var peaks = new List<SpectralPeak>();
                foreach (var start in Framing.Starts(signal.Samples.Length, FrameSize, HopSize, startFromZero: false))
                {
                    Framing.Fill(signal.Samples, start, window, frame);
                    fft.RealSpectrum(frame, magnitude);
                    var energy = 0.0;
                    foreach (var m in magnitude)
                    {
                        energy += m * m;
                    }
                    foreach (var (picking, frames) in Frames)
                    {
                        SpectralPeaks.Find(magnitude, signal.SampleRate, MinFrequency, MaxFrequency, 0, peaks, picking);
                        frames.Add(new TonalFrame(peaks.Where(p => p.Magnitude > PeakThreshold).ToList(), peaks.Take(TuningPeaks).ToList(), energy));
                    }
                }
            }
        }
    }
}
