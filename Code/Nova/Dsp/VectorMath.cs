using System.Numerics;
using System.Runtime.InteropServices;

namespace Nova.Dsp
{
    public static class VectorMath
    {
        /// <summary>
        /// SIMD dot product of two equally long spans. Four independent accumulators keep the adds from waiting on
        /// each other, which matters more than the multiply width for spectrum-length inputs.
        /// </summary>
        public static double Dot(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
        {
            var va = MemoryMarshal.Cast<double, Vector<double>>(a);
            var vb = MemoryMarshal.Cast<double, Vector<double>>(b[..a.Length]);
            Vector<double> acc0 = Vector<double>.Zero, acc1 = Vector<double>.Zero, acc2 = Vector<double>.Zero, acc3 = Vector<double>.Zero;
            var i = 0;
            for (; i + 4 <= va.Length; i += 4)
            {
                acc0 = Vector.FusedMultiplyAdd(va[i], vb[i], acc0);
                acc1 = Vector.FusedMultiplyAdd(va[i + 1], vb[i + 1], acc1);
                acc2 = Vector.FusedMultiplyAdd(va[i + 2], vb[i + 2], acc2);
                acc3 = Vector.FusedMultiplyAdd(va[i + 3], vb[i + 3], acc3);
            }
            for (; i < va.Length; i++)
            {
                acc0 = Vector.FusedMultiplyAdd(va[i], vb[i], acc0);
            }
            var sum = Vector.Sum(acc0 + acc1 + (acc2 + acc3));
            for (var j = va.Length * Vector<double>.Count; j < a.Length; j++)
            {
                sum += a[j] * b[j];
            }
            return sum;
        }

        /// <summary>
        /// Autocorrelation r[k] = Σ x[i]·x[i+k] for four consecutive lags k..k+3 at once: each x[i] is loaded once
        /// for all four, and the four sums are independent.
        /// </summary>
        public static (double, double, double, double) Autocorrelation4(ReadOnlySpan<double> x, int k)
        {
            var common = x.Length - k - 3; // terms shared by all four lags
            ref var x0 = ref MemoryMarshal.GetReference(x);
            Vector<double> acc0 = Vector<double>.Zero, acc1 = Vector<double>.Zero, acc2 = Vector<double>.Zero, acc3 = Vector<double>.Zero;
            var i = 0;
            for (; i + Vector<double>.Count <= common; i += Vector<double>.Count)
            {
                var xi = Vector.LoadUnsafe(ref x0, (nuint)i);
                var j = (nuint)(i + k);
                acc0 = Vector.FusedMultiplyAdd(xi, Vector.LoadUnsafe(ref x0, j), acc0);
                acc1 = Vector.FusedMultiplyAdd(xi, Vector.LoadUnsafe(ref x0, j + 1), acc1);
                acc2 = Vector.FusedMultiplyAdd(xi, Vector.LoadUnsafe(ref x0, j + 2), acc2);
                acc3 = Vector.FusedMultiplyAdd(xi, Vector.LoadUnsafe(ref x0, j + 3), acc3);
            }
            double r0 = Vector.Sum(acc0), r1 = Vector.Sum(acc1), r2 = Vector.Sum(acc2), r3 = Vector.Sum(acc3);
            for (var t = i; t < x.Length - k; t++)
            {
                r0 += x[t] * x[t + k];
                if (t + k + 1 < x.Length)
                {
                    r1 += x[t] * x[t + k + 1];
                }
                if (t + k + 2 < x.Length)
                {
                    r2 += x[t] * x[t + k + 2];
                }
                if (t + k + 3 < x.Length)
                {
                    r3 += x[t] * x[t + k + 3];
                }
            }
            return (r0, r1, r2, r3);
        }

        /// <summary>dst[i] = a[i] · b[i].</summary>
        public static void Multiply(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> dst)
        {
            var i = 0;
            for (; i + Vector<double>.Count <= a.Length; i += Vector<double>.Count)
            {
                (new Vector<double>(a[i..]) * new Vector<double>(b[i..])).CopyTo(dst[i..]);
            }
            for (; i < a.Length; i++)
            {
                dst[i] = a[i] * b[i];
            }
        }

        /// <summary>Converts floats to doubles.</summary>
        public static void Widen(ReadOnlySpan<float> src, Span<double> dst)
        {
            var i = 0;
            for (; i + Vector<float>.Count <= src.Length; i += Vector<float>.Count)
            {
                Vector.Widen(new Vector<float>(src[i..]), out var low, out var high);
                low.CopyTo(dst[i..]);
                high.CopyTo(dst[(i + Vector<double>.Count)..]);
            }
            for (; i < src.Length; i++)
            {
                dst[i] = src[i];
            }
        }

        /// <summary>Σ x·log2(x) over positive values (zeros contribute nothing), vectorised.</summary>
        public static double SumXLog2X(ReadOnlySpan<double> x)
        {
            var vx = MemoryMarshal.Cast<double, Vector<double>>(x);
            var acc = Vector<double>.Zero;
            var one = Vector<double>.One;
            foreach (var v in vx)
            {
                // Zero and negative entries become log2(1) = 0, as entropy's 0·log 0 = 0 convention needs.
                var safe = Vector.ConditionalSelect(Vector.GreaterThan(v, Vector<double>.Zero), v, one);
                acc += safe * Vector.Log2(safe);
            }
            var sum = Vector.Sum(acc);
            for (var j = vx.Length * Vector<double>.Count; j < x.Length; j++)
            {
                if (x[j] > 0)
                {
                    sum += x[j] * Math.Log2(x[j]);
                }
            }
            return sum;
        }
    }
}
