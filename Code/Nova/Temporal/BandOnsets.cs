using Nova.Audio;
using Nova.Dsp;

namespace Nova.Temporal
{
    /// <summary>
    /// Onset strength in four frequency bands at 100 frames a second: low under 150 Hz (kick, bass), low-mid 150–800 Hz
    /// (snare body), high-mid 800–4,000 Hz (snare crack, vocals, leads), high above 4,000 Hz (hi-hats, cymbals), the
    /// same edges as AcousticBrainz's energy bands.
    /// Per bin, magnitudes are log-compressed (log(1 + 100·m)) so a quiet hi-hat counts next to a loud kick, and only
    /// rises from the previous frame count, so decays aren't onsets. Each band is the mean rise per bin, which keeps
    /// bands with many bins comparable to bands with few.
    /// </summary>
    public static class BandOnsets
    {
        public const int Bands = 4;
        public static readonly double[] Edges = { 0, 150, 800, 4000, double.MaxValue };

        private const int FrameSize = 1024;
        private const double HopSeconds = 0.01;
        private const double Compression = 100;

        /// <summary>
        /// Eight octave-wide bands for the learned Groove head's frame input (Nova.Frames): kick, bass, low and high
        /// snare body, crack, presence, hats, air. Finer than the four above so the head can tell, say, a rim from a snare.
        /// </summary>
        public static readonly double[] EightBandEdges = { 0, 100, 200, 400, 800, 1600, 3200, 6400, double.MaxValue };

        /// <returns>4 values per frame; frame t is centred at t · 10 ms.</returns>
        public static FrameSeries Compute(MonoSignal signal) => Compute(signal, Edges);

        /// <summary>The same onset strength over any band edges (ascending, first 0, last MaxValue).</summary>
        /// <returns>edges.Length − 1 values per frame; frame t is centred at t · 10 ms.</returns>
        public static FrameSeries Compute(MonoSignal signal, double[] edges)
        {
            var bands = edges.Length - 1;
            var hop = (int)Math.Round(signal.SampleRate * HopSeconds);
            var bins = FrameSize / 2 + 1;
            var band = new int[bins];
            var binsPerBand = new int[bands];
            for (var k = 1; k < bins; k++)
            {
                var frequency = (double)k * signal.SampleRate / FrameSize;
                var b = 0;
                while (frequency >= edges[b + 1])
                {
                    b++;
                }
                band[k] = b;
                binsPerBand[b]++;
            }

            var window = Window.Create(WindowType.Hann, FrameSize);
            var fft = new Fft(FrameSize);
            var frame = new double[FrameSize];
            var magnitude = new double[bins];
            var previous = new double[bins];
            var values = new List<double>();
            var t = 0;
            Span<double> rises = stackalloc double[bands];
            foreach (var start in Framing.Starts(signal.Samples.Length, FrameSize, hop, startFromZero: false))
            {
                Framing.Fill(signal.Samples, start, window, frame);
                fft.RealSpectrum(frame, magnitude);
                rises.Clear();
                // Bin 0 is DC, which carries no onsets.
                for (var k = 1; k < bins; k++)
                {
                    var level = Math.Log(1 + Compression * magnitude[k]);
                    if (t > 0 && level > previous[k])
                    {
                        rises[band[k]] += level - previous[k];
                    }
                    previous[k] = level;
                }
                for (var b = 0; b < bands; b++)
                {
                    values.Add(binsPerBand[b] > 0 ? rises[b] / binsPerBand[b] : 0);
                }
                t++;
            }
            return new FrameSeries(values.ToArray(), bands, (double)hop / signal.SampleRate);
        }
    }
}
