using Nova.Audio;

namespace Nova.Temporal
{
    /// <summary>
    /// The track in clock time, one row a second, independent of the bar grid: what's left to go on when a track has
    /// no pulse, or the grid is wrong. Row r covers [r, r + 1) seconds.
    /// <list type="bullet">
    /// <item>Loudness: RMS level in dB after replay-gain normalisation, -100 for silence.</item>
    /// <item>Onset: mean strength in the four <see cref="BandOnsets"/> bands.</item>
    /// <item>Pulse salience: how periodic the onsets are in an 8-second window centred on the row, whatever the period
    /// (see <see cref="Pulse"/>). Near 0 for noise, pads and speech; near 1 for a drum machine.</item>
    /// <item>Beat support: how well the beat tracker's period fits that window's periodicity, 0 to 1.</item>
    /// <item>Flatness: mean spectral flatness in the four bands, the high one capped at 11 kHz (see <see cref="BandFlatness"/>): near 0 for tones and
    /// drones, about 0.56 for white noise, 0 for silence. Tells static and harsh noise from ambient pads, which pulse
    /// salience and onsets can't.</item>
    /// <item>Noisiness: the band flatnesses weighted by each band's share of the power, one number for how much of what
    /// you hear is noise.</item>
    /// </list>
    /// Timbre and harmony in time order are in <see cref="BeatSequence"/>, whose rows carry their own times.
    /// </summary>
    public sealed record ClockSeries(double HopSeconds, double[] Loudness, double[] Onset, double[] PulseSalience, double[] BeatSupport,
        double[] Flatness, double[] Noisiness)
    {
        public const double Hop = 1.0;
        public const int OnsetDims = BandOnsets.Bands;
        public const int FlatnessDims = BandOnsets.Bands;

        public int Count => Loudness.Length;

        public ReadOnlySpan<double> OnsetAt(int row) => Onset.AsSpan(row * OnsetDims, OnsetDims);

        public ReadOnlySpan<double> FlatnessAt(int row) => Flatness.AsSpan(row * FlatnessDims, FlatnessDims);

        /// <param name="flatness">Flatness frames from <see cref="BandFlatness"/> (four bands, then noisiness).</param>
        /// <param name="beatPeriod">The tracker's median beat interval in seconds, or 0 when there are no beats.</param>
        public static ClockSeries Compute(MonoSignal signal, FrameSeries onsets, FrameSeries flatness, double beatPeriod)
        {
            var rows = Math.Max(0, (int)Math.Ceiling(signal.DurationSeconds / Hop - 1e-9));
            var loudness = new double[rows];
            var onset = new double[rows * OnsetDims];
            var salience = new double[rows];
            var support = new double[rows];
            var flat = new double[rows * FlatnessDims];
            var noisiness = new double[rows];
            Span<double> mean = stackalloc double[BandFlatness.Dims];
            var envelope = Pulse.Envelope(onsets);
            for (var r = 0; r < rows; r++)
            {
                var start = r * Hop;
                loudness[r] = Level(signal, start, start + Hop);
                if (onsets.Count > 0)
                {
                    onsets.Mean(start, start + Hop, onset.AsSpan(r * OnsetDims, OnsetDims));
                }
                if (flatness.Count > 0)
                {
                    flatness.Mean(start, start + Hop, mean);
                    mean[..FlatnessDims].CopyTo(flat.AsSpan(r * FlatnessDims, FlatnessDims));
                    noisiness[r] = mean[FlatnessDims];
                }
                (salience[r], support[r]) = Pulse.Analyse(envelope, onsets.HopSeconds, start + Hop / 2, beatPeriod);
            }
            return new ClockSeries(Hop, loudness, onset, salience, support, flat, noisiness);
        }

        internal static double Level(MonoSignal signal, double start, double end)
        {
            var first = Math.Clamp((int)(start * signal.SampleRate), 0, signal.Samples.Length);
            var stop = Math.Clamp((int)(end * signal.SampleRate), first, signal.Samples.Length);
            if (stop == first)
            {
                return -100;
            }
            var sum = 0.0;
            for (var i = first; i < stop; i++)
            {
                sum += (double)signal.Samples[i] * signal.Samples[i];
            }
            var meanSquare = sum / (stop - first);
            return meanSquare < 1e-10 ? -100 : 10 * Math.Log10(meanSquare);
        }
    }

