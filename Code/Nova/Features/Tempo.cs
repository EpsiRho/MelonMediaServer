using Nova.Audio;
using Nova.Dsp;

namespace Nova.Features
{
    public sealed record TempoResult(double Bpm, double[] Beats);

    /// <summary>
    /// Beat tracking in the style of Degara et al. (2012), "Reliability-informed beat tracking of musical
    /// signals", which AcousticBrainz used for rhythm.bpm and rhythm.beats_position:
    /// complex-domain onset function, Davies-style beat-period tracking, then an HMM over
    /// "time since last beat" decoded with Viterbi. BPM is derived from the beat times the same way.
    /// </summary>
    public static class Tempo
    {
        private const int FrameSize = 2048;
        private const int HopSize = 1024;
        private const int Upsample = 2;
        private const double PeriodFrameSeconds = 5.944308390022676;
        private const int Combs = 4;
        private const double IbiSigmaSeconds = 0.025;
        private const double Alpha = 0.5;
        private const double BpmTolerance = 5;
        private const float FloatEpsilon = 1.1920929e-7f;

        public static TempoResult Compute(MonoSignal signal, double minTempo = 40, double maxTempo = 208)
        {
            var odf = OnsetFunction(signal);
            if (odf.Length < 4)
            {
                return new TempoResult(0, Array.Empty<double>());
            }

            var rate = (double)signal.SampleRate / HopSize * Upsample;
            var detection = Resample(odf);
            // Periods come from a thresholded copy; beats are decoded on the detection function itself.
            var periods = BeatPeriods(AdaptiveThreshold(detection, (int)Math.Floor(0.1 * rate)), rate, minTempo, maxTempo);
            var beats = DecodeBeats(detection, periods, rate);
            return new TempoResult(BpmFromBeats(beats), beats);
        }

        /// <summary>
        /// Complex spectral difference: distance between each bin and its prediction from the two previous frames,
        /// which keeps the previous magnitude and extrapolates the phase linearly (φ = 2φ₁ − φ₂). Phases are kept as
        /// unit phasors, so cos(φ − 2φ₁ + φ₂) = Re(u · conj(u₁)² · u₂) needs no trigonometry.
        /// </summary>
        private static double[] OnsetFunction(MonoSignal signal)
        {
            var window = Window.Create(WindowType.Hann, FrameSize);
            var fft = new Fft(FrameSize);
            var bins = FrameSize / 2 + 1;
            var frame = new double[FrameSize];
            var re = new double[bins];
            var im = new double[bins];
            var prevMagnitude = new double[bins];
            // Phasors of the previous two frames; a zero bin has phase 0 (or π for a -0 real part), as atan2 gives.
            var u1r = new double[bins];
            var u1i = new double[bins];
            var u2r = new double[bins];
            var u2i = new double[bins];
            Array.Fill(u1r, 1.0);
            Array.Fill(u2r, 1.0);
            var values = new List<double>();

            foreach (var start in Framing.Starts(signal.Samples.Length, FrameSize, HopSize, startFromZero: true))
            {
                Framing.Fill(signal.Samples, start, window, frame);
                fft.RealTransform(frame, re, im);
                var sum = 0.0;
                for (var k = 0; k < bins; k++)
                {
                    var b = Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
                    double ur, ui;
                    if (b > 0)
                    {
                        ur = re[k] / b;
                        ui = im[k] / b;
                    }
                    else
                    {
                        ur = double.IsNegative(re[k]) ? -1 : 1;
                        ui = 0;
                    }

                    var cr = u1r[k] * u1r[k] - u1i[k] * u1i[k];
                    var ci = -2 * u1r[k] * u1i[k];
                    var pr = cr * u2r[k] - ci * u2i[k];
                    var pi = cr * u2i[k] + ci * u2r[k];
                    var cos = ur * pr - ui * pi;

                    var a = prevMagnitude[k];
                    var d2 = a * a + b * b - 2 * a * b * cos;
                    sum += Math.Sqrt(Math.Max(d2, 0));
                    (u2r[k], u2i[k]) = (u1r[k], u1i[k]);
                    (u1r[k], u1i[k]) = (ur, ui);
                    prevMagnitude[k] = b;
                }
                values.Add(sum);
            }

            return values.ToArray();
        }

