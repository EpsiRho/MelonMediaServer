namespace Nova.Dsp
{
    /// <summary>
    /// Single-array statistics used on spectra and band energies, with Essentia's definitions and edge cases.
    /// </summary>
    public static class ArrayShape
    {
        /// <summary>
        /// Spread (variance), skewness and excess kurtosis of x treated as a distribution over positions
        /// i / (n - 1) * range. An all-zero array gives (0, 0, -3).
        /// </summary>
        public static (double Spread, double Skewness, double Kurtosis) Distribution(ReadOnlySpan<double> x, double range)
        {
            var n = x.Length;
            double sum = 0, weighted = 0;
            for (var i = 0; i < n; i++)
            {
                sum += x[i];
                weighted += i * x[i];
            }
            if (sum == 0 || n < 2)
            {
                return (0, 0, -3);
            }

            var step = 1.0 / (n - 1);
            var centroid = weighted * step / sum;
            double m2 = 0, m3 = 0, m4 = 0;
            for (var i = 0; i < n; i++)
            {
                var d = i * step - centroid;
                var d2 = d * d * x[i];
                m2 += d2;
                m3 += d2 * d;
                m4 += d2 * d * d;
            }
            var spread = m2 / sum * range * range;
            if (spread == 0)
            {
                return (0, 0, -3);
            }
            var skewness = m3 / sum * range * range * range / Math.Pow(spread, 1.5);
            var kurtosis = m4 / sum * Math.Pow(range, 4) / (spread * spread) - 3;
            return (spread, skewness, kurtosis);
        }

        /// <summary>Maximum over arithmetic mean; 0 for an all-zero array.</summary>
        public static double Crest(ReadOnlySpan<double> x)
        {
            double max = 0, sum = 0;
            foreach (var v in x)
            {
                max = Math.Max(max, v);
                sum += v;
            }
            return max == 0 ? 0 : max / (sum / x.Length);
        }

        /// <summary>
        /// Flatness (geometric over arithmetic mean) in dB, divided by -60 and capped at 1: 0 is noise-like,
        /// 1 is peaky. Arrays containing a zero give 1.
        /// </summary>
        public static double FlatnessDb(ReadOnlySpan<double> x)
        {
            double logSum = 0, sum = 0;
            foreach (var v in x)
            {
                if (v <= 0)
                {
                    return 1;
                }
                logSum += Math.Log(v);
                sum += v;
            }
            var flatness = Math.Exp(logSum / x.Length) / (sum / x.Length);
            var db = flatness < 1e-9 ? -90 : 10 * Math.Log10(flatness);
            return Math.Min(db / -60, 1);
        }

        /// <summary>
        /// Shannon entropy in bits of x normalised to sum to 1, using −Σ p·log2 p = log2 S − (Σ x·log2 x) / S
        /// with S = Σ x, so the logs run vectorised on x directly.
        /// </summary>
        public static double Entropy(ReadOnlySpan<double> x)
        {
            var sum = 0.0;
            foreach (var v in x)
            {
                sum += v;
            }
            if (sum == 0)
            {
                return 0;
            }
            return Math.Log2(sum) - VectorMath.SumXLog2X(x) / sum;
        }

        /// <summary>Least-squares slope of x against positions i / (n - 1) * range.</summary>
        public static double Decrease(ReadOnlySpan<double> x, double range)
        {
            var n = x.Length;
            var meanY = 0.0;
            foreach (var v in x)
            {
                meanY += v;
            }
            meanY /= n;
            var step = range / (n - 1);
            var meanX = range / 2;
            double sxx = 0, sxy = 0;
            for (var i = 0; i < n; i++)
            {
                var dx = i * step - meanX;
                sxx += dx * dx;
                sxy += dx * (x[i] - meanY);
            }
            return sxy / sxx;
        }
    }
}
