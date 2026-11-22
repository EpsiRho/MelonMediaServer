namespace Nova.Temporal
{
    /// <summary>
    /// The rhythm in one small picture: every bar folded onto one, 16 steps by the four <see cref="BandOnsets"/> bands,
    /// plus swing and syncopation.
    /// <list type="bullet">
    /// <item>Folding: step j of a beat sits at the beat's time plus j/4 of its length, so tempo drift doesn't smear the
    /// pattern; its value is the band's strongest onset within half a step either side, which tolerates human timing.</item>
    /// <item>Swing: where the off-beat onset peaks inside each beat, as a fraction p of the beat (high-mid plus high
    /// bands, local maxima only, parabolic interpolation). Eighths look in [0.42, 0.72] and give p / (1 − p): 1.0 is
    /// straight, 2.0 triplet swing. Sixteenths look around 0.25 and 0.75. Medians over the beats with a clear peak: one
    /// that reaches a quarter of the curve's 90th percentile and twice the beat's own median, so the busy, flat onset
    /// curve of beatless music doesn't read as swing.</item>
    /// <item>Clarity: how uneven the bar is, the coefficient of variation of each band's 16 steps, weighted by band.
    /// Near 0 for a flat pattern (no rhythm to speak of: ambient, noise, a wandering beat track), around 1 for a clear
    /// groove.</item>
    /// <item>Syncopation (after Longuet-Higgins and Lee): strength on a weak step that the next stronger step doesn't
    /// match, weighted by how many metrical levels apart they are, over the total; 0 when everything is on the beat.</item>
    /// </list>
    /// </summary>
    /// <param name="Steps">16 × 4, step-major (step s, band b at s·4 + b); each band's 16 values sum to 1.</param>
    /// <param name="BandWeights">Share of all onset strength per band.</param>
    /// <param name="Variation">Mean of 1 − cosine between each bar's pattern and the mean pattern.</param>
    /// <param name="Syncopation">Per band.</param>
    /// <param name="SyncopationByPhase">
    /// SyncopationOverall if the bar started k beats later (k = 0 to 3; k = 0 is SyncopationOverall), so a model that
    /// rotates the pattern to allow for a wrong downbeat can take the matching syncopation instead of a stale one.
    /// </param>
    /// <param name="Steps48">
    /// The same fold at 48 steps (12 per beat), so straight sixteenths (every 3 steps) and triplets (every 4) both land
    /// on steps; 16 steps can't place triplets and 24 can't place sixteenths.
    /// </param>
    /// <param name="BarSteps">Every bar's own 16 × 4 pattern, bar-major, before normalising, so a changing groove survives.</param>
    public sealed record OnsetPattern(
        double[] Steps,
        double[] BandWeights,
        double Variation,
        double Clarity,
        double Swing8,
        double Swing8Confidence,
        double Swing16,
        double Swing16Confidence,
        double[] Syncopation,
        double SyncopationOverall,
        int Bars,
        double[] SyncopationByPhase,
        double[] Steps48,
        double[] BarSteps)
    {
        public const int StepCount = 16;
        public const int StepsPerBeat = 4;
        public const int FineStepCount = 48;
        public const int FineStepsPerBeat = 12;
        private const int Bands = BandOnsets.Bands;

        // Metrical level of each sixteenth in a 4/4 bar: 0 the bar start, 1 beat 3, 2 beats 2 and 4, 3 eighths, 4 sixteenths.
        private static readonly int[] Level = { 0, 4, 3, 4, 2, 4, 3, 4, 1, 4, 3, 4, 2, 4, 3, 4 };

        // A beat's off-beat peak must reach this share of the 90th percentile of the swing onset function, and this
        // multiple of the beat's own median.
        private const double PeakShare = 0.25;
        private const double PeakContrast = 2;

        public double At(int step, int band) => Steps[step * Bands + band];

        public static OnsetPattern Empty { get; } = new(new double[StepCount * Bands], new double[Bands], 0, 0, 1, 0, 1, 0,
            new double[Bands], 0, 0, new double[4], new double[FineStepCount * Bands], Array.Empty<double>());

        public static OnsetPattern Compute(BeatGrid grid, FrameSeries onsets)
        {
            var beats = grid.Beats;
            if (grid.Bars == 0 || onsets.Count == 0)
            {
                return Empty;
            }

            var sum = new double[StepCount * Bands];
            var fineSum = new double[FineStepCount * Bands];
            var bars = new List<double[]>();
            foreach (var start in grid.BarStarts)
            {
                var bar = Fold(grid, beats, onsets, start, StepsPerBeat);
                bars.Add(bar);
                for (var i = 0; i < sum.Length; i++)
                {
                    sum[i] += bar[i];
                }
                var fine = Fold(grid, beats, onsets, start, FineStepsPerBeat);
                for (var i = 0; i < fineSum.Length; i++)
                {
                    fineSum[i] += fine[i];
                }
            }
            var mean = sum.Select(v => v / bars.Count).ToArray();

            var variation = bars.Where(b => b.Any(v => v > 0)).Select(b => 1 - BeatGrid.Cosine(b, mean)).DefaultIfEmpty(0).Average();

            var total = mean.Sum();
            var weights = new double[Bands];
            var steps = new double[StepCount * Bands];
            var syncopation = new double[Bands];
            var shapeSum = new double[StepCount];
            var clarity = 0.0;
            for (var b = 0; b < Bands; b++)
            {
                var band = new double[StepCount];
                for (var s = 0; s < StepCount; s++)
                {
                    band[s] = mean[s * Bands + b];
                }
                var bandTotal = band.Sum();
                weights[b] = total > 0 ? bandTotal / total : 0;
                for (var s = 0; s < StepCount; s++)
                {
                    steps[s * Bands + b] = bandTotal > 0 ? band[s] / bandTotal : 0;
                    shapeSum[s] += steps[s * Bands + b];
                }
                syncopation[b] = Syncopate(band);
                if (bandTotal > 0)
                {
                    var bandMean = bandTotal / StepCount;
                    var sd = Math.Sqrt(band.Sum(v => (v - bandMean) * (v - bandMean)) / StepCount);
                    clarity += weights[b] * sd / bandMean;
                }
            }

            var byPhase = new double[4];
            var rotated = new double[StepCount];
            for (var k = 0; k < 4; k++)
            {
                for (var s = 0; s < StepCount; s++)
                {
                    rotated[s] = shapeSum[(s + k * StepsPerBeat) % StepCount];
                }
                byPhase[k] = Syncopate(rotated);
            }

            var (swing8, confidence8, swing16, confidence16) = Swing(beats, onsets);
            return new OnsetPattern(steps, weights, variation, clarity, swing8, confidence8, swing16, confidence16, syncopation,
                byPhase[0], bars.Count, byPhase, NormaliseBands(fineSum, FineStepCount), bars.SelectMany(b => b).ToArray());
        }

        /// <summary>
        /// One bar folded onto 4 · stepsPerBeat steps: step j of a beat sits at the beat's time plus j / stepsPerBeat of
        /// its length, and takes each band's strongest onset within half a step either side.
        /// </summary>
        private static double[] Fold(BeatGrid grid, double[] beats, FrameSeries onsets, int start, int stepsPerBeat)
        {
            var bar = new double[4 * stepsPerBeat * Bands];
            for (var k = 0; k < grid.BeatsPerBar && k < 4; k++)
            {
                var t0 = beats[start + k];
                var length = BeatLength(beats, start + k);
                for (var j = 0; j < stepsPerBeat; j++)
                {
                    var centre = t0 + j * length / stepsPerBeat;
                    var half = length / stepsPerBeat / 2;
                    var (first, stop) = onsets.Range(centre - half, centre + half);
                    var step = k * stepsPerBeat + j;
                    for (var t = first; t < stop; t++)
                    {
                        var frame = onsets.Frame(t);
                        for (var b = 0; b < Bands; b++)
                        {
                            bar[step * Bands + b] = Math.Max(bar[step * Bands + b], frame[b]);
                        }
                    }
                }
            }
            return bar;
        }

        /// <summary>Each band's values over the steps scaled to sum to 1 (all 0 for a silent band).</summary>
        private static double[] NormaliseBands(double[] pattern, int steps)
        {
            var output = new double[pattern.Length];
            for (var b = 0; b < Bands; b++)
            {
                var total = 0.0;
                for (var s = 0; s < steps; s++)
                {
                    total += pattern[s * Bands + b];
                }
                for (var s = 0; s < steps && total > 0; s++)
                {
                    output[s * Bands + b] = pattern[s * Bands + b] / total;
                }
            }
            return output;
        }

        private static double BeatLength(double[] beats, int i)
            => i + 1 < beats.Length ? beats[i + 1] - beats[i] : beats[i] - beats[i - 1];

        /// <summary>For each step, the shortfall of the next stronger step (cyclic), times the level gap, over 4 × the total.</summary>
        public static double Syncopate(double[] pattern)
        {
            var total = pattern.Sum();
            if (total <= 0)
            {
                return 0;
            }
            var score = 0.0;
            for (var s = 0; s < StepCount; s++)
            {
                if (Level[s] == 0)
                {
                    continue;
                }
                var t = (s + 1) % StepCount;
                while (Level[t] >= Level[s])
                {
                    t = (t + 1) % StepCount;
                }
                score += Math.Max(0, pattern[s] - pattern[t]) * (Level[s] - Level[t]);
            }
            return score / (4 * total);
        }

        private static (double Swing8, double Confidence8, double Swing16, double Confidence16) Swing(double[] beats, FrameSeries onsets)
        {
            // The off-beats are mostly hi-hats, rides and snare ghosts: the two upper bands.
            var curve = new double[onsets.Count];
            for (var t = 0; t < curve.Length; t++)
            {
                var frame = onsets.Frame(t);
                curve[t] = frame[2] + frame[3];
            }
            var sorted = curve.Where(v => v > 0).OrderBy(v => v).ToArray();
            var threshold = sorted.Length > 0 ? PeakShare * sorted[(int)(0.9 * (sorted.Length - 1))] : double.PositiveInfinity;

            var eighths = new List<double>();
            var sixteenths = new List<double>();
            var usable = 0;
            for (var i = 0; i + 1 < beats.Length; i++)
            {
                var t0 = beats[i];
                var length = beats[i + 1] - beats[i];
                if (length <= 0)
                {
                    continue;
                }
                usable++;
                var floor = Math.Max(threshold, PeakContrast * BeatMedian(curve, onsets.HopSeconds, t0, length));
                if (Peak(curve, onsets.HopSeconds, t0, length, 0.42, 0.72, floor) is { } p8)
                {
                    eighths.Add(p8 / (1 - p8));
                }
                if (Peak(curve, onsets.HopSeconds, t0, length, 0.17, 0.36, floor) is { } p16a)
                {
                    sixteenths.Add(p16a / (0.5 - p16a));
                }
                if (Peak(curve, onsets.HopSeconds, t0, length, 0.67, 0.86, floor) is { } p16b)
                {
                    sixteenths.Add((p16b - 0.5) / (1 - p16b));
                }
            }
            if (usable == 0)
            {
                return (1, 0, 1, 0);
            }
            return (eighths.Count > 0 ? Median(eighths) : 1, (double)eighths.Count / usable,
                sixteenths.Count > 0 ? Median(sixteenths) : 1, Math.Min(1, sixteenths.Count / (2.0 * usable)));
        }

        private static double BeatMedian(double[] curve, double hop, double t0, double length)
        {
            var first = Math.Max(0, (int)Math.Ceiling(t0 / hop));
            var stop = Math.Min(curve.Length, (int)Math.Ceiling((t0 + length) / hop));
            return stop > first ? Median(curve[first..stop].ToList()) : 0;
        }

        /// <summary>
        /// The strongest local maximum of the curve whose time lies in [low, high] of the beat, as a fraction of the beat,
        /// refined by a parabola through it and its neighbours; null when there's none above the threshold.
        /// </summary>
        private static double? Peak(double[] curve, double hop, double t0, double length, double low, double high, double threshold)
        {
            var first = Math.Max(1, (int)Math.Ceiling((t0 + low * length) / hop));
            var last = Math.Min(curve.Length - 2, (int)Math.Floor((t0 + high * length) / hop));
            var best = -1;
            for (var t = first; t <= last; t++)
            {
                if (curve[t] >= threshold && curve[t] > curve[t - 1] && curve[t] >= curve[t + 1] && (best < 0 || curve[t] > curve[best]))
                {
                    best = t;
                }
            }
            if (best < 0)
            {
                return null;
            }
            double a = curve[best - 1], b = curve[best], c = curve[best + 1];
            var denominator = a - 2 * b + c;
            var offset = denominator < 0 ? 0.5 * (a - c) / denominator : 0;
            var position = ((best + offset) * hop - t0) / length;
            return Math.Clamp(position, low, high);
        }

        private static double Median(List<double> values)
        {
            var sorted = values.OrderBy(v => v).ToArray();
            var m = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[m] : 0.5 * (sorted[m - 1] + sorted[m]);
        }
    }
}