        /// <summary>Normalise by the maximum, then upsample x2 by linear interpolation between samples (n samples become 2n − 1).</summary>
        private static double[] Resample(double[] odf)
        {
            var max = odf.Max();
            var scale = max > 0 ? 1 / max : 1;
            var up = new double[(odf.Length - 1) * Upsample + 1];
            for (var i = 0; i + 1 < odf.Length; i++)
            {
                var a = odf[i] * scale;
                var step = (odf[i + 1] * scale - a) / Upsample;
                for (var r = 0; r < Upsample; r++)
                {
                    up[i * Upsample + r] = a + step * r;
                }
            }
            up[^1] = odf[^1] * scale;
            return up;
        }

        /// <summary>Subtracts a centred moving average (edges extended with the end values) and half-wave rectifies.</summary>
        private static double[] AdaptiveThreshold(double[] x, int halfWidth)
        {
            var output = new double[x.Length];
            for (var i = 0; i < x.Length; i++)
            {
                var sum = 0.0;
                for (var j = i - halfWidth; j <= i + halfWidth; j++)
                {
                    sum += x[Math.Clamp(j, 0, x.Length - 1)];
                }
                output[i] = Math.Max(x[i] - sum / (2 * halfWidth + 1), 0);
            }
            return output;
        }

        /// <summary>
        /// One beat period (in detection-function samples) per 1.5 s step: comb-filtered autocorrelation over
        /// ~6 s windows starting every 1.5 s, smoothed over time with a Viterbi pass and a Rayleigh prior centred
        /// on 120 BPM. The tempo limits zero the observations; the Viterbi transitions allow periods of 28 to 108
        /// samples (at 86 Hz) and move at most 4 standard deviations of their Gaussian between steps.
        /// </summary>
        private static int[] BeatPeriods(double[] d, double rate, double minTempo, double maxTempo)
        {
            var n = (int)Math.Round(PeriodFrameSeconds * rate);
            var hop = n / Combs;
            var scale = rate / (44100.0 / 512);

            var combMin = Combs - 1;
            var combMax = (n - (Combs - 1)) / Combs - 1;
            var userMin = Math.Min((int)Math.Floor(60 / maxTempo * rate) - 1, hop - 1);
            var userMax = Math.Min((int)Math.Ceiling(60 / minTempo * rate) - 1, hop - 1);
            var lo = (int)Math.Floor(28 * scale) - 1;
            var hi = Math.Min((int)Math.Ceiling(108 * scale) - 1, hop - 1);

            // Rayleigh weighting over every period lag, peaking at the lag of 120 BPM. It weights each comb
            // output (otherwise the comb favours multiples of the true period) and is the Viterbi prior.
            // It must cover lags outside the allowed range too: zeros there would pull down the adaptive
            // threshold next to the range's edge and make the shortest periods win.
            var beta2 = Math.Pow(Math.Round(60 * rate / 120), 2);
            var prior = new double[hop];
            for (var p = 0; p < hop; p++)
            {
                var tau = p + 1.0;
                prior[p] = tau / beta2 * Math.Exp(-0.5 * tau * tau / beta2);
            }
            Normalize(prior);

            var observations = new List<double[]>();
            var segment = new double[n];
            var acf = new double[n];
            var maxObs = 0.0;
            foreach (var start in Framing.Starts(d.Length, n, hop, startFromZero: true))
            {
                for (var i = 0; i < n; i++)
                {
                    var j = start + i;
                    segment[i] = j < d.Length ? d[j] : 0;
                }
                for (var lag = 0; lag < n; lag++)
                {
                    var sum = 0.0;
                    for (var i = 0; i + lag < n; i++)
                    {
                        sum += segment[i] * segment[i + lag];
                    }
                    acf[lag] = sum / (n - lag);
                }

                var comb = new double[hop];
                for (var p = combMin; p <= combMax; p++)
                {
                    var sum = 0.0;
                    for (var c = 1; c <= Combs; c++)
                    {
                        for (var r = 1 - c; r <= c - 1; r++)
                        {
                            sum += acf[(p + 1) * c - 1 + r] / (2 * c - 1);
                        }
                    }
                    comb[p] = sum * prior[p];
                }

                var obs = AdaptiveThreshold(comb, 8);
                for (var p = 0; p < hop; p++)
                {
                    if (p <= userMin || p > userMax)
                    {
                        obs[p] = 0;
                    }
                }
                Normalize(obs);
                maxObs = Math.Max(maxObs, obs.Max());
                observations.Add(obs);
            }

            var frames = observations.Count;
            var random = new Random(0);
            foreach (var obs in observations)
            {
                for (var p = 0; p < hop; p++)
                {
                    obs[p] += 1e-4 * maxObs * random.NextDouble();
                }
            }

            // Gaussian transitions (sd 8 samples at 86 Hz) between periods inside [lo, hi], cut off at 4 sd.
            var sigma = 8 * scale;
            var reach = (int)Math.Ceiling(4 * sigma);
            var delta = new double[hop];
            var next = new double[hop];
            var back = new int[frames, hop];
            for (var p = 0; p < hop; p++)
            {
                delta[p] = SafeLog(prior[p]) + SafeLog(observations[0][p]);
            }
            for (var t = 1; t < frames; t++)
            {
                for (var j = 0; j < hop; j++)
                {
                    next[j] = double.NegativeInfinity;
                    if (j < lo || j > hi)
                    {
                        continue;
                    }
                    var best = double.NegativeInfinity;
                    var arg = -1;
                    for (var i = Math.Max(lo, j - reach); i <= Math.Min(hi, j + reach); i++)
                    {
                        var v = delta[i] - (j - i) * (j - i) / (2 * sigma * sigma);
                        if (v > best)
                        {
                            best = v;
                            arg = i;
                        }
                    }
                    if (arg >= 0)
                    {
                        next[j] = best + SafeLog(observations[t][j]);
                        back[t, j] = arg;
                    }
                }
                (delta, next) = (next, delta);
            }

            var path = new int[frames];
            var state = 0;
            for (var p = 1; p < hop; p++)
            {
                if (delta[p] > delta[state])
                {
                    state = p;
                }
            }
            for (var t = frames - 1; t >= 0; t--)
            {
                path[t] = state + 1;
                if (t > 0)
                {
                    state = back[t, state];
                }
            }
            return path;
        }

