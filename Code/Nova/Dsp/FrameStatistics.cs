namespace Nova.Dsp
{
    /// <summary>
    /// Per-dimension summary of a frame-wise descriptor, with the statistics and names AcousticBrainz stores:
    /// population variance; d* are statistics of |first difference| and d*2 of |second difference|;
    /// Cov / Icov (only for MFCC-style descriptors) are the population covariance and its inverse.
    /// </summary>
    public sealed class DescriptorStats
    {
        public required double[] Mean { get; init; }
        public required double[] Var { get; init; }
        public required double[] Median { get; init; }
        public required double[] Min { get; init; }
        public required double[] Max { get; init; }
        public required double[] Dmean { get; init; }
        public required double[] Dvar { get; init; }
        public required double[] Dmean2 { get; init; }
        public required double[] Dvar2 { get; init; }
        public double[,]? Cov { get; init; }
        public double[,]? Icov { get; init; }

        public int Dimensions => Mean.Length;

        public static DescriptorStats FromFrames(IReadOnlyList<double[]> frames, bool covariance = false)
        {
            var dims = frames.Count > 0 ? frames[0].Length : 0;
            var values = new double[frames.Count * dims];
            for (var t = 0; t < frames.Count; t++)
            {
                frames[t].CopyTo(values, t * dims);
            }
            return FromValues(values, dims, covariance);
        }

        /// <summary>Statistics of frames stored back to back: frame t is values[t·dims .. (t+1)·dims).</summary>
        public static DescriptorStats FromValues(ReadOnlySpan<double> values, int dims, bool covariance = false)
        {
            var n = dims > 0 ? values.Length / dims : 0;
            var cov = covariance && n > 0 ? Covariance(values, n, dims) : null;
            var stats = new DescriptorStats
            {
                Mean = new double[dims], Var = new double[dims], Median = new double[dims],
                Min = new double[dims], Max = new double[dims], Dmean = new double[dims],
                Dvar = new double[dims], Dmean2 = new double[dims], Dvar2 = new double[dims],
                Cov = cov,
                Icov = cov != null ? Invert(cov) : null,
            };

            var column = new double[n];
            var d1 = new double[Math.Max(n - 1, 0)];
            var d2 = new double[Math.Max(n - 2, 0)];
            for (var d = 0; d < dims; d++)
            {
                for (var t = 0; t < n; t++)
                {
                    column[t] = values[t * dims + d];
                }
                (stats.Mean[d], stats.Var[d]) = MeanVar(column);
                stats.Min[d] = n > 0 ? column.Min() : 0;
                stats.Max[d] = n > 0 ? column.Max() : 0;

                for (var t = 0; t + 1 < n; t++)
                {
                    d1[t] = Math.Abs(column[t + 1] - column[t]);
                }
                for (var t = 0; t + 2 < n; t++)
                {
                    d2[t] = Math.Abs(column[t + 2] - 2 * column[t + 1] + column[t]);
                }
                (stats.Dmean[d], stats.Dvar[d]) = MeanVar(d1);
                (stats.Dmean2[d], stats.Dvar2[d]) = MeanVar(d2);
                // Rearranges the column, so it comes last.
                stats.Median[d] = Selection.Median(column);
            }
            return stats;
        }

        private static (double mean, double var) MeanVar(double[] x)
        {
            if (x.Length == 0)
            {
                return (0, 0);
            }
            var mean = 0.0;
            foreach (var v in x)
            {
                mean += v;
            }
            mean /= x.Length;
            var var = 0.0;
            foreach (var v in x)
            {
                var += (v - mean) * (v - mean);
            }
            return (mean, var / x.Length);
        }

        private static double[,] Covariance(ReadOnlySpan<double> values, int n, int dims)
        {
            var mean = new double[dims];
            for (var t = 0; t < n; t++)
            {
                for (var i = 0; i < dims; i++)
                {
                    mean[i] += values[t * dims + i] / n;
                }
            }
            var cov = new double[dims, dims];
            for (var t = 0; t < n; t++)
            {
                var f = values.Slice(t * dims, dims);
                for (var i = 0; i < dims; i++)
                {
                    for (var j = 0; j < dims; j++)
                    {
                        cov[i, j] += (f[i] - mean[i]) * (f[j] - mean[j]) / n;
                    }
                }
            }
            return cov;
        }

        /// <summary>Gauss-Jordan inversion with partial pivoting; returns null for a singular matrix.</summary>
        private static double[,]? Invert(double[,] m)
        {
            var size = m.GetLength(0);
            var a = (double[,])m.Clone();
            var inv = new double[size, size];
            for (var i = 0; i < size; i++)
            {
                inv[i, i] = 1;
            }
            for (var col = 0; col < size; col++)
            {
                var pivot = col;
                for (var r = col + 1; r < size; r++)
                {
                    if (Math.Abs(a[r, col]) > Math.Abs(a[pivot, col]))
                    {
                        pivot = r;
                    }
                }
                if (Math.Abs(a[pivot, col]) < 1e-300)
                {
                    return null;
                }
                for (var c = 0; c < size; c++)
                {
                    (a[col, c], a[pivot, c]) = (a[pivot, c], a[col, c]);
                    (inv[col, c], inv[pivot, c]) = (inv[pivot, c], inv[col, c]);
                }
                var p = a[col, col];
                for (var c = 0; c < size; c++)
                {
                    a[col, c] /= p;
                    inv[col, c] /= p;
                }
                for (var r = 0; r < size; r++)
                {
                    if (r == col)
                    {
                        continue;
                    }
                    var factor = a[r, col];
                    for (var c = 0; c < size; c++)
                    {
                        a[r, c] -= factor * a[col, c];
                        inv[r, c] -= factor * inv[col, c];
                    }
                }
            }
            return inv;
        }
    }
}