    /// <summary>
    /// Periodicity of the onset envelope in a window, from its spectrum. The envelope (all four bands summed) has its
    /// slow trend removed (a Gaussian of σ = 1 s, which passes nothing at beat rates), so swells and fades don't count.
    /// A comb with teeth at every harmonic of a pulse rate f up to 16 Hz is slid over rates of 1–4 Hz (60–240 bpm).
    /// A beat is a train of sharp onsets, with power at many harmonics; two partials beating, or a tremolo, put a single
    /// sinusoid into the envelope, so the strongest tooth counts for no more than all the others together. The comb's
    /// share of the window's power between 0.5 and 16 Hz, less what white noise would give it with that many teeth,
    /// rescaled to 0–1, is the salience at f; the best over f is the window's salience. Drums score high, pads and noise
    /// near 0. Dense grooves count too: a samba's sixteenths and their harmonics are all teeth of its pulse.
    /// </summary>
    public static class Pulse
    {
        public const double WindowSeconds = 8;
        private const double TrendSigmaSeconds = 1;
        private const double MinRate = 1, MaxRate = 4, LowBand = 0.5, HighBand = 16;

        private static readonly object TableLock = new();
        private static (int Length, double Hop, double[] Cos, double[] Sin)? _table;

        /// <summary>The detrended onset envelope, one value per onset frame.</summary>
        public static double[] Envelope(FrameSeries onsets)
        {
            var n = onsets.Count;
            var raw = new double[n];
            for (var t = 0; t < n; t++)
            {
                foreach (var v in onsets.Frame(t))
                {
                    raw[t] += v;
                }
            }
            var sigma = TrendSigmaSeconds / onsets.HopSeconds;
            var radius = (int)Math.Ceiling(3 * sigma);
            var kernel = new double[2 * radius + 1];
            for (var k = -radius; k <= radius; k++)
            {
                kernel[k + radius] = Math.Exp(-k * k / (2 * sigma * sigma));
            }
            var detrended = new double[n];
            for (var t = 0; t < n; t++)
            {
                double sum = 0, weight = 0;
                for (var k = Math.Max(-radius, -t); k <= radius && t + k < n; k++)
                {
                    sum += kernel[k + radius] * raw[t + k];
                    weight += kernel[k + radius];
                }
                detrended[t] = raw[t] - sum / weight;
            }
            return detrended;
        }

        /// <summary>
        /// Salience of the window centred at <paramref name="centre"/> seconds (0 to 1), and how well a beat period
        /// agrees with it: the salience at the beat rate (or a half, third, double or triple of it) over the best, 1 when
        /// one of those is the most periodic rate. Both are 0 for a silent window; support is 0 without a period.
        /// </summary>
        public static (double Salience, double Support) Analyse(double[] envelope, double hopSeconds, double centre, double period)
        {
            var spectrum = Spectrum(envelope, hopSeconds, centre);
            if (spectrum == null)
            {
                return (0, 0);
            }
            var resolution = 1 / WindowSeconds;
            var first = (int)Math.Ceiling(LowBand / resolution);
            var total = 0.0;
            for (var k = first; k < spectrum.Length; k++)
            {
                total += spectrum[k];
            }
            if (total <= 0)
            {
                return (0, 0);
            }
            var bandBins = spectrum.Length - first;
            var best = 0.0;
            // A quarter-bin step, so a rate between bins isn't missed.
            for (var rate = MinRate; rate <= MaxRate + 1e-9; rate += resolution / 4)
            {
                best = Math.Max(best, Salience(spectrum, resolution, rate, total, bandBins));
            }

            var support = 0.0;
            if (period > 0 && best > 0)
            {
                // Beats fit the window if they sit on a metrical level of its pulse: the beat rate, or a half, third,
                // double or triple of it (a two-beat or triplet periodicity, a half-time tracker). A 4:3 or 3:2 error doesn't.
                foreach (var multiple in new[] { 1, 0.5, 1.0 / 3, 2, 3 })
                {
                    var rate = multiple / period;
                    if (rate >= MinRate - 1e-9 && rate <= MaxRate + 1e-9)
                    {
                        support = Math.Max(support, Math.Clamp(Salience(spectrum, resolution, rate, total, bandBins) / best, 0, 1));
                    }
                }
            }
            return (best, support);
        }

