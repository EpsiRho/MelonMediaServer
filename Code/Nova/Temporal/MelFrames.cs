using Nova.Audio;
using Nova.Dsp;

namespace Nova.Temporal
{
    /// <summary>
    /// Log-mel level at 100 frames a second: the learned Groove head's v2 input (Nova.Frames --mel). 64 mel bands from
    /// 0 to 16 kHz over 2,048-sample Hann frames (21.5 Hz bins, so even the lowest bands get a few), on the magnitude
    /// spectrum, compressed as log(1 + 10,000 · m). Nova's magnitudes are small (a loud band is about 0.5), so this is
    /// a true log above a floor about 74 dB under that: a band's level minus its mean is then its movement in dB, and a
    /// static EQ or gain cancels out.
    /// <para>
    /// Only the level is written. The head's onset channel is its positive change from frame to frame, and its level
    /// channel has each band's clip mean removed, both worked out when a window is cut, so one file serves any window.
    /// </para>
    /// </summary>
    public static class MelFrames
    {
        public const int Bands = 64;
        public const double LowHz = 0;
        public const double HighHz = 16000;

        private const int FrameSize = 2048;
        private const double HopSeconds = 0.01;
        private const double Compression = 10000;

        /// <returns><see cref="Bands"/> values per frame; frame t is centred at t · 10 ms.</returns>
        public static FrameSeries Compute(MonoSignal signal)
        {
            var hop = (int)Math.Round(signal.SampleRate * HopSeconds);
            var bins = FrameSize / 2 + 1;
            var filters = MelFilterbank.Create(signal.SampleRate, bins, Bands, LowHz, HighHz);
            var window = Window.Create(WindowType.Hann, FrameSize);
            var fft = new Fft(FrameSize);
            var frame = new double[FrameSize];
            var magnitude = new double[bins];
            var values = new List<double>();
            foreach (var start in Framing.Starts(signal.Samples.Length, FrameSize, hop, startFromZero: false))
            {
                Framing.Fill(signal.Samples, start, window, frame);
                fft.RealSpectrum(frame, magnitude);
                for (var b = 0; b < Bands; b++)
                {
                    var m = 0.0;
                    foreach (var (bin, weight) in filters[b])
                    {
                        m += weight * magnitude[bin];
                    }
                    values.Add(Math.Log(1 + Compression * m));
                }
            }
            return new FrameSeries(values.ToArray(), Bands, (double)hop / signal.SampleRate);
        }
    }
}