        /// <summary>
        /// HMM whose state counts samples since the last beat, starting just after a beat. The chance of a beat
        /// at each state follows a Gaussian inter-beat-interval distribution around the local period (the hazard
        /// of that distribution); the onset strength is the observation. Viterbi gives the beat times. Each
        /// period estimate takes over at the end of its 1.5 s step, so the first one also covers the first 3 s.
        /// </summary>
        private static double[] DecodeBeats(double[] detection, int[] periods, double rate)
        {
            var hop = (int)Math.Round(PeriodFrameSeconds * rate) / Combs;
            // The longest interval is the longest period plus 3 sd, counted in single-precision steps as Essentia does.
            var resolution = (float)(1 / rate);
            var longest = (float)(periods.Max() / rate + 3 * IbiSigmaSeconds);
            var states = 0;
            for (var t = resolution; t <= longest; t += resolution)
            {
                states++;
            }

            var hazards = new Dictionary<int, (double[] beat, double[] stay)>();
            foreach (var period in periods.Distinct())
            {
                hazards[period] = Hazard(period, rate, states);
            }

            var logBeatObs = new double[detection.Length];
            var logRestObs = new double[detection.Length];
            for (var t = 0; t < detection.Length; t++)
            {
                var b = 0.99 * (detection[t] > 0 ? detection[t] : FloatEpsilon);
                logBeatObs[t] = (1 - Alpha) * Math.Log(b);
                logRestObs[t] = (1 - Alpha) * Math.Log(1 - b);
            }

            var delta = new double[states];
            var next = new double[states];
            var beatFrom = new int[detection.Length];
            Array.Fill(delta, double.NegativeInfinity);
            delta[0] = 0;
            var index = 0;
            for (var t = 0; t < detection.Length; t++)
            {
                var (beat, stay) = hazards[periods[index]];
                var best = double.NegativeInfinity;
                var arg = 0;
                for (var s = 0; s < states; s++)
                {
                    var v = delta[s] + beat[s];
                    if (v > best)
                    {
                        best = v;
                        arg = s;
                    }
                }
                next[0] = best + logBeatObs[t];
                beatFrom[t] = arg;
                for (var s = 1; s < states; s++)
                {
                    next[s] = delta[s - 1] + stay[s - 1] + logRestObs[t];
                }
                (delta, next) = (next, delta);
                while (index + 1 < periods.Length && (index + 2) * hop <= t + 1)
                {
                    index++;
                }
            }

            var state = 0;
            for (var s = 1; s < states; s++)
            {
                if (delta[s] > delta[state])
                {
                    state = s;
                }
            }

            var beats = new List<double>();
            for (var t = detection.Length - 1; t >= 0; t--)
            {
                if (state == 0)
                {
                    beats.Add(t / rate);
                    state = beatFrom[t];
                }
                else
                {
                    state--;
                }
            }
            beats.Reverse();
            return beats.ToArray();
        }

