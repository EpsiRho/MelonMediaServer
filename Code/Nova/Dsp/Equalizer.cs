namespace Nova.Dsp
{
    /// <summary>The shape of one equalizer band.</summary>
    public enum EqBandType
    {
        /// <summary>Bell boost or cut around <see cref="EqBand.Frequency"/>; width from <see cref="EqBand.Q"/>.</summary>
        Peak,
        /// <summary>Boost or cut everything below the corner.</summary>
        LowShelf,
        /// <summary>Boost or cut everything above the corner.</summary>
        HighShelf,
        /// <summary>Removes content above the corner at <see cref="EqBand.Slope"/> dB/octave.</summary>
        LowPass,
        /// <summary>Removes content below the corner at <see cref="EqBand.Slope"/> dB/octave.</summary>
        HighPass,
        /// <summary>Keeps a band around the centre (0 dB at the centre); width from Q.</summary>
        BandPass,
        /// <summary>Removes a narrow band around the centre; width from Q.</summary>
        Notch,
    }

    /// <summary>One band of an <see cref="EqualizerSettings"/>.</summary>
    public sealed class EqBand
    {
        public EqBandType Type { get; set; } = EqBandType.Peak;
        /// <summary>Centre or corner frequency in Hz. Values above the track's Nyquist range are pulled just under it.</summary>
        public double Frequency { get; set; } = 1000;
        /// <summary>Boost (positive) or cut (negative) in dB. Used by Peak and the shelves.</summary>
        public double Gain { get; set; }
        /// <summary>
        /// Width: higher is narrower. Peak/BandPass/Notch: centre ÷ bandwidth (1.41 ≈ one octave). Shelves: 0.707 is
        /// a smooth transition, higher adds overshoot. LowPass/HighPass at 12 dB/oct: resonance (0.707 is flat).
        /// </summary>
        public double Q { get; set; } = Equalizer.Butterworth;
        /// <summary>LowPass/HighPass roll-off in dB per octave: 6, 12, 18, 24, 36 or 48.</summary>
        public int Slope { get; set; } = 12;
        public bool Enabled { get; set; } = true;
    }

    /// <summary>A complete equalizer: bands in series plus an output gain.</summary>
    public sealed class EqualizerSettings
    {
        public const int MaxBands = 32;
        public const double MinFrequency = 10, MaxFrequency = 40_000;
        public const double MaxGain = 30, MinQ = 0.05, MaxQ = 40;
        public static readonly int[] Slopes = { 6, 12, 18, 24, 36, 48 };

        /// <summary>Gain in dB applied to the output (after the bands).</summary>
        public double Preamp { get; set; }
        /// <summary>
        /// Lowers the output by the response's highest boost, so boosting bands doesn't push loud passages into
        /// clipping. On by default; turn it off to manage <see cref="Preamp"/> by hand.
        /// </summary>
        public bool AutoHeadroom { get; set; } = true;
        public List<EqBand> Bands { get; set; } = new();

        /// <summary>Null when the settings are usable, otherwise a message describing the first problem.</summary>
        public string? Validate()
        {
            if (Bands == null) return "Bands are required.";
            if (Bands.Count > MaxBands) return $"At most {MaxBands} bands.";
            if (!double.IsFinite(Preamp) || Math.Abs(Preamp) > MaxGain) return $"Preamp must be between -{MaxGain} and {MaxGain} dB.";
            for (var i = 0; i < Bands.Count; i++)
            {
                var b = Bands[i];
                if (b == null) return $"Band {i + 1} is empty.";
                if (!Enum.IsDefined(b.Type)) return $"Band {i + 1}: unknown type.";
                if (!double.IsFinite(b.Frequency) || b.Frequency < MinFrequency || b.Frequency > MaxFrequency)
                    return $"Band {i + 1}: frequency must be between {MinFrequency} and {MaxFrequency} Hz.";
                if (!double.IsFinite(b.Gain) || Math.Abs(b.Gain) > MaxGain) return $"Band {i + 1}: gain must be between -{MaxGain} and {MaxGain} dB.";
                if (!double.IsFinite(b.Q) || b.Q < MinQ || b.Q > MaxQ) return $"Band {i + 1}: Q must be between {MinQ} and {MaxQ}.";
                if (b.Type is EqBandType.LowPass or EqBandType.HighPass && Array.IndexOf(Slopes, b.Slope) < 0)
                    return $"Band {i + 1}: slope must be one of {string.Join(", ", Slopes)} dB/octave.";
            }
            return null;
        }
    }

    /// <summary>
    /// Applies <see cref="EqualizerSettings"/> to interleaved float audio, in blocks, keeping filter state between
    /// calls so a track can be streamed through it. Sections run in double precision (transposed direct form II),
    /// so narrow low-frequency bands stay clean. Output above -1 dBFS is soft-clipped instead of hard-clipped.
    /// <para>Cost is about 9 flops per sample per section: ten bands on a stereo 44.1 kHz track take roughly a
    /// millisecond per second of audio, far below decoding or encoding.</para>
    /// </summary>
    public sealed class Equalizer
    {
        public const double Butterworth = 0.7071067811865476;
        private const int BlockFrames = 1024;
        private const double ClipKnee = 0.891250938133746; // -1 dBFS

        private readonly Biquad[] _sections;
        private readonly double[] _state; // per channel, per section: s1, s2
        private readonly double[] _scratch = new double[BlockFrames * 2]; // two channels at a time
        private readonly double _gain;

        public int SampleRate { get; }
        public int Channels { get; }
        /// <summary>The output gain in dB actually applied: preamp minus the automatic headroom.</summary>
        public double OutputGainDb { get; }
        /// <summary>True when the settings change nothing (no active bands, 0 dB gain).</summary>
        public bool IsBypass { get; }

        public Equalizer(EqualizerSettings settings, int sampleRate, int channels)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
            var error = settings.Validate();
            if (error != null) throw new ArgumentException(error, nameof(settings));

            SampleRate = sampleRate;
            Channels = channels;
            _sections = Design(settings, sampleRate);
            _state = new double[_sections.Length * channels * 2];
            OutputGainDb = OutputGain(settings, _sections, sampleRate);
            _gain = Math.Pow(10, OutputGainDb / 20);
            IsBypass = _sections.Length == 0 && OutputGainDb == 0;
        }

        /// <summary>Clears the filter history, e.g. after seeking.</summary>
        public void Reset() => Array.Clear(_state);

        /// <summary>Filters <paramref name="samples"/> (interleaved, whole frames) in place.</summary>
        public void Process(Span<float> samples)
        {
            var channels = Channels;
            var frames = samples.Length / channels;
            for (var start = 0; start < frames; start += BlockFrames)
            {
                var count = Math.Min(BlockFrames, frames - start);
                var block = samples.Slice(start * channels, count * channels);
                // Channels go through in pairs: each section's recursion is a serial chain of multiply-adds, and
                // running two independent chains in one loop lets the CPU overlap them (about twice as fast).
                for (var c = 0; c < channels; c += 2)
                {
                    var pair = c + 1 < channels;
                    var x0 = _scratch.AsSpan(0, count);
                    var x1 = _scratch.AsSpan(BlockFrames, count);
                    for (var n = 0; n < count; n++) x0[n] = block[n * channels + c];
                    if (pair) for (var n = 0; n < count; n++) x1[n] = block[n * channels + c + 1];
                    for (var k = 0; k < _sections.Length; k++)
                    {
                        if (pair) Run2(_sections[k], x0, x1, (c * _sections.Length + k) * 2, ((c + 1) * _sections.Length + k) * 2);
                        else Run(_sections[k], x0, (c * _sections.Length + k) * 2);
                    }
                    Store(x0, block, c, channels);
                    if (pair) Store(x1, block, c + 1, channels);
                }
            }
        }

        private void Store(ReadOnlySpan<double> x, Span<float> block, int channel, int channels)
        {
            var gain = _gain;
            for (var n = 0; n < x.Length; n++)
            {
                var y = x[n] * gain;
                block[n * channels + channel] = (float)(Math.Abs(y) <= ClipKnee ? y : SoftClip(y));
            }
        }

        /// <summary>One section over a block; coefficients and history stay in registers.</summary>
        private void Run(in Biquad f, Span<double> x, int state)
        {
            double b0 = f.B0, b1 = f.B1, b2 = f.B2, a1 = f.A1, a2 = f.A2;
            double s1 = _state[state], s2 = _state[state + 1];
            for (var n = 0; n < x.Length; n++)
            {
                var input = x[n];
                var y = b0 * input + s1;
                s1 = b1 * input - a1 * y + s2;
                s2 = b2 * input - a2 * y;
                x[n] = y;
            }
            Save(state, s1, s2);
        }

        /// <summary><see cref="Run"/> for two channels at once.</summary>
        private void Run2(in Biquad f, Span<double> x, Span<double> z, int stateX, int stateZ)
        {
            double b0 = f.B0, b1 = f.B1, b2 = f.B2, a1 = f.A1, a2 = f.A2;
            double s1 = _state[stateX], s2 = _state[stateX + 1];
            double t1 = _state[stateZ], t2 = _state[stateZ + 1];
            z = z[..x.Length];
            for (var n = 0; n < x.Length; n++)
            {
                double inX = x[n], inZ = z[n];
                var y = b0 * inX + s1;
                var w = b0 * inZ + t1;
                s1 = b1 * inX - a1 * y + s2;
                t1 = b1 * inZ - a1 * w + t2;
                s2 = b2 * inX - a2 * y;
                t2 = b2 * inZ - a2 * w;
                x[n] = y;
                z[n] = w;
            }
            Save(stateX, s1, s2);
            Save(stateZ, t1, t2);
        }

        private void Save(int state, double s1, double s2)
        {
            // After long digital silence the history decays toward subnormals, which are very slow on x86.
            _state[state] = Math.Abs(s1) < 1e-30 ? 0 : s1;
            _state[state + 1] = Math.Abs(s2) < 1e-30 ? 0 : s2;
        }

        private static double SoftClip(double x)
        {
            var a = Math.Abs(x);
            if (a <= ClipKnee) return x;
            // Linear up to the knee, then a tanh curve that meets it with slope 1 and approaches full scale.
            const double room = 1 - ClipKnee;
            return Math.CopySign(ClipKnee + room * Math.Tanh((a - ClipKnee) / room), x);
        }

        // ── Response ─────────────────────────────────────────────────────────

        /// <summary>The bands' combined response in dB at each frequency (excluding the output gain).</summary>
        public static double[] ResponseDb(EqualizerSettings settings, int sampleRate, ReadOnlySpan<double> frequencies)
        {
            var sections = Design(settings, sampleRate);
            var result = new double[frequencies.Length];
            for (var i = 0; i < frequencies.Length; i++) result[i] = SectionsDb(sections, frequencies[i], sampleRate);
            return result;
        }

        /// <summary><paramref name="count"/> log-spaced frequencies from 20 Hz to 20 kHz (or Nyquist), for plotting.</summary>
        public static double[] LogFrequencies(int count, int sampleRate)
        {
            count = Math.Max(2, count);
            var top = Math.Min(20_000, sampleRate * 0.5);
            var result = new double[count];
            for (var i = 0; i < count; i++) result[i] = 20 * Math.Pow(top / 20, i / (double)(count - 1));
            return result;
        }

        /// <summary>The output gain in dB the equalizer applies for these settings at this rate.</summary>
        public static double ComputeOutputGainDb(EqualizerSettings settings, int sampleRate) =>
            OutputGain(settings, Design(settings, sampleRate), sampleRate);

        private static double OutputGain(EqualizerSettings settings, Biquad[] sections, int sampleRate)
        {
            if (!settings.AutoHeadroom || sections.Length == 0) return settings.Preamp;
            // Highest point of the response over the audible range: a dense log grid plus each band's own frequency.
            var peak = 0.0;
            foreach (var f in LogFrequencies(512, sampleRate)) peak = Math.Max(peak, SectionsDb(sections, f, sampleRate));
            foreach (var band in settings.Bands)
                if (band.Enabled && band.Frequency < sampleRate * 0.5)
                    peak = Math.Max(peak, SectionsDb(sections, band.Frequency, sampleRate));
            return settings.Preamp - peak;
        }

        private static double SectionsDb(Biquad[] sections, double frequency, int sampleRate)
        {
            var omega = 2 * Math.PI * Math.Clamp(frequency, 0, sampleRate * 0.5) / sampleRate;
            var power = 1.0;
            foreach (var s in sections) power *= s.MagnitudeSquared(omega);
            return 10 * Math.Log10(Math.Max(power, 1e-30));
        }

        // ── Design ───────────────────────────────────────────────────────────

        /// <summary>The cascade of sections for the enabled bands at <paramref name="sampleRate"/>.</summary>
        public static Biquad[] Design(EqualizerSettings settings, int sampleRate)
        {
            var sections = new List<Biquad>();
            var limit = sampleRate * 0.47; // the matched design needs the corner under Nyquist
            foreach (var band in settings.Bands)
            {
                if (band is not { Enabled: true }) continue;
                var gainless = band.Type is EqBandType.LowPass or EqBandType.HighPass or EqBandType.BandPass or EqBandType.Notch;
                if (!gainless && band.Gain == 0) continue; // a 0 dB bell or shelf is a wire
                // A low-pass at or above Nyquist removes nothing that's there.
                if (band.Type == EqBandType.LowPass && band.Frequency >= limit) continue;

                var omega = 2 * Math.PI * Math.Min(band.Frequency, limit) / sampleRate;
                switch (band.Type)
                {
                    case EqBandType.Peak: sections.Add(MatchedBiquad.Peak(omega, band.Gain, band.Q)); break;
                    case EqBandType.LowShelf: sections.Add(MatchedBiquad.LowShelf(omega, band.Gain, band.Q)); break;
                    case EqBandType.HighShelf: sections.Add(MatchedBiquad.HighShelf(omega, band.Gain, band.Q)); break;
                    case EqBandType.BandPass: sections.Add(MatchedBiquad.BandPass(omega, band.Q)); break;
                    case EqBandType.Notch: sections.Add(MatchedBiquad.Notch(omega, band.Q)); break;
                    case EqBandType.LowPass:
                    case EqBandType.HighPass:
                        AddPass(sections, omega, band.Type == EqBandType.HighPass, band.Slope, band.Q);
                        break;
                }
            }
            return sections.ToArray();
        }

        /// <summary>
        /// A Butterworth cascade of order slope/6: second-order sections with Q = 1 / (2·sin((2k-1)π / 2N)) plus a
        /// first-order section for odd orders. At 12 dB/oct the band's own Q sets the resonance.
        /// </summary>
        private static void AddPass(List<Biquad> sections, double omega, bool highPass, int slope, double q)
        {
            var order = slope / 6;
            if (order == 2)
            {
                sections.Add(highPass ? MatchedBiquad.HighPass(omega, q) : MatchedBiquad.LowPass(omega, q));
                return;
            }
            for (var k = 1; k <= order / 2; k++)
            {
                var sectionQ = 1 / (2 * Math.Sin((2 * k - 1) * Math.PI / (2 * order)));
                sections.Add(highPass ? MatchedBiquad.HighPass(omega, sectionQ) : MatchedBiquad.LowPass(omega, sectionQ));
            }
            if (order % 2 == 1) sections.Add(MatchedBiquad.FirstOrder(omega, highPass));
        }
    }
}
