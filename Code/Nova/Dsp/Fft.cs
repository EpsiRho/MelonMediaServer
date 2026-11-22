using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nova.Dsp
{
    /// <summary>
    /// FFT of real frames: the even and odd samples are packed into one complex sequence of half the length,
    /// transformed with an in-place iterative radix-2 FFT, then unpacked into bins 0..N/2.
    /// Butterfly stages wide enough for a SIMD register (<see cref="Vector{T}"/>: 4 doubles with AVX2, 2 with NEON)
    /// run vectorised, two stages per pass over the data. Fusing stages keeps every operation and its order, so
    /// the result is bit-identical to the plain scalar algorithm.
    /// </summary>
    public sealed class Fft
    {
        private readonly int _size;
        private readonly int _half;
        private readonly double[] _cos;
        private readonly double[] _sin;
        private readonly int[] _reverse;
        private readonly double[] _re;
        private readonly double[] _im;
        // Twiddles of each stage, contiguous so SIMD stages can load them directly: stage s has 2^s of them.
        private readonly double[][] _stageCos;
        private readonly double[][] _stageSin;

        public Fft(int size)
        {
            if (size < 2 || (size & (size - 1)) != 0)
            {
                throw new ArgumentException("FFT size must be a power of two.", nameof(size));
            }

            _size = size;
            _half = size / 2;
            // Twiddles e^(-2πik/N) for k < N/2; the half-size transform uses every other one.
            _cos = new double[_half];
            _sin = new double[_half];
            for (var i = 0; i < _half; i++)
            {
                _cos[i] = Math.Cos(-2 * Math.PI * i / size);
                _sin[i] = Math.Sin(-2 * Math.PI * i / size);
            }

            var bits = (int)Math.Log2(_half);
            _reverse = new int[_half];
            for (var i = 0; i < _half; i++)
            {
                var r = 0;
                for (var b = 0; b < bits; b++)
                {
                    r |= ((i >> b) & 1) << (bits - 1 - b);
                }
                _reverse[i] = r;
            }

            _re = new double[_half];
            _im = new double[_half];

            _stageCos = new double[bits][];
            _stageSin = new double[bits][];
            for (var s = 0; s < bits; s++)
            {
                var half = 1 << s;
                var step = _size / (2 * half);
                _stageCos[s] = new double[half];
                _stageSin[s] = new double[half];
                for (var j = 0; j < half; j++)
                {
                    _stageCos[s][j] = _cos[j * step];
                    _stageSin[s][j] = _sin[j * step];
                }
            }
        }

        public int Size => _size;

        /// <summary>
        /// Transforms a real frame and writes magnitude (and optionally phase) for bins 0..N/2.
        /// </summary>
        public void RealSpectrum(ReadOnlySpan<double> frame, Span<double> magnitude, Span<double> phase = default)
        {
            Transform(frame);
            for (var k = 0; k <= _half; k++)
            {
                var (re, im) = Bin(k);
                magnitude[k] = Math.Sqrt(re * re + im * im);
                if (!phase.IsEmpty)
                {
                    phase[k] = Math.Atan2(im, re);
                }
            }
        }

        /// <summary>Transforms a real frame and writes the real and imaginary parts for bins 0..N/2.</summary>
        public void RealTransform(ReadOnlySpan<double> frame, Span<double> real, Span<double> imaginary)
        {
            Transform(frame);
            for (var k = 0; k <= _half; k++)
            {
                (real[k], imaginary[k]) = Bin(k);
            }
        }

        /// <summary>
        /// Bin k of the real input's spectrum from the packed transform Z: X[k] = E[k] + e^(-2πik/N)·O[k], where
        /// E = (Z[k] + conj Z[N/2-k]) / 2 and O = (Z[k] - conj Z[N/2-k]) / 2i are the even and odd samples' spectra.
        /// </summary>
        private (double Re, double Im) Bin(int k)
        {
            var a = k == _half ? 0 : k;
            var b = k == 0 ? 0 : _half - k;
            var zr = _re[a];
            var zi = _im[a];
            var cr = _re[b];
            var ci = -_im[b];
            var er = 0.5 * (zr + cr);
            var ei = 0.5 * (zi + ci);
            var or = 0.5 * (zi - ci);
            var oi = -0.5 * (zr - cr);
            var wr = k < _half ? _cos[k] : -1;
            var wi = k < _half ? _sin[k] : 0;
            return (er + wr * or - wi * oi, ei + wr * oi + wi * or);
        }

        private void Transform(ReadOnlySpan<double> frame)
        {
            if (frame.Length >= _size)
            {
                for (var n = 0; n < _half; n++)
                {
                    _re[_reverse[n]] = frame[2 * n];
                    _im[_reverse[n]] = frame[2 * n + 1];
                }
            }
            else
            {
                for (var n = 0; n < _half; n++)
                {
                    _re[_reverse[n]] = 2 * n < frame.Length ? frame[2 * n] : 0;
                    _im[_reverse[n]] = 2 * n + 1 < frame.Length ? frame[2 * n + 1] : 0;
                }
            }

            var stages = _stageCos.Length;
            var s = 0;
            for (; s + 1 < stages && (2 << s) < Vector<double>.Count; s += 2)
            {
                ScalarStagePair(s);
            }
            for (; s < stages && (1 << s) < Vector<double>.Count; s++)
            {
                ScalarStage(s);
            }
            for (; s + 1 < stages; s += 2)
            {
                VectorStagePair(s);
            }
            if (s < stages)
            {
                VectorStage(s);
            }
        }

        private void ScalarStage(int s)
        {
            var half = 1 << s;
            var wrs = _stageCos[s];
            var wis = _stageSin[s];
            for (var start = 0; start < _half; start += 2 * half)
            {
                for (var j = 0; j < half; j++)
                {
                    var a = start + j;
                    var b = a + half;
                    var tr = _re[b] * wrs[j] - _im[b] * wis[j];
                    var ti = _re[b] * wis[j] + _im[b] * wrs[j];
                    _re[b] = _re[a] - tr;
                    _im[b] = _im[a] - ti;
                    _re[a] += tr;
                    _im[a] += ti;
                }
            }
        }

        /// <summary>Stages s and s+1 in one pass, as <see cref="VectorStagePair"/> does, for stages narrower than a vector.</summary>
        private void ScalarStagePair(int s)
        {
            var q = 1 << s;
            var w1r = _stageCos[s];
            var w1i = _stageSin[s];
            var w2r = _stageCos[s + 1];
            var w2i = _stageSin[s + 1];
            for (var start = 0; start < _half; start += 4 * q)
            {
                for (var j = 0; j < q; j++)
                {
                    var a0 = start + j;
                    var a1 = a0 + q;
                    var a2 = a1 + q;
                    var a3 = a2 + q;
                    double r0 = _re[a0], i0 = _im[a0], r1 = _re[a1], i1 = _im[a1];
                    double r2 = _re[a2], i2 = _im[a2], r3 = _re[a3], i3 = _im[a3];
                    Butterfly(ref r0, ref i0, ref r1, ref i1, w1r[j], w1i[j]);
                    Butterfly(ref r2, ref i2, ref r3, ref i3, w1r[j], w1i[j]);
                    Butterfly(ref r0, ref i0, ref r2, ref i2, w2r[j], w2i[j]);
                    Butterfly(ref r1, ref i1, ref r3, ref i3, w2r[j + q], w2i[j + q]);
                    (_re[a0], _im[a0], _re[a1], _im[a1]) = (r0, i0, r1, i1);
                    (_re[a2], _im[a2], _re[a3], _im[a3]) = (r2, i2, r3, i3);
                }
            }
        }

        private void VectorStage(int s)
        {
            var half = 1 << s;
            ref var re = ref MemoryMarshal.GetArrayDataReference(_re);
            ref var im = ref MemoryMarshal.GetArrayDataReference(_im);
            ref var wr = ref MemoryMarshal.GetArrayDataReference(_stageCos[s]);
            ref var wi = ref MemoryMarshal.GetArrayDataReference(_stageSin[s]);
            for (var start = 0; start < _half; start += 2 * half)
            {
                for (var j = 0; j < half; j += Vector<double>.Count)
                {
                    var a = (nuint)(start + j);
                    var b = a + (nuint)half;
                    Butterfly(ref re, ref im, a, b, Vector.LoadUnsafe(ref wr, (nuint)j), Vector.LoadUnsafe(ref wi, (nuint)j));
                }
            }
        }

        /// <summary>
        /// Stages s and s+1 in one pass. Each group of four quarter-blocks (a0..a3) is loaded once, takes the two
        /// stage-s butterflies (a0,a1) and (a2,a3), then the two stage-(s+1) butterflies (a0,a2) and (a1,a3).
        /// </summary>
        private void VectorStagePair(int s)
        {
            var q = 1 << s;
            ref var re = ref MemoryMarshal.GetArrayDataReference(_re);
            ref var im = ref MemoryMarshal.GetArrayDataReference(_im);
            ref var w1r = ref MemoryMarshal.GetArrayDataReference(_stageCos[s]);
            ref var w1i = ref MemoryMarshal.GetArrayDataReference(_stageSin[s]);
            ref var w2r = ref MemoryMarshal.GetArrayDataReference(_stageCos[s + 1]);
            ref var w2i = ref MemoryMarshal.GetArrayDataReference(_stageSin[s + 1]);
            for (var start = 0; start < _half; start += 4 * q)
            {
                for (var j = 0; j < q; j += Vector<double>.Count)
                {
                    var a0 = (nuint)(start + j);
                    var a1 = a0 + (nuint)q;
                    var a2 = a1 + (nuint)q;
                    var a3 = a2 + (nuint)q;
                    var r0 = Vector.LoadUnsafe(ref re, a0);
                    var i0 = Vector.LoadUnsafe(ref im, a0);
                    var r1 = Vector.LoadUnsafe(ref re, a1);
                    var i1 = Vector.LoadUnsafe(ref im, a1);
                    var r2 = Vector.LoadUnsafe(ref re, a2);
                    var i2 = Vector.LoadUnsafe(ref im, a2);
                    var r3 = Vector.LoadUnsafe(ref re, a3);
                    var i3 = Vector.LoadUnsafe(ref im, a3);

                    var wr = Vector.LoadUnsafe(ref w1r, (nuint)j);
                    var wi = Vector.LoadUnsafe(ref w1i, (nuint)j);
                    Butterfly(ref r0, ref i0, ref r1, ref i1, wr, wi);
                    Butterfly(ref r2, ref i2, ref r3, ref i3, wr, wi);
                    Butterfly(ref r0, ref i0, ref r2, ref i2, Vector.LoadUnsafe(ref w2r, (nuint)j), Vector.LoadUnsafe(ref w2i, (nuint)j));
                    Butterfly(ref r1, ref i1, ref r3, ref i3, Vector.LoadUnsafe(ref w2r, (nuint)(j + q)), Vector.LoadUnsafe(ref w2i, (nuint)(j + q)));

                    r0.StoreUnsafe(ref re, a0);
                    i0.StoreUnsafe(ref im, a0);
                    r1.StoreUnsafe(ref re, a1);
                    i1.StoreUnsafe(ref im, a1);
                    r2.StoreUnsafe(ref re, a2);
                    i2.StoreUnsafe(ref im, a2);
                    r3.StoreUnsafe(ref re, a3);
                    i3.StoreUnsafe(ref im, a3);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Butterfly(ref double re, ref double im, nuint a, nuint b, Vector<double> wr, Vector<double> wi)
        {
            var ar = Vector.LoadUnsafe(ref re, a);
            var ai = Vector.LoadUnsafe(ref im, a);
            var br = Vector.LoadUnsafe(ref re, b);
            var bi = Vector.LoadUnsafe(ref im, b);
            Butterfly(ref ar, ref ai, ref br, ref bi, wr, wi);
            ar.StoreUnsafe(ref re, a);
            ai.StoreUnsafe(ref im, a);
            br.StoreUnsafe(ref re, b);
            bi.StoreUnsafe(ref im, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Butterfly(ref double ar, ref double ai, ref double br, ref double bi, double wr, double wi)
        {
            var tr = br * wr - bi * wi;
            var ti = br * wi + bi * wr;
            br = ar - tr;
            bi = ai - ti;
            ar += tr;
            ai += ti;
        }

        /// <summary>a, b ← a + w·b, a − w·b.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Butterfly(ref Vector<double> ar, ref Vector<double> ai, ref Vector<double> br, ref Vector<double> bi,
            Vector<double> wr, Vector<double> wi)
        {
            var tr = br * wr - bi * wi;
            var ti = br * wi + bi * wr;
            br = ar - tr;
            bi = ai - ti;
            ar += tr;
            ai += ti;
        }
    }
}
