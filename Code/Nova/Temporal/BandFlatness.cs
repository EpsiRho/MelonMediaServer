using Nova.Audio;
using Nova.Dsp;

namespace Nova.Temporal
{
    /// <summary>
    /// How noise-like the sound is in each of the four <see cref="BandOnsets"/> bands: per frame, the spectral flatness of
    /// the band's power spectrum (geometric over arithmetic mean of its bins). Hiss, static and distortion spread their
    /// power evenly across bins and score high; tones, chords and drones put it into a few peaks and score near 0.
    /// White noise reads about 0.56 (e^−γ: the power in a noise bin is exponentially distributed), a sine about 0.
    /// Per band rather than over the whole spectrum, so a noise's overall tilt (pink, brown) doesn't read as tonal.
    /// The high band stops at 11 kHz: above it music rolls off steeply and lossy encoders cut it off entirely (MP3s near
    /// 16 kHz), and those empty bins would make every track read as tonal there.
    /// Unlike the chroma's flatness, which comes from spectral peaks and so reads noise as near 0, this needs no peaks.
    /// A band that is near silent in a frame scores 0: no sound is not noise, and the dither under a fade-out (white noise
    /// around −90 dBFS) shouldn't read as static. The line is a per-bin level of −80 dBFS, white noise's at that level.
    /// A band more than 60 dB below the frame's loudest band also scores 0: that is window leakage from the loud band,
    /// not sound of its own.
    /// The fifth value is the frame's noisiness: the four flatnesses weighted by each band's share of the frame's power,
    /// so quiet hiss under a loud drone barely counts while a full-band noise wall reads as noise.
    /// </summary>
    public static class BandFlatness
    {
        // 4,096 samples (93 ms at 44.1 kHz) so even the low band (under 150 Hz) has about 14 bins to be flat across.
        private const int FrameSize = 4096;
        private const int HopSize = 2048;
        private const double SilentLevel = 1e-8;
        private const double BinFloor = 1e-20;
        private const double LeakageRatio = 1e-6;
        private const double TopHz = 11000;

        /// <summary>Values per frame: the four band flatnesses, then the power-weighted noisiness.</summary>
        public const int Dims = BandOnsets.Bands + 1;

        /// <returns><see cref="Dims"/> values per frame, 0 to 1; frame t is centred at t · 2,048 samples.</returns>
        public static FrameSeries Compute(MonoSignal signal)
        {
            var bins = FrameSize / 2 + 1;
            var band = new int[bins];
            var binsPerBand = new int[BandOnsets.Bands];
            for (var k = 1; k < bins; k++)
            {
                var frequency = (double)k * signal.SampleRate / FrameSize;
                if (frequency >= TopHz)
                {
                    band[k] = -1;
                    continue;
                }
                var b = 0;
                while (frequency >= BandOnsets.Edges[b + 1])
                {
                    b++;
                }
                band[k] = b;
                binsPerBand[b]++;
            }

            var window = Window.Create(WindowType.Hann, FrameSize);
            // White noise of variance σ² gives each bin σ² · Σw² on average, so the line scales with the window.
            var silent = SilentLevel * window.Sum(w => (double)w * w);
            var fft = new Fft(FrameSize);
            var frame = new double[FrameSize];
            var magnitude = new double[bins];
            var values = new List<double>();
            Span<double> logSum = stackalloc double[BandOnsets.Bands];
            Span<double> sum = stackalloc double[BandOnsets.Bands];
            foreach (var start in Framing.Starts(signal.Samples.Length, FrameSize, HopSize, startFromZero: false))
            {
                Framing.Fill(signal.Samples, start, window, frame);
                fft.RealSpectrum(frame, magnitude);
                logSum.Clear();
                sum.Clear();
                // Bin 0 is DC, which isn't sound.
                for (var k = 1; k < bins; k++)
                {
                    if (band[k] < 0)
                    {
                        continue;
                    }
                    var power = magnitude[k] * magnitude[k] + BinFloor;
                    logSum[band[k]] += Math.Log(power);
                    sum[band[k]] += power;
                }
                var loudest = 0.0;
                for (var b = 0; b < BandOnsets.Bands; b++)
                {
                    loudest = Math.Max(loudest, binsPerBand[b] > 0 ? sum[b] / binsPerBand[b] : 0);
                }
                double weighted = 0, total = 0;
                for (var b = 0; b < BandOnsets.Bands; b++)
                {
                    var n = binsPerBand[b];
                    var mean = n > 0 ? sum[b] / n : 0;
                    var flat = mean < silent || mean < LeakageRatio * loudest ? 0 : Math.Clamp(Math.Exp(logSum[b] / n) / mean, 0, 1);
                    values.Add(flat);
                    weighted += flat * sum[b];
                    total += sum[b];
                }
                values.Add(loudest < silent ? 0 : weighted / total);
            }
            return new FrameSeries(values.ToArray(), Dims, (double)HopSize / signal.SampleRate);
        }
    }
}
