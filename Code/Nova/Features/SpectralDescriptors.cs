using System.Runtime.InteropServices;
using Nova.Audio;
using Nova.Dsp;
using Nova.Temporal;

namespace Nova.Features
{
    /// <summary>
    /// Frame-wise descriptors from AcousticBrainz's lowlevel namespace, summarised with the same statistics.
    /// Frames are 2048 samples with a 1024 hop and a Blackman-Harris 62 window, on the replay-gain-normalised signal.
    /// As in AcousticBrainz, spectral centroid, spread/skewness/kurtosis, entropy, dissonance and contrast use an
    /// equal-loudness-filtered copy; everything else uses the signal as is. Zero-crossing and silence rates use
    /// the unwindowed frames.
    /// Keys are AcousticBrainz's names, e.g. "mfcc", "spectral_centroid", "barkbands_crest", "silence_rate_20dB".
    /// </summary>
    public static class SpectralDescriptors
    {
        private const int FrameSize = 2048;
        private const int HopSize = 1024;
        private const int Bins = FrameSize / 2 + 1;
        private const int MelBandCount = 40;
        private const double MelLow = 0;
        private const double MelHigh = 11000;
        private const int CepstrumCount = 13;
        private const double RolloffCutoff = 0.85;
        private const double SilentPower = 1e-10;
        // Added to digitally silent frames to avoid log(0). Essentia's "-100 dB" noise has amplitude 1e-10: on
        // AcousticBrainz, silent frames' spectral energy sits at 1e-20, 10 orders below what 1e-5 noise gives.
        private const double SilenceNoise = 1e-10;
        private const double ComplexityThreshold = 0.005;
        private const int MaxPeaks = 100;
        private const double PeakMaxFrequency = 5000;
        private const double SalienceLow = 100;
        private const double SalienceHigh = 5000;

        // Bark band edges in Hz; 27 bands use the first 28.
        private static readonly double[] BarkEdges =
        {
            0, 50, 100, 150, 200, 300, 400, 510, 630, 770, 920, 1080, 1270, 1480, 1720, 2000, 2320, 2700, 3150,
            3700, 4400, 5300, 6400, 7700, 9500, 12000, 15500, 20500,
        };

        private static readonly (string name, double low, double high)[] EnergyBands =
        {
            ("spectral_energyband_low", 20, 150),
            ("spectral_energyband_middle_low", 150, 800),
            ("spectral_energyband_middle_high", 800, 4000),
            ("spectral_energyband_high", 4000, 20000),
        };

        // Frame power thresholds; AcousticBrainz converts -20/-30/-60 with db2lin(dB / 2), i.e. 10^(dB / 20).
        private static readonly (string name, double threshold)[] SilenceRates =
        {
            ("silence_rate_20dB", Math.Pow(10, -20 / 20.0)),
            ("silence_rate_30dB", Math.Pow(10, -30 / 20.0)),
            ("silence_rate_60dB", Math.Pow(10, -60 / 20.0)),
        };

        /// <param name="equalLoudness">The signal already passed through <see cref="EqualLoudness.Apply"/>, if available.</param>
        public static IReadOnlyDictionary<string, DescriptorStats> Compute(MonoSignal signal, float[]? equalLoudness = null)
            => ComputeWithFrames(signal, equalLoudness, Array.Empty<string>()).Stats;

        /// <summary>
        /// The same statistics, plus the frame values of the descriptors named in <paramref name="keep"/> in time order,
        /// for the temporal features. Frame t is centred at t · 1024 samples.
        /// </summary>
        internal static (IReadOnlyDictionary<string, DescriptorStats> Stats, IReadOnlyDictionary<string, FrameSeries> Frames) ComputeWithFrames(
            MonoSignal signal, float[]? equalLoudness, IReadOnlyCollection<string> keep)
        {
            // The plain and equal-loudness passes are independent, so they run side by side.
            var plain = Task.Run(() => PlainPass(signal));
            var equalLoudnessPass = Task.Run(() => EqualLoudnessPass(signal, equalLoudness ?? EqualLoudness.Apply(signal.Samples)));
            Task.WaitAll(plain, equalLoudnessPass);
            var all = plain.Result.Frames.Concat(equalLoudnessPass.Result.Frames).ToList();
            var stats = all.ToDictionary(f => f.Key, f => DescriptorStats.FromValues(CollectionsMarshal.AsSpan(f.Value.Values), f.Value.Dims,
                covariance: f.Key is "mfcc" or "gfcc"));
            var hop = (double)HopSize / signal.SampleRate;
            var frames = all.Where(f => keep.Contains(f.Key))
                .ToDictionary(f => f.Key, f => new FrameSeries(f.Value.Values.ToArray(), f.Value.Dims, hop));
            return (stats, frames);
        }

