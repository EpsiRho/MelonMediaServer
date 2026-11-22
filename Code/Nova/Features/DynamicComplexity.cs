using Nova.Audio;

namespace Nova.Features
{
    /// <summary>
    /// AcousticBrainz's lowlevel.dynamic_complexity: the mean absolute deviation, in dB, of a smoothed loudness curve
    /// from its loudness-weighted average. Loudness is a 35 ms one-pole average of the high-passed signal's energy,
    /// read every 200 ms; silent frames at either end are dropped.
    /// </summary>
    public static class DynamicComplexity
    {
        private const double FrameSeconds = 0.2;
        private const double SmoothingSeconds = 0.035;
        private const double SilenceDb = -90;

        public static double Compute(MonoSignal signal)
        {
            var x = signal.Samples;
            var frameSize = (int)(FrameSeconds * signal.SampleRate);
            var frames = x.Length / frameSize;
            if (frames == 0)
            {
                return 0;
            }
            var c = Math.Exp(-1 / (SmoothingSeconds * signal.SampleRate));

            // Within a frame the smoother's output at the frame end weights sample j by c^(frameSize-1-j).
            var weights = new double[frameSize];
            for (var j = 0; j < frameSize; j++)
            {
                weights[j] = Math.Pow(c, frameSize - 1 - j);
            }
            var carry = Math.Pow(c, frameSize);

            var levels = new List<double>(frames);
            double state = 0, previousIn = 0, previousOut = 0;
            for (var f = 0; f < frames; f++)
            {
                var sum = 0.0;
                for (var j = 0; j < frameSize; j++)
                {
                    // First-order high-pass pre-filter.
                    double input = x[f * frameSize + j];
                    var output = 0.98595 * (input - previousIn) + 0.9719 * previousOut;
                    previousIn = input;
                    previousOut = output;
                    sum += weights[j] * output * output;
                }
                state = carry * state + (1 - c) * sum;
                levels.Add(state < 1e-9 ? SilenceDb : 10 * Math.Log10(state));
            }

            var first = levels.FindIndex(l => l != SilenceDb);
            if (first < 0)
            {
                return 0;
            }
            var last = levels.FindLastIndex(l => l != SilenceDb);
            var kept = levels.GetRange(first, last - first + 1);

            // Louder frames weigh more: each frame's weight is 0.9^(-L).
            double weightSum = 0, weighted = 0;
            foreach (var l in kept)
            {
                var w = Math.Pow(0.9, -l);
                weightSum += w;
                weighted += w * l;
            }
            var average = weighted / weightSum;
            return kept.Average(l => Math.Abs(l - average));
        }
    }
}
