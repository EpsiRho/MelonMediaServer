namespace Nova.Temporal
{
    /// <summary>A section between two novelty peaks, in bars and seconds.</summary>
    public sealed record Section(int StartBar, int Bars, double Start, double Length);

    /// <summary>
    /// The shape of a track over time, per bar (the mean of its beats):
    /// <list type="bullet">
    /// <item>Self-similarity: cosine between every pair of bars, for harmony (chroma) and for timbre (MFCC, each
    /// coefficient standardised over the track so similarity reflects change within it), resized to 64 × 64.</item>
    /// <item>Novelty: Foote's checkerboard kernel, Gaussian-tapered, 8 bars each side (fewer for short tracks), slid along
    /// the diagonal of the mean of the two matrices; it peaks where the bars before and after are each self-similar but
    /// different from each other.</item>
    /// <item>Sections: novelty peaks above the mean plus half a standard deviation, at least 4 bars apart, strongest
    /// first.</item>
    /// <item>Energy: per-bar loudness relative to the track's median, and per-bar onset strength (all bands) over its
    /// track mean, resized to 64 points. The second is relative onset strength, not a count of onsets per second, and
    /// both are after replay-gain normalisation, so neither is the recording's own loudness.</item>
    /// </list>
    /// Matrices are row-major. Absolute length survives resizing only through Bars and the sections. Only complete bars
    /// count: a pickup before the first bar and a partial bar at the end are left out (RhythmValidity's LeadIn and
    /// Tail say how long they are, and ClockSeries covers them). Cosine similarity can't tell a progression from its
    /// mirror image (negating every timbre vector leaves the matrix unchanged), so the ordered per-beat rows in
    /// BeatSequence are kept next to it.
    /// </summary>
    public sealed record BarStructure(
        int Bars,
        double[] ChromaSimilarity,
        double[] TimbreSimilarity,
        double[] Novelty,
        Section[] Sections,
        double[] EnergyCurve,
        double[] OnsetDensityCurve,
        double EnergyLevel,
        double EnergyRange)
    {
        public const int MatrixSize = 64;
        public const int CurvePoints = 64;
        private const int KernelBars = 8;
        private const int MinSectionBars = 4;

        public static BarStructure Empty { get; } = new(0, new double[MatrixSize * MatrixSize], new double[MatrixSize * MatrixSize],
            Array.Empty<double>(), Array.Empty<Section>(), new double[CurvePoints], new double[CurvePoints], 0, 0);

        public static BarStructure Compute(BeatGrid grid, BeatSequence beats)
        {
            var n = grid.Bars;
            if (n < 2)
            {
                return Empty;
            }

            // Per-track standardisation of each MFCC over the beats.
            var mfccMean = new double[BeatSequence.MfccDims];
            var mfccSd = new double[BeatSequence.MfccDims];
            for (var d = 0; d < BeatSequence.MfccDims; d++)
            {
                double sum = 0, squares = 0;
                for (var i = 0; i < beats.Count; i++)
                {
                    var v = beats.MfccAt(i)[d];
                    sum += v;
                    squares += v * v;
                }
                mfccMean[d] = sum / beats.Count;
                mfccSd[d] = Math.Sqrt(Math.Max(0, squares / beats.Count - mfccMean[d] * mfccMean[d]));
            }

            var chroma = new double[n * BeatSequence.ChromaDims];
            var timbre = new double[n * BeatSequence.MfccDims];
            var loudness = new double[n];
            var density = new double[n];
            for (var bar = 0; bar < n; bar++)
            {
                var start = grid.BarStarts[bar];
                var power = 0.0;
                for (var k = 0; k < grid.BeatsPerBar; k++)
                {
                    var i = start + k;
                    var c = beats.ChromaAt(i);
                    for (var d = 0; d < BeatSequence.ChromaDims; d++)
                    {
                        chroma[bar * BeatSequence.ChromaDims + d] += c[d] / grid.BeatsPerBar;
                    }
                    var m = beats.MfccAt(i);
                    for (var d = 0; d < BeatSequence.MfccDims; d++)
                    {
                        var z = mfccSd[d] > 1e-12 ? (m[d] - mfccMean[d]) / mfccSd[d] : 0;
                        timbre[bar * BeatSequence.MfccDims + d] += z / grid.BeatsPerBar;
                    }
                    power += Math.Pow(10, beats.Loudness[i] / 10) / grid.BeatsPerBar;
                    foreach (var o in beats.OnsetAt(i))
                    {
                        density[bar] += o / grid.BeatsPerBar;
                    }
                }
                loudness[bar] = power < 1e-10 ? -100 : 10 * Math.Log10(power);
            }

            // No n × n matrix is ever held: a long mix has thousands of bars (three dense matrices at 10,000 bars took
            // 2.4 GB). The resized views are built a row at a time, and novelty only needs bars near the diagonal.
            double Combined(int i, int j) => 0.5 * (
                BeatGrid.Cosine(chroma.AsSpan(i * BeatSequence.ChromaDims, BeatSequence.ChromaDims), chroma.AsSpan(j * BeatSequence.ChromaDims, BeatSequence.ChromaDims)) +
                BeatGrid.Cosine(timbre.AsSpan(i * BeatSequence.MfccDims, BeatSequence.MfccDims), timbre.AsSpan(j * BeatSequence.MfccDims, BeatSequence.MfccDims)));
            var novelty = NoveltyCurve(Combined, n);
            var sections = FindSections(novelty, grid, beats);

            var sortedLoudness = loudness.OrderBy(v => v).ToArray();
            var level = TempoStability.Median(loudness);
            var meanDensity = density.Average();
            return new BarStructure(
                n,
                ResizedSimilarity(chroma, n, BeatSequence.ChromaDims),
                ResizedSimilarity(timbre, n, BeatSequence.MfccDims),
                novelty,
                sections,
                Resample.Linear(loudness.Select(l => l - level).ToArray(), CurvePoints),
                Resample.Linear(density.Select(d => meanDensity > 0 ? d / meanDensity : 0).ToArray(), CurvePoints),
                level,
                TempoStability.Percentile(sortedLoudness, 0.95) - TempoStability.Percentile(sortedLoudness, 0.05));
        }

        /// <summary>
        /// The bar-by-bar cosine matrix resized to MatrixSize × MatrixSize, exactly as <see cref="Resample.Matrix"/> would,
        /// but computing one row at a time: memory grows with n × MatrixSize instead of n × n.
        /// </summary>
        private static double[] ResizedSimilarity(double[] features, int n, int dims)
        {
            var rows = new double[n * MatrixSize];
            var row = new double[n];
            for (var i = 0; i < n; i++)
            {
                var a = features.AsSpan(i * dims, dims);
                for (var j = 0; j < n; j++)
                {
                    row[j] = BeatGrid.Cosine(a, features.AsSpan(j * dims, dims));
                }
                Resample.Linear(row, MatrixSize).CopyTo(rows, i * MatrixSize);
            }
            return Resample.Columns(rows, n, MatrixSize);
        }

        private static double[] NoveltyCurve(Func<int, int, double> ssm, int n)
        {
            var half = Math.Max(1, Math.Min(KernelBars, n / 4));
            var sigma = half / 2.0;
            var taper = new double[2 * half];
            for (var k = 0; k < 2 * half; k++)
            {
                var distance = k - half + 0.5;
                taper[k] = Math.Exp(-distance * distance / (2 * sigma * sigma));
            }
            var novelty = new double[n];
            for (var c = 0; c < n; c++)
            {
                // Rows and columns c-half .. c+half-1: "before" is below c, "after" from c on.
                double sum = 0, weight = 0;
                for (var a = 0; a < 2 * half; a++)
                {
                    var i = c - half + a;
                    if (i < 0 || i >= n)
                    {
                        continue;
                    }
                    for (var b = 0; b < 2 * half; b++)
                    {
                        var j = c - half + b;
                        if (j < 0 || j >= n)
                        {
                            continue;
                        }
                        var sign = (a < half) == (b < half) ? 1 : -1;
                        var w = taper[a] * taper[b];
                        sum += sign * w * ssm(i, j);
                        weight += w;
                    }
                }
                novelty[c] = weight > 0 ? Math.Max(0, sum / weight) : 0;
            }
            novelty[0] = 0; // the start of the track isn't a change
            return novelty;
        }

        private static Section[] FindSections(double[] novelty, BeatGrid grid, BeatSequence beats)
        {
            var n = novelty.Length;
            var mean = novelty.Average();
            var sd = Math.Sqrt(novelty.Sum(v => (v - mean) * (v - mean)) / n);
            var candidates = new List<int>();
            for (var c = 1; c < n; c++)
            {
                var left = novelty[c - 1];
                var right = c + 1 < n ? novelty[c + 1] : double.NegativeInfinity;
                if (novelty[c] > left && novelty[c] >= right && novelty[c] > mean + 0.5 * sd)
                {
                    candidates.Add(c);
                }
            }
            var accepted = new List<int>();
            foreach (var c in candidates.OrderByDescending(c => novelty[c]))
            {
                if (c >= MinSectionBars && n - c >= MinSectionBars && accepted.All(a => Math.Abs(a - c) >= MinSectionBars))
                {
                    accepted.Add(c);
                }
            }
            accepted.Sort();
            var bounds = new List<int> { 0 };
            bounds.AddRange(accepted);
            bounds.Add(n);

            double BarTime(int bar) => bar < n
                ? beats.Time[grid.BarStarts[bar]]
                : beats.Time[grid.BarStarts[n - 1] + grid.BeatsPerBar - 1] + beats.Duration[grid.BarStarts[n - 1] + grid.BeatsPerBar - 1];

            var sections = new Section[bounds.Count - 1];
            for (var s = 0; s + 1 < bounds.Count; s++)
            {
                var start = BarTime(bounds[s]);
                sections[s] = new Section(bounds[s], bounds[s + 1] - bounds[s], start, BarTime(bounds[s + 1]) - start);
            }
            return sections;
        }
    }
}