        /// <summary>The comb's share of the power at one rate, with white noise's share for that many teeth taken off, 0 to 1.</summary>
        private static double Salience(double[] spectrum, double resolution, double rate, double total, int bandBins)
        {
            var (score, teeth) = Comb(spectrum, resolution, rate);
            var baseline = Math.Min(0.9, 3.0 * teeth / bandBins);
            return Math.Max(0, (score / total - baseline) / (1 - baseline));
        }

        /// <summary>Power from 0 to HighBand Hz (bins below LowBand left at 0) of the Hann-windowed segment, or null when it's silent.</summary>
        private static double[]? Spectrum(double[] envelope, double hopSeconds, double centre)
        {
            var length = (int)Math.Round(WindowSeconds / hopSeconds);
            var resolution = 1 / WindowSeconds;
            var start = (int)Math.Round(centre / hopSeconds) - length / 2;
            var bins = (int)Math.Floor(HighBand / resolution) + 1;
            var window = new double[length];
            var energy = 0.0;
            for (var i = 0; i < length; i++)
            {
                var t = start + i;
                var v = t >= 0 && t < envelope.Length ? envelope[t] : 0;
                window[i] = v * (0.5 - 0.5 * Math.Cos(2 * Math.PI * (i + 0.5) / length));
                energy += window[i] * window[i];
            }
            if (energy < 1e-12)
            {
                return null;
            }
            var (cos, sin) = Table(length, hopSeconds, bins, resolution);
            var spectrum = new double[bins];
            var first = (int)Math.Ceiling(LowBand / resolution);
            for (var k = first; k < bins; k++)
            {
                double re = 0, im = 0;
                var row = k * length;
                for (var i = 0; i < length; i++)
                {
                    re += window[i] * cos[row + i];
                    im += window[i] * sin[row + i];
                }
                spectrum[k] = re * re + im * im;
            }
            return spectrum;
        }

        /// <summary>Cosines and sines for each bin and sample, computed once (the hop and window never change in practice).</summary>
        private static (double[] Cos, double[] Sin) Table(int length, double hopSeconds, int bins, double resolution)
        {
            lock (TableLock)
            {
                if (_table is { } t && t.Length == length && t.Hop == hopSeconds)
                {
                    return (t.Cos, t.Sin);
                }
                var cos = new double[bins * length];
                var sin = new double[bins * length];
                for (var k = 0; k < bins; k++)
                {
                    var w = 2 * Math.PI * k * resolution * hopSeconds;
                    for (var i = 0; i < length; i++)
                    {
                        cos[k * length + i] = Math.Cos(w * i);
                        sin[k * length + i] = Math.Sin(w * i);
                    }
                }
                _table = (length, hopSeconds, cos, sin);
                return (cos, sin);
            }
        }

        /// <summary>
        /// Power at every harmonic of the rate (each the nearest bin and its neighbours) up to the last bin, with the
        /// strongest tooth capped at the sum of the others; and how many teeth there were.
        /// </summary>
        private static (double Score, int Teeth) Comb(double[] spectrum, double resolution, double rate)
        {
            double sum = 0, strongest = 0;
            var teeth = 0;
            var first = (int)Math.Ceiling(LowBand / resolution);
            for (var h = 1; ; h++)
            {
                var centre = (int)Math.Round(h * rate / resolution);
                if (centre >= spectrum.Length)
                {
                    break;
                }
                var tooth = 0.0;
                for (var k = centre - 1; k <= centre + 1; k++)
                {
                    if (k >= first && k < spectrum.Length)
                    {
                        tooth += spectrum[k];
                    }
                }
                sum += tooth;
                strongest = Math.Max(strongest, tooth);
                teeth++;
            }
            var others = sum - strongest;
            return (others + Math.Min(strongest, others), teeth);
        }
    }
}