        private static FrameLog PlainPass(MonoSignal signal)
        {
            var sampleRate = signal.SampleRate;
            var nyquist = sampleRate / 2.0;
            var scale = nyquist / (Bins - 1);
            var melFilters = MelFilters(sampleRate);
            var erbBands = new ErbBands(Bins, sampleRate);
            var salience = new PitchSalience(Bins, sampleRate);
            var peaks = new List<SpectralPeak>();
            var log = new FrameLog();
            void Add(string name, double value) => log.Add(name, value);
            void AddBands(string name, double[] values) => log.Add(name, values);
            void AddBandShape(string name, double[] bands)
            {
                var (spread, skewness, kurtosis) = ArrayShape.Distribution(bands, bands.Length - 1);
                Add(name + "_spread", spread);
                Add(name + "_skewness", skewness);
                Add(name + "_kurtosis", kurtosis);
                Add(name + "_crest", ArrayShape.Crest(bands));
                Add(name + "_flatness_db", ArrayShape.FlatnessDb(bands));
            }

            var previous = new double[Bins];
            var power = new double[Bins];
            var mel = new double[MelBandCount];
            var erb = new double[erbBands.Count];
            var bark = new double[BarkEdges.Length - 1];
            foreach (var (samples, spectrum) in Spectra(signal.Samples))
            {
                var framePower = 0.0;
                foreach (var s in samples)
                {
                    framePower += s * s;
                }
                framePower /= samples.Length;
                foreach (var (name, threshold) in SilenceRates)
                {
                    Add(name, framePower < threshold ? 1 : 0);
                }
                Add("zerocrossingrate", ZeroCrossingRate(samples));

                var energy = 0.0;
                var hfc = 0.0;
                for (var k = 0; k < Bins; k++)
                {
                    power[k] = spectrum[k] * spectrum[k];
                    energy += power[k];
                    hfc += k * scale * power[k];
                }

                // Mel bands (unit-sum triangles on the power spectrum) and MFCC (DCT-II of their dB values).
                Array.Clear(mel);
                for (var b = 0; b < MelBandCount; b++)
                {
                    foreach (var (bin, weight) in melFilters[b])
                    {
                        mel[b] += weight * power[bin];
                    }
                }
                AddBands("melbands", mel);
                AddBands("mfcc", Cepstrum(mel));
                AddBandShape("melbands", mel);

                // ERB bands (gammatone filterbank on the power spectrum) and GFCC, computed like MFCC.
                erbBands.Apply(power, erb);
                AddBands("erbbands", erb);
                AddBands("gfcc", Cepstrum(erb));
                AddBandShape("erbbands", erb);

                // Bark bands: summed power over half-open bin ranges with rounded edges.
                Array.Clear(bark);
                for (var b = 0; b < bark.Length; b++)
                {
                    var start = (int)Math.Floor(BarkEdges[b] / scale + 0.5);
                    var end = Math.Min((int)Math.Floor(BarkEdges[b + 1] / scale + 0.5), Bins);
                    if (start >= Bins)
                    {
                        break;
                    }
                    for (var k = start; k < end; k++)
                    {
                        bark[b] += power[k];
                    }
                }
                AddBands("barkbands", bark);
                AddBandShape("barkbands", bark);

                foreach (var (name, low, high) in EnergyBands)
                {
                    var start = (int)Math.Round(low / scale, MidpointRounding.AwayFromZero);
                    var stop = Math.Min((int)Math.Round(high / scale, MidpointRounding.AwayFromZero), Bins - 1);
                    var sum = 0.0;
                    for (var k = start; k <= stop; k++)
                    {
                        sum += power[k];
                    }
                    Add(name, sum);
                }

                Add("spectral_energy", energy);
                Add("spectral_rms", Math.Sqrt(energy / Bins));
                Add("spectral_rolloff", Rolloff(power, energy, scale));
                Add("spectral_decrease", ArrayShape.Decrease(power, nyquist));
                Add("hfc", hfc);
                Add("spectral_strongpeak", StrongPeak(spectrum));
                Add("pitch_salience", salience.Compute(spectrum));

                // Peaks between 100 Hz and 5 kHz above 0.005, including AcousticBrainz's forced boundary peak.
                SpectralPeaks.Find(spectrum, sampleRate, SalienceLow, PeakMaxFrequency, ComplexityThreshold, peaks);
                Add("spectral_complexity", Math.Min(peaks.Count, MaxPeaks));

                var flux = 0.0;
                for (var k = 0; k < Bins; k++)
                {
                    var d = spectrum[k] - previous[k];
                    flux += d * d;
                }
                Add("spectral_flux", Math.Sqrt(flux));
                previous = spectrum;
            }
            return log;
        }

