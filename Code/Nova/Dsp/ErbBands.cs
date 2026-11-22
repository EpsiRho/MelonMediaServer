using System.Numerics;

namespace Nova.Dsp
{
    /// <summary>
    /// Gammatone filterbank on ERB-spaced centre frequencies (Slaney's auditory toolbox design), as used by
    /// Essentia's ERBBands/GFCC. Each band is the power spectrum weighted by the filter's amplitude response.
    /// To match AcousticBrainz, the response uses Essentia's |p - z|^-8 pole term (the pole squared rather than
    /// the pole times its conjugate), normalised by the gain of the exact filter at its centre frequency.
    /// </summary>
    public sealed class ErbBands
    {
        private const double EarQ = 9.26449;
        private const double MinBandwidth = 24.7;
        private const int Order = 4;

        private readonly double[][] _filters;

        public ErbBands(int bins, int sampleRate, int bandCount = 40, double lowFrequency = 40, double highFrequency = 22050)
        {
            var fftSize = 2 * (bins - 1);
            var t = 1.0 / sampleRate;
            var bw = EarQ * MinBandwidth;
            var sqrtPlus = Math.Sqrt(3 + Math.Pow(2, 1.5));
            var sqrtMinus = Math.Sqrt(3 - Math.Pow(2, 1.5));
            var unit = new Complex[bins];
            for (var k = 0; k < bins; k++)
            {
                unit[k] = Complex.FromPolarCoordinates(1, 2 * Math.PI * k / fftSize);
            }

            _filters = new double[bandCount][];
            for (var b = 0; b < bandCount; b++)
            {
                var cf = (highFrequency + bw) * Math.Pow((lowFrequency + bw) / (highFrequency + bw), (double)(bandCount - b) / bandCount) - bw;
                var erb = cf / EarQ + MinBandwidth;
                var bandwidth = 1.019 * 2 * Math.PI * erb;
                var theta = 2 * Math.PI * cf * t;
                var pole = Complex.FromPolarCoordinates(Math.Exp(-bandwidth * t), theta);
                var gtCos = 2 * t * Math.Cos(theta) / Math.Exp(bandwidth * t);
                var gtSin = t * Math.Sin(theta) / Math.Exp(bandwidth * t);
                double[] zeros =
                {
                    (gtCos + 2 * sqrtPlus * gtSin) / 2 / t,
                    (gtCos - 2 * sqrtPlus * gtSin) / 2 / t,
                    (gtCos + 2 * sqrtMinus * gtSin) / 2 / t,
                    (gtCos - 2 * sqrtMinus * gtSin) / 2 / t,
                };
                double ZeroTerm(Complex z) => zeros.Aggregate(1.0, (acc, zero) => acc * (z - zero).Magnitude);

                // Gain of the exact cascade at the centre frequency, so that filter would peak at 1.
                var centre = Complex.FromPolarCoordinates(1, theta);
                var gain = Math.Pow(t, Order) * ZeroTerm(centre)
                    / Math.Pow(((centre - pole) * (centre - Complex.Conjugate(pole))).Magnitude, Order);

                var filter = new double[bins];
                for (var k = 0; k < bins; k++)
                {
                    var z = unit[k];
                    filter[k] = Math.Pow(t, Order) / gain * ZeroTerm(z) * Math.Pow(((pole - z) * (pole - z)).Magnitude, -Order);
                }
                _filters[b] = filter;
            }
        }

        public int Count => _filters.Length;

        public void Apply(ReadOnlySpan<double> power, Span<double> bands)
        {
            for (var b = 0; b < _filters.Length; b++)
            {
                bands[b] = VectorMath.Dot(power, _filters[b]);
            }
        }
    }
}
