namespace Nova.Temporal
{
    /// <summary>
    /// Bars from beats: which beat starts each bar. The first version assumes 4 beats per bar. Each of the 4 possible
    /// phases is scored by how much its bar starts stand out on four cues, each standardised across beats:
    /// harmonic change (chroma of the two beats after against the two before), timbre change (the same with MFCC,
    /// leaving out MFCC 0, which is mostly level), kick accent (low-band onsets against the neighbouring beats) and
    /// backbeat (the high-mid band rising into the next beat, where snares and claps sit). On drums alone harmony says
    /// nothing; checked against E-GMD (F1) the grid finds the right beat 1 about half the time, and mostly confuses
    /// beat 1 with beat 3, which kick and snare can't tell apart.
    /// </summary>
    /// <param name="DownbeatPhase">Index of the first beat that starts a bar, 0 to 3.</param>
    /// <param name="BarStarts">Beat index of every complete bar (all its beats present).</param>
    /// <param name="DownbeatConfidence">
    /// The winning phase's margin over the runner-up in standard errors, m, mapped to m / (m + 2): 0 when two phases
    /// tie, 0.5 at two standard errors.
    /// </param>
    public sealed record BeatGrid(double[] Beats, int BeatsPerBar, int DownbeatPhase, int[] BarStarts, double DownbeatConfidence)
    {
        public const int DefaultBeatsPerBar = 4;
        private const int MinBeats = 8;

        public int Bars => BarStarts.Length;

        public static BeatGrid Compute(BeatSequence beats)
        {
            var n = beats.Count;
            const int perBar = DefaultBeatsPerBar;
            if (n < MinBeats)
            {
                return new BeatGrid(beats.Time, perBar, 0, Starts(n, 0, perBar), 0);
            }

            var harmonic = new double[n];
            var timbre = new double[n];
            var kick = new double[n];
            var backbeat = new double[n];
            var valid = new bool[n];
            Span<double> before = stackalloc double[BeatSequence.ChromaDims];
            Span<double> after = stackalloc double[BeatSequence.ChromaDims];
            Span<double> mfccBefore = stackalloc double[BeatSequence.MfccDims];
            Span<double> mfccAfter = stackalloc double[BeatSequence.MfccDims];
            for (var i = 2; i + 2 <= n; i++)
            {
                Average(beats.ChromaAt(i - 2), beats.ChromaAt(i - 1), before);
                Average(beats.ChromaAt(i), beats.ChromaAt(i + 1), after);
                harmonic[i] = 1 - Cosine(before, after);
                Average(beats.MfccAt(i - 2), beats.MfccAt(i - 1), mfccBefore);
                Average(beats.MfccAt(i), beats.MfccAt(i + 1), mfccAfter);
                var distance = 0.0;
                for (var d = 1; d < BeatSequence.MfccDims; d++)
                {
                    distance += (mfccAfter[d] - mfccBefore[d]) * (mfccAfter[d] - mfccBefore[d]);
                }
                timbre[i] = Math.Sqrt(distance);
                kick[i] = beats.OnsetAt(i)[0] - 0.5 * (beats.OnsetAt(i - 1)[0] + beats.OnsetAt(i + 1)[0]);
                // Snare and claps land on 2 and 4, so the high-mid band rises from a bar start to the beat after it.
                backbeat[i] = beats.OnsetAt(i + 1)[2] - beats.OnsetAt(i)[2];
                valid[i] = true;
            }
            foreach (var cue in new[] { harmonic, timbre, kick, backbeat })
            {
                Standardise(cue, valid);
            }

            var combined = new double[n];
            var scores = new double[perBar];
            var counts = new int[perBar];
            for (var i = 0; i < n; i++)
            {
                if (valid[i])
                {
                    combined[i] = harmonic[i] + timbre[i] + kick[i] + backbeat[i];
                    scores[i % perBar] += combined[i];
                    counts[i % perBar]++;
                }
            }
            for (var p = 0; p < perBar; p++)
            {
                scores[p] = counts[p] > 0 ? scores[p] / counts[p] : double.NegativeInfinity;
            }
            var order = Enumerable.Range(0, perBar).OrderByDescending(p => scores[p]).ToArray();
            var phase = order[0];

            // Standard error of a phase's mean, from the spread of the combined cue over all valid beats.
            var all = combined.Where((_, i) => valid[i]).ToArray();
            var mean = all.Average();
            var sd = Math.Sqrt(all.Sum(c => (c - mean) * (c - mean)) / all.Length);
            var standardError = sd / Math.Sqrt(Math.Max(1, counts.Min()));
            var margin = standardError > 0 ? (scores[order[0]] - scores[order[1]]) / standardError : 0;
            return new BeatGrid(beats.Time, perBar, phase, Starts(n, phase, perBar), margin / (margin + 2));
        }

        private static int[] Starts(int beats, int phase, int perBar)
        {
            var starts = new List<int>();
            for (var s = phase; s + perBar <= beats; s += perBar)
            {
                starts.Add(s);
            }
            return starts.ToArray();
        }

        private static void Average(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> destination)
        {
            for (var d = 0; d < destination.Length; d++)
            {
                destination[d] = 0.5 * (a[d] + b[d]);
            }
        }

        internal static double Cosine(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
        {
            double dot = 0, na = 0, nb = 0;
            for (var d = 0; d < a.Length; d++)
            {
                dot += a[d] * b[d];
                na += a[d] * a[d];
                nb += b[d] * b[d];
            }
            return na > 0 && nb > 0 ? dot / Math.Sqrt(na * nb) : 0;
        }

        private static void Standardise(double[] x, bool[] valid)
        {
            double sum = 0, count = 0;
            for (var i = 0; i < x.Length; i++)
            {
                if (valid[i])
                {
                    sum += x[i];
                    count++;
                }
            }
            var mean = sum / count;
            var variance = 0.0;
            for (var i = 0; i < x.Length; i++)
            {
                if (valid[i])
                {
                    variance += (x[i] - mean) * (x[i] - mean);
                }
            }
            var sd = Math.Sqrt(variance / count);
            for (var i = 0; i < x.Length; i++)
            {
                x[i] = valid[i] && sd > 1e-12 ? (x[i] - mean) / sd : 0;
            }
        }
    }
}