        private static FrameLog EqualLoudnessPass(MonoSignal signal, float[] filtered)
        {
            var sampleRate = signal.SampleRate;
            var nyquist = sampleRate / 2.0;
            var scale = nyquist / (Bins - 1);
            var contrast = new SpectralContrast(FrameSize, sampleRate);
            var contrastCoeffs = new double[contrast.BandCount];
            var contrastValleys = new double[contrast.BandCount];
            var peaks = new List<SpectralPeak>();
            var log = new FrameLog();
            void Add(string name, double value) => log.Add(name, value);

            foreach (var (_, spectrum) in Spectra(filtered))
            {
                // Centroid of the power spectrum; the distribution shape is of the magnitude spectrum.
                double weighted = 0, total = 0;
                for (var k = 0; k < Bins; k++)
                {
                    var p = spectrum[k] * spectrum[k];
                    weighted += k * p;
                    total += p;
                }
                Add("spectral_centroid", total > 0 ? weighted / total * scale : 0);
                var (spread, skewness, kurtosis) = ArrayShape.Distribution(spectrum, nyquist);
                Add("spectral_spread", spread);
                Add("spectral_skewness", skewness);
                Add("spectral_kurtosis", kurtosis);
                Add("spectral_entropy", ArrayShape.Entropy(spectrum));

                SpectralPeaks.Find(spectrum, sampleRate, 0, PeakMaxFrequency, 0, peaks);
                if (peaks.Count > MaxPeaks)
                {
                    peaks.Sort((a, b) => b.Magnitude.CompareTo(a.Magnitude));
                    peaks.RemoveRange(MaxPeaks, peaks.Count - MaxPeaks);
                    peaks.Sort((a, b) => a.Frequency.CompareTo(b.Frequency));
                }
                Add("dissonance", Dissonance.Compute(peaks));

                contrast.Compute(spectrum, contrastCoeffs, contrastValleys);
                log.Add("spectral_contrast_coeffs", contrastCoeffs);
                log.Add("spectral_contrast_valleys", contrastValleys);
            }
            return log;
        }

        /// <summary>Per-descriptor frame values, in frame order, stored flat (frame t occupies [t·dims, (t+1)·dims)).</summary>
        private sealed class FrameLog
        {
            public Dictionary<string, (int Dims, List<double> Values)> Frames { get; } = new();

            public void Add(string name, double value) => Values(name, 1).Add(value);

            public void Add(string name, ReadOnlySpan<double> values) => Values(name, values.Length).AddRange(values);

            private List<double> Values(string name, int dims)
            {
                if (!Frames.TryGetValue(name, out var entry))
                {
                    Frames[name] = entry = (dims, new List<double>());
                }
                return entry.Values;
            }
        }

        /// <summary>
        /// Centred frames and their magnitude spectra; digitally silent frames get -100 dB noise first.
        /// The samples array is reused between frames, and the two magnitude arrays alternate, so the previous
        /// frame's spectrum stays valid while the current one is in use.
        /// </summary>
        private static IEnumerable<(double[] Samples, double[] Magnitude)> Spectra(float[] signal)
        {
            var window = Window.Create(WindowType.BlackmanHarris62, FrameSize);
            var fft = new Fft(FrameSize);
            var samples = new double[FrameSize];
            var frame = new double[FrameSize];
            var noise = new Random(0);
            var magnitudes = new[] { new double[Bins], new double[Bins] };
            var index = 0;
            foreach (var start in Framing.Starts(signal.Length, FrameSize, HopSize, startFromZero: false))
            {
                if (start >= 0 && start + FrameSize <= signal.Length)
                {
                    VectorMath.Widen(signal.AsSpan(start, FrameSize), samples);
                }
                else
                {
                    for (var i = 0; i < FrameSize; i++)
                    {
                        var j = start + i;
                        samples[i] = j >= 0 && j < signal.Length ? signal[j] : 0;
                    }
                }
                var power = VectorMath.Dot(samples, samples);
                if (power / FrameSize < SilentPower)
                {
                    for (var i = 0; i < FrameSize; i++)
                    {
                        samples[i] += SilenceNoise * (2 * noise.NextDouble() - 1);
                    }
                }
                VectorMath.Multiply(samples, window, frame);

                var magnitude = magnitudes[index ^= 1];
                fft.RealSpectrum(frame, magnitude);
                yield return (samples, magnitude);
            }
        }

