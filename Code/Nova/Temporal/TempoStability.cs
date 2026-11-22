namespace Nova.Temporal
{
    /// <summary>
    /// How steady the tempo is, from the beat intervals. Robust statistics throughout, because one missed or extra beat
    /// would dominate a standard deviation; intervals outside 0.7–1.4 times the median are taken as tracking errors
    /// (a missed beat doubles an interval) and skipped.
    /// </summary>
    /// <param name="IbiSpread">Median absolute deviation of the intervals over their median: 0 for a drum machine.</param>
    /// <param name="Drift">Least-squares slope of local bpm against time, in bpm per minute.</param>
    /// <param name="TempoChanges">Sustained jumps: the median of the next 8 beats differs from the previous 8 by over 4%.</param>
    /// <param name="BpmCurve">Local bpm, median-filtered over 5 beats, resampled to 64 points over the track.</param>
    public sealed record TempoStability(
        double IbiSpread,
        double Drift,
        double LocalBpmLow,
        double LocalBpmHigh,
        int TempoChanges,
        double[] BpmCurve)
    {
        public const int CurvePoints = 64;
        private const int ChangeWindow = 8;
        private const double ChangeRatio = 0.04;

        public static TempoStability Empty { get; } = new(0, 0, 0, 0, 0, new double[CurvePoints]);

        public static TempoStability Compute(double[] beats)
        {
            if (beats.Length < 3)
            {
                return Empty;
            }
            var intervals = new double[beats.Length - 1];
            for (var i = 0; i < intervals.Length; i++)
            {
                intervals[i] = beats[i + 1] - beats[i];
            }
            var median = Median(intervals);
            if (median <= 0)
            {
                return Empty;
            }
            var times = new List<double>();
            var bpm = new List<double>();
            for (var i = 0; i < intervals.Length; i++)
            {
                if (intervals[i] > 0.7 * median && intervals[i] < 1.4 * median)
                {
                    times.Add(0.5 * (beats[i] + beats[i + 1]));
                    bpm.Add(60 / intervals[i]);
                }
            }
            if (bpm.Count < 3)
            {
                return Empty;
            }
            var kept = bpm.Select(b => 60 / b).ToArray();
            var keptMedian = Median(kept);
            var spread = Median(kept.Select(x => Math.Abs(x - keptMedian)).ToArray()) / keptMedian;

            // Slope of bpm against minutes, by least squares.
            var meanT = times.Average() / 60;
            var meanB = bpm.Average();
            double sxy = 0, sxx = 0;
            for (var i = 0; i < bpm.Count; i++)
            {
                var dt = times[i] / 60 - meanT;
                sxy += dt * (bpm[i] - meanB);
                sxx += dt * dt;
            }
            var drift = sxx > 0 ? sxy / sxx : 0;

            var sortedBpm = bpm.OrderBy(b => b).ToArray();
            var smooth = new double[bpm.Count];
            for (var i = 0; i < smooth.Length; i++)
            {
                smooth[i] = Median(bpm.Skip(Math.Max(0, i - 2)).Take(Math.Min(bpm.Count, i + 3) - Math.Max(0, i - 2)).ToArray());
            }

            var changes = 0;
            var lastChange = -ChangeWindow;
            for (var i = ChangeWindow; i + ChangeWindow <= smooth.Length; i++)
            {
                var before = Median(smooth.Skip(i - ChangeWindow).Take(ChangeWindow).ToArray());
                var after = Median(smooth.Skip(i).Take(ChangeWindow).ToArray());
                if (Math.Abs(after - before) > ChangeRatio * before && i - lastChange >= ChangeWindow)
                {
                    changes++;
                    lastChange = i;
                }
            }

            return new TempoStability(spread, drift, Percentile(sortedBpm, 0.05), Percentile(sortedBpm, 0.95), changes,
                Resample.Linear(smooth, CurvePoints));
        }

        internal static double Median(double[] values)
        {
            if (values.Length == 0)
            {
                return 0;
            }
            var sorted = values.OrderBy(v => v).ToArray();
            var m = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[m] : 0.5 * (sorted[m - 1] + sorted[m]);
        }

        internal static double Percentile(double[] sorted, double q)
        {
            if (sorted.Length == 0)
            {
                return 0;
            }
            var position = q * (sorted.Length - 1);
            var low = (int)Math.Floor(position);
            var high = Math.Min(sorted.Length - 1, low + 1);
            return sorted[low] + (position - low) * (sorted[high] - sorted[low]);
        }
    }
}
