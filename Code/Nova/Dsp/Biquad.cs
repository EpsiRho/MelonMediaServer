namespace Nova.Dsp
{
    /// <summary>
    /// One second-order IIR section, normalised so a0 = 1:
    /// H(z) = (b0 + b1·z⁻¹ + b2·z⁻²) / (1 + a1·z⁻¹ + a2·z⁻²). A first-order section has b2 = a2 = 0.
    /// </summary>
    public readonly record struct Biquad(double B0, double B1, double B2, double A1, double A2)
    {
        public static readonly Biquad Identity = new(1, 0, 0, 0, 0);

        /// <summary>|H|² at <paramref name="omega"/> radians per sample (0 … π).</summary>
        public double MagnitudeSquared(double omega)
        {
            // |b0 + b1·e^-jω + b2·e^-2jω|² written in φ = sin²(ω/2), which stays accurate near DC where the
            // cos(ω) form loses digits (see Vicanek, "Matched Second Order Digital Filters", 2016).
            var s = Math.Sin(omega / 2);
            var p1 = s * s;
            var p0 = 1 - p1;
            var p2 = 4 * p0 * p1;
            var num = Sq(B0 + B1 + B2) * p0 + Sq(B0 - B1 + B2) * p1 - 4 * B0 * B2 * p2;
            var den = Sq(1 + A1 + A2) * p0 + Sq(1 - A1 + A2) * p1 - 4 * A2 * p2;
            return den <= 0 ? 0 : Math.Max(0, num) / den;
        }

        private static double Sq(double x) => x * x;
    }

    /// <summary>
    /// Designs digital sections whose magnitude response matches an analog prototype, without the high-frequency
    /// "cramping" of the bilinear transform (the RBJ cookbook): a 3 dB bell at 16 kHz on a 44.1 kHz track keeps its
    /// width and gain instead of being squeezed against Nyquist.
    /// <para>
    /// Method (Vicanek 2016): the poles are placed by impulse invariance, which reproduces the analog decay exactly;
    /// then the numerator is solved so |H|² equals the analog |H|² at DC, at Nyquist and at the corner frequency.
    /// Any analog biquad works, so every band type goes through <see cref="Match"/>.
    /// </para>
    /// </summary>
    public static class MatchedBiquad
    {
        /// <summary>
        /// Matches the normalised analog prototype H(s) = (n2·s² + n1·s + n0) / (d2·s² + d1·s + d0), where s is
        /// in units of the corner frequency, at corner <paramref name="omega0"/> radians per sample (0 &lt; ω0 &lt; π).
        /// </summary>
        public static Biquad Match(double n2, double n1, double n0, double d2, double d1, double d0, double omega0)
        {
            // Poles: roots of the denominator, scaled to radians per sample, mapped by z = e^(s·T).
            double a1, a2;
            var disc = d1 * d1 - 4 * d2 * d0;
            if (disc < 0)
            {
                var sigma = -d1 / (2 * d2) * omega0;
                var omega = Math.Sqrt(-disc) / (2 * d2) * omega0;
                var r = Math.Exp(sigma);
                a1 = -2 * r * Math.Cos(omega);
                a2 = r * r;
            }
            else
            {
                var root = Math.Sqrt(disc);
                var p = (-d1 + root) / (2 * d2) * omega0;
                var q = (-d1 - root) / (2 * d2) * omega0;
                a1 = -(Math.Exp(p) + Math.Exp(q));
                a2 = Math.Exp(p + q);
            }

            var A0 = Sq(1 + a1 + a2);
            var A1 = Sq(1 - a1 + a2);
            var A2 = -4 * a2;

            var nyquist = Math.PI / omega0; // Nyquist in units of the corner frequency
            var B0 = A0 * Analog(n2, n1, n0, d2, d1, d0, 0);
            var B1 = A1 * Analog(n2, n1, n0, d2, d1, d0, nyquist);

            var s = Math.Sin(omega0 / 2);
            var p1 = s * s;
            var p0 = 1 - p1;
            var p2 = 4 * p0 * p1;
            var target = Analog(n2, n1, n0, d2, d1, d0, 1) * (A0 * p0 + A1 * p1 + A2 * p2);
            var B2 = (target - B0 * p0 - B1 * p1) / p2;

            // Back from |N|² terms to coefficients: W = b0+b1+b2, V = b0-b1+b2, B2 = -4·b0·b2. Taking the larger root
            // for b0 keeps the zeros inside the unit circle (minimum phase, the shortest ringing for the response).
            var W = Math.Sqrt(B0);
            var V = Math.Sqrt(B1);
            var sum = (W + V) / 2;
            var root2 = Math.Sqrt(Math.Max(0, sum * sum + B2));
            var b0 = (sum + root2) / 2;
            var b2 = (sum - root2) / 2;
            var b1 = (W - V) / 2;
            return new Biquad(b0, b1, b2, a1, a2);
        }

        /// <summary>First-order low-pass (<paramref name="highPass"/> false) or high-pass, matched at DC and Nyquist.</summary>
        public static Biquad FirstOrder(double omega0, bool highPass)
        {
            var a1 = -Math.Exp(-omega0);
            var nyquist = Math.PI / omega0;
            var dc = highPass ? 0 : 1;
            var top = highPass ? nyquist * nyquist / (1 + nyquist * nyquist) : 1 / (1 + nyquist * nyquist);
            var W = Math.Sqrt(dc) * (1 + a1);
            var V = Math.Sqrt(top) * (1 - a1);
            return new Biquad((W + V) / 2, (W - V) / 2, 0, a1, 0);
        }

        // ── Prototypes (RBJ analog forms; gain in dB, A = 10^(gain/40)) ──────────────────────────────────

        public static Biquad Peak(double omega0, double gainDb, double q)
        {
            var A = Math.Pow(10, gainDb / 40);
            return Match(1, A / q, 1, 1, 1 / (A * q), 1, omega0);
        }

        public static Biquad LowShelf(double omega0, double gainDb, double q)
        {
            var A = Math.Pow(10, gainDb / 40);
            var k = Math.Sqrt(A) / q;
            return Match(A, A * k, A * A, A, k, 1, omega0);
        }

        public static Biquad HighShelf(double omega0, double gainDb, double q)
        {
            var A = Math.Pow(10, gainDb / 40);
            var k = Math.Sqrt(A) / q;
            return Match(A * A, A * k, A, 1, k, A, omega0);
        }

        public static Biquad LowPass(double omega0, double q) => Match(0, 0, 1, 1, 1 / q, 1, omega0);
        public static Biquad HighPass(double omega0, double q) => Match(1, 0, 0, 1, 1 / q, 1, omega0);
        /// <summary>Band-pass with 0 dB at the centre.</summary>
        public static Biquad BandPass(double omega0, double q) => Match(0, 1 / q, 0, 1, 1 / q, 1, omega0);
        public static Biquad Notch(double omega0, double q) => Match(1, 0, 1, 1, 1 / q, 1, omega0);

        /// <summary>|H(jΩ)|² of the analog prototype at Ω (units of the corner frequency).</summary>
        private static double Analog(double n2, double n1, double n0, double d2, double d1, double d0, double w)
        {
            var w2 = w * w;
            var num = Sq(n0 - n2 * w2) + Sq(n1 * w);
            var den = Sq(d0 - d2 * w2) + Sq(d1 * w);
            return num / den;
        }

        private static double Sq(double x) => x * x;
    }
}
