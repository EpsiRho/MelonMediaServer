using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nova.Dsp
{
    /// <summary>
    /// ReplayGain's equal-loudness curve at 44.1 kHz: a 10th-order Yule-Walk IIR followed by a 2nd-order
    /// Butterworth high-pass at 150 Hz. The coefficients are the published ReplayGain values.
    /// </summary>
    public static class EqualLoudness
    {
        private static readonly double[] YuleB =
        {
            0.05418656406430, -0.02911007808948, -0.00848709379851, -0.00851165645469, -0.00834990904936,
            0.02245293253339, -0.02596338512915, 0.01624864962975, -0.00240879051584, 0.00674613682247,
            -0.00187763777362,
        };

        private static readonly double[] YuleA =
        {
            1.00000000000000, -3.47845948550071, 6.36317777566148, -8.54751527471874, 9.47693607801280,
            -8.81498681370155, 6.85401540936998, -4.39470996079559, 2.19611684890774, -0.75104302451432,
            0.13149317958808,
        };

        private static readonly double[] ButterB = { 0.98500175787242, -1.97000351574484, 0.98500175787242 };
        private static readonly double[] ButterA = { 1.00000000000000, -1.96977855582618, 0.97022847566350 };

        /// <summary>
        /// Both filters in one pass, direct form I with zero initial state. After the first samples, the
        /// coefficients and the filter history are kept in locals, so nothing but the output is allocated and the
        /// loop has no bounds checks.
        /// </summary>
        public static float[] Apply(ReadOnlySpan<float> input)
        {
            var output = new float[input.Length];
            var head = Math.Min(input.Length, YuleB.Length);
            var yule = new double[YuleB.Length];
            double z1 = 0, z2 = 0; // Butterworth outputs n-1 and n-2

            // Samples whose history reaches before the start, where the missing terms are zero.
            for (var n = 0; n < head; n++)
            {
                var acc = 0.0;
                for (var k = 0; k <= n; k++)
                {
                    acc += YuleB[k] * input[n - k];
                }
                for (var k = n; k >= 1; k--)
                {
                    acc -= YuleA[k] * yule[n - k];
                }
                yule[n] = acc;
                var y = ButterB[0] * acc + ButterB[1] * (n >= 1 ? yule[n - 1] : 0) + ButterB[2] * (n >= 2 ? yule[n - 2] : 0)
                    - ButterA[2] * z2 - ButterA[1] * z1;
                (z2, z1) = (z1, y);
                output[n] = (float)y;
            }
            if (input.Length <= head)
            {
                return output;
            }

            double b0 = YuleB[0], b1 = YuleB[1], b2 = YuleB[2], b3 = YuleB[3], b4 = YuleB[4], b5 = YuleB[5];
            double b6 = YuleB[6], b7 = YuleB[7], b8 = YuleB[8], b9 = YuleB[9], b10 = YuleB[10];
            double a1 = YuleA[1], a2 = YuleA[2], a3 = YuleA[3], a4 = YuleA[4], a5 = YuleA[5];
            double a6 = YuleA[6], a7 = YuleA[7], a8 = YuleA[8], a9 = YuleA[9], a10 = YuleA[10];
            double c0 = ButterB[0], c1 = ButterB[1], c2 = ButterB[2], d1 = ButterA[1], d2 = ButterA[2];
            // Yule-Walk outputs n-1 .. n-10.
            double y1 = yule[head - 1], y2 = yule[head - 2], y3 = yule[head - 3], y4 = yule[head - 4], y5 = yule[head - 5];
            double y6 = yule[head - 6], y7 = yule[head - 7], y8 = yule[head - 8], y9 = yule[head - 9], y10 = yule[head - 10];
            ref var x = ref MemoryMarshal.GetReference(input);
            for (var n = head; n < input.Length; n++)
            {
                // Pairwise sums keep the dependency chain short; only a1·y1 depends on the previous sample.
                var fir = ((b0 * Unsafe.Add(ref x, n) + b1 * Unsafe.Add(ref x, n - 1)) + (b2 * Unsafe.Add(ref x, n - 2) + b3 * Unsafe.Add(ref x, n - 3)))
                    + ((b4 * Unsafe.Add(ref x, n - 4) + b5 * Unsafe.Add(ref x, n - 5)) + (b6 * Unsafe.Add(ref x, n - 6) + b7 * Unsafe.Add(ref x, n - 7)))
                    + ((b8 * Unsafe.Add(ref x, n - 8) + b9 * Unsafe.Add(ref x, n - 9)) + b10 * Unsafe.Add(ref x, n - 10));
                var feedback = ((a10 * y10 + a9 * y9) + (a8 * y8 + a7 * y7)) + ((a6 * y6 + a5 * y5) + (a4 * y4 + a3 * y3)) + a2 * y2;
                var acc = fir - feedback - a1 * y1;
                var y = c0 * acc + c1 * y1 + c2 * y2 - d2 * z2 - d1 * z1;
                (z2, z1) = (z1, y);
                (y10, y9, y8, y7, y6, y5, y4, y3, y2, y1) = (y9, y8, y7, y6, y5, y4, y3, y2, y1, acc);
                output[n] = (float)y;
            }
            return output;
        }
    }
}