        // AcousticBrainz's mel bands: the shared filterbank, 40 bands from 0 to 11 kHz.
        private static (int bin, double weight)[][] MelFilters(int sampleRate) =>
            MelFilterbank.Create(sampleRate, Bins, MelBandCount, MelLow, MelHigh);

        // Both filterbanks have 40 bands, so MFCC and GFCC share one DCT matrix.
        private static readonly double[,] DctMatrix = CreateDct(MelBandCount, CepstrumCount);

        /// <summary>DCT-II scaled by 1/sqrt(N) for the first coefficient and sqrt(2/N) for the rest.</summary>
        private static double[,] CreateDct(int n, int count)
        {
            var matrix = new double[count, n];
            for (var i = 0; i < count; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    matrix[i, j] = Math.Cos(Math.PI * i * (j + 0.5) / n) * (i == 0 ? Math.Sqrt(1.0 / n) : Math.Sqrt(2.0 / n));
                }
            }
            return matrix;
        }

        /// <summary>Cepstrum of band energies: the DCT of their amp2db values.</summary>
        private static double[] Cepstrum(double[] bands)
        {
            var output = new double[CepstrumCount];
            for (var j = 0; j < bands.Length; j++)
            {
                var db = AmpToDb(bands[j]);
                for (var i = 0; i < CepstrumCount; i++)
                {
                    output[i] += DctMatrix[i, j] * db;
                }
            }
            return output;
        }

        /// <summary>Lowest frequency below which 85% of the spectral energy lies.</summary>
        private static double Rolloff(double[] power, double energy, double scale)
        {
            var threshold = RolloffCutoff * energy;
            var cumulative = 0.0;
            for (var k = 0; k < power.Length; k++)
            {
                cumulative += power[k];
                if (cumulative >= threshold)
                {
                    return k * scale;
                }
            }
            return 0;
        }

        /// <summary>Sign changes per sample; zero counts as negative and the first sample is never a crossing.</summary>
        private static double ZeroCrossingRate(double[] x)
        {
            var count = 0;
            var wasPositive = x[0] > 0;
            for (var i = 1; i < x.Length; i++)
            {
                var positive = x[i] > 0;
                if (positive != wasPositive)
                {
                    count++;
                    wasPositive = positive;
                }
            }
            return (double)count / x.Length;
        }

        /// <summary>
        /// The spectrum's maximum divided by log10 of the ratio between the bin indices bounding its half-maximum
        /// run (one past the end over the first bin); 0 for a flat spectrum or a run that starts at bin 0.
        /// </summary>
        private static double StrongPeak(double[] x)
        {
            int maxIndex = 0, minIndex = 0;
            for (var k = 1; k < x.Length; k++)
            {
                if (x[k] > x[maxIndex])
                {
                    maxIndex = k;
                }
                if (x[k] < x[minIndex])
                {
                    minIndex = k;
                }
            }
            var max = x[maxIndex];
            if (max == x[minIndex])
            {
                return 0;
            }
            var threshold = max / 2;
            var left = maxIndex;
            while (left >= 0 && x[left] >= threshold)
            {
                left--;
            }
            left++;
            var right = maxIndex + 1;
            while (right < x.Length && x[right] >= threshold)
            {
                right++;
            }
            return left == 0 ? 0 : max / Math.Log10((double)right / left);
        }

        /// <summary>Essentia's amp2db: 20·log10(x), or -180 dB below 1e-9.</summary>
        private static double AmpToDb(double x) => x < 1e-9 ? -180 : 20 * Math.Log10(x);

        /// <summary>
        /// Peak of the magnitude spectrum's autocorrelation between lags of 100 Hz and 5 kHz, relative to lag 0:
        /// high for harmonic sounds, low for noise.
        /// </summary>
        private sealed class PitchSalience(int bins, int sampleRate)
        {
            private readonly int _lowLag = (int)(SalienceLow * bins / (sampleRate / 2.0));
            private readonly int _highLag = (int)(SalienceHigh * bins / (sampleRate / 2.0));

            public double Compute(double[] spectrum)
            {
                ReadOnlySpan<double> x = spectrum;
                var zeroLag = VectorMath.Dot(x, x);
                if (zeroLag == 0)
                {
                    return 0;
                }
                var max = double.NegativeInfinity;
                var k = _lowLag;
                for (; k + 4 <= _highLag; k += 4)
                {
                    var (r0, r1, r2, r3) = VectorMath.Autocorrelation4(x, k);
                    max = Math.Max(max, Math.Max(Math.Max(r0, r1), Math.Max(r2, r3)));
                }
                for (; k < _highLag; k++)
                {
                    max = Math.Max(max, VectorMath.Dot(x[..^k], x[k..]));
                }
                return max / zeroLag;
            }
        }
    }
}
