using Nova.Dsp;

namespace Nova.Features
{
    /// <summary>
    /// Sensory dissonance of a set of spectral peaks (Plomp and Levelt's roughness curve over critical bandwidths),
    /// with peak magnitudes weighted by the A-weighting curve squared, following Essentia.
    /// Peaks must be in ascending frequency; peaks at or below 50 Hz and partners at or above 10 kHz are ignored.
    /// </summary>
    public static class Dissonance
    {
        private const double MaxBandwidths = 1.18;

        public static double Compute(IReadOnlyList<SpectralPeak> peaks)
        {
            var n = peaks.Count;
            var loudness = new double[n];
            var bandwidths = new double[n];
            var total = 0.0;
            for (var i = 0; i < n; i++)
            {
                var a = AWeighting(peaks[i].Frequency);
                loudness[i] = peaks[i].Magnitude * a * a;
                bandwidths[i] = CriticalBandwidth(HzToBark(peaks[i].Frequency));
                total += loudness[i];
            }
            if (total == 0)
            {
                return 0;
            }

            var dissonance = 0.0;
            for (var p1 = 0; p1 < n; p1++)
            {
                var f1 = peaks[p1].Frequency;
                if (f1 <= 50)
                {
                    continue;
                }
                var bark1 = HzToBark(f1);
                var startF = BarkToHz(bark1 - 1.18);
                var endF = BarkToHz(bark1 + 1.18);

                var p2 = 0;
                while (p2 < n && peaks[p2].Frequency < startF && peaks[p2].Frequency < 50)
                {
                    p2++;
                }
                // Partners further than 1.18 critical bandwidths (of either peak, so at most f1's) are fully
                // consonant and add exactly nothing, so the scan can skip straight past them.
                var reach = MaxBandwidths * bandwidths[p1] * (1 + 1e-9);
                p2 = Math.Max(p2, LowerBound(peaks, f1 - reach));
                var peakDissonance = 0.0;
                for (; p2 < n && peaks[p2].Frequency < endF && peaks[p2].Frequency < 10000 && peaks[p2].Frequency <= f1 + reach; p2++)
                {
                    var bandwidth = Math.Min(bandwidths[p1], bandwidths[p2]);
                    var d = 1 - Consonance(Math.Abs(peaks[p2].Frequency - f1) / bandwidth);
                    if (d > 0)
                    {
                        peakDissonance += d * (loudness[p1] + loudness[p2]) / total;
                    }
                }
                dissonance += Math.Min(peakDissonance, loudness[p1] / total);
            }
            return dissonance / 2;
        }

        /// <summary>Index of the first peak at or above the frequency.</summary>
        private static int LowerBound(IReadOnlyList<SpectralPeak> peaks, double frequency)
        {
            int lo = 0, hi = peaks.Count;
            while (lo < hi)
            {
                var mid = (lo + hi) / 2;
                if (peaks[mid].Frequency < frequency)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }
            return lo;
        }

        /// <summary>Polynomial fit of Plomp and Levelt's consonance curve, for a frequency gap in critical bandwidths.</summary>
        private static double Consonance(double df)
        {
            if (df < 0 || df > MaxBandwidths)
            {
                return 1;
            }
            var c = ((((-6.58977878 * df + 28.58224226) * df - 47.36739986) * df + 35.70679761) * df - 10.36526344) * df + 1.00026609;
            return Math.Clamp(c, 0, 1);
        }

        private static double AWeighting(double f)
        {
            var f2 = f * f;
            return 1.25893 * 12200 * 12200 * f2 * f2
                / ((f2 + 20.6 * 20.6) * (f2 + 12200 * 12200) * Math.Sqrt(f2 + 107.7 * 107.7) * Math.Sqrt(f2 + 737.9 * 737.9));
        }

        /// <summary>Traunmüller's Hz-to-Bark formula with its low and high corrections.</summary>
        private static double HzToBark(double f)
        {
            var b = 26.81 * f / (1960 + f) - 0.53;
            if (b < 2)
            {
                b += 0.15 * (2 - b);
            }
            if (b > 20.1)
            {
                b += 0.22 * (b - 20.1);
            }
            return b;
        }

        private static double BarkToHz(double z)
        {
            if (z < 2)
            {
                z = (z - 0.3) / 0.85;
            }
            if (z > 20.1)
            {
                z = (z - 4.422) / 1.22;
            }
            return 1960 * (z + 0.53) / (26.28 - z);
        }

        private static double CriticalBandwidth(double z) => 52548 / (z * z - 52.56 * z + 690.39);
    }
}