        /// <summary>
        /// Log-probabilities of a beat (or no beat) on the next sample given s samples since the last beat. The
        /// inter-beat interval follows a Gaussian density centred on the period, cut off at 4 sd and scaled by
        /// 0.005 as in Essentia. It isn't normalised, so no beat is ever forced, and the last state can't wait.
        /// </summary>
        private static (double[] beat, double[] stay) Hazard(int period, double rate, int states)
        {
            var step = 1 / rate;
            var half = (int)Math.Ceiling(4 * IbiSigmaSeconds / step);
            var height = 1 / (IbiSigmaSeconds * Math.Sqrt(2 * Math.PI)) * (0.01 / Upsample);
            var pdf = new double[states];
            for (var s = 0; s < states; s++)
            {
                // Being in state s means s samples since the beat; a beat now makes the interval s + 1.
                var offset = s + 1 - period;
                if (Math.Abs(offset) <= half)
                {
                    var x = offset * step;
                    var g = height * Math.Exp(-x * x / (2 * IbiSigmaSeconds * IbiSigmaSeconds));
                    pdf[s] = g < 1e-12 ? 0 : g;
                }
            }

            var beat = new double[states];
            var stay = new double[states];
            var survival = 1.0;
            for (var s = 0; s < states; s++)
            {
                var h = survival > 0 ? Math.Clamp(pdf[s] / survival, 0, 1) : 1;
                beat[s] = Alpha * SafeLog(h);
                stay[s] = s + 1 < states ? Alpha * SafeLog(1 - h) : double.NegativeInfinity;
                survival *= 1 - h;
            }
            return (beat, stay);
        }

        /// <summary>Mode of the per-interval BPMs (2 BPM bins, rounded to the nearest), then the mean of the intervals within 5 BPM of it.</summary>
        public static double BpmFromBeats(IReadOnlyList<double> beats)
        {
            if (beats.Count < 2)
            {
                return 0;
            }

            var estimates = new List<double>();
            for (var i = 1; i < beats.Count; i++)
            {
                var interval = beats[i] - beats[i - 1];
                if (interval > 0)
                {
                    estimates.Add(60 / interval);
                }
            }
            if (estimates.Count == 0)
            {
                return 0;
            }

            var counts = new Dictionary<int, int>();
            foreach (var bpm in estimates)
            {
                var bin = (int)Math.Round(bpm / 2, MidpointRounding.AwayFromZero);
                counts[bin] = counts.GetValueOrDefault(bin) + 1;
            }
            var closest = 2.0 * counts.OrderByDescending(c => c.Value).ThenBy(c => c.Key).First().Key;
            var near = estimates.Where(e => Math.Abs(e - closest) < BpmTolerance).ToList();
            return near.Count > 0 ? near.Average() : closest;
        }

        private static void Normalize(double[] x)
        {
            var sum = x.Sum();
            if (sum > 0)
            {
                for (var i = 0; i < x.Length; i++)
                {
                    x[i] /= sum;
                }
            }
        }

        private static double SafeLog(double x) => x > 0 ? Math.Log(x) : double.NegativeInfinity;
    }
}
