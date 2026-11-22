using Nova.Audio;
using Nova.Dsp;

namespace Nova.Features
{
    /// <summary>
    /// Dynamic-range descriptor in [0, 1]: near 1 for heavily compressed tracks, near 0 for very dynamic ones.
    /// Matches AcousticBrainz's lowlevel.average_loudness: Stevens loudness (energy^0.67) on raw, unwindowed
    /// 2 s frames with a 1 s hop, normalised to the loudest frame, averaged in dB and squeezed between -5 and -2 dB.
    /// The floors apply to absolute levels, so frames must not be windowed or rescaled first.
    /// </summary>
    public static class AverageLoudness
    {
        private const int FrameSize = 88200;
        private const int HopSize = 44100;
        private const double Floor = 1e-4; // -80 dB relative to the loudest frame
        private const double SqueezeLow = -5.0;
        private const double SqueezeHigh = -2.0;

        public static double Compute(MonoSignal signal)
        {
            var window = Enumerable.Repeat(1.0, FrameSize).ToArray();
            var frame = new double[FrameSize];
            var levels = new List<double>();
            foreach (var start in Framing.Starts(signal.Samples.Length, FrameSize, HopSize, startFromZero: true))
            {
                Framing.Fill(signal.Samples, start, window, frame);
                var energy = 0.0;
                foreach (var x in frame)
                {
                    energy += x * x;
                }
                levels.Add(Math.Pow(energy, 0.67));
            }

            if (levels.Count == 0)
            {
                return double.NaN;
            }

            var max = Math.Max(levels.Max(), Floor);
            var mean = levels.Average(l => Math.Max(l / max, Floor));
            var db = mean < 1e-9 ? -90 : 10 * Math.Log10(mean);
            return 0.5 + 0.5 * Math.Tanh(-1 + 2 * (db - SqueezeLow) / (SqueezeHigh - SqueezeLow));
        }
    }
}
