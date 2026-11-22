namespace Nova.Dsp
{
    public enum WindowType
    {
        Hann,
        BlackmanHarris62,
    }

    public static class Window
    {
        /// <summary>
        /// Symmetric window scaled so its absolute values sum to 2, the convention Essentia's spectra assume:
        /// a full-scale sine then shows up with a peak magnitude of about its amplitude.
        /// </summary>
        public static double[] Create(WindowType type, int size)
        {
            var w = new double[size];
            var denom = size - 1.0;
            for (var i = 0; i < size; i++)
            {
                var x = 2 * Math.PI * i / denom;
                w[i] = type switch
                {
                    WindowType.Hann => 0.5 - 0.5 * Math.Cos(x),
                    WindowType.BlackmanHarris62 => 0.44959 - 0.49364 * Math.Cos(x) + 0.05677 * Math.Cos(2 * x),
                    _ => throw new ArgumentOutOfRangeException(nameof(type)),
                };
            }

            var sum = 0.0;
            foreach (var v in w)
            {
                sum += Math.Abs(v);
            }
            if (sum > 0)
            {
                for (var i = 0; i < size; i++)
                {
                    w[i] *= 2 / sum;
                }
            }

            return w;
        }
    }

    public static class Framing
    {
        /// <summary>
        /// Frame start positions. With startFromZero the first frame begins at sample 0 and the frame that
        /// reaches the end is the last one; otherwise the first frame is centred on sample 0 and the last
        /// one is the first whose centre is at or past the end. Partial frames are zero-padded.
        /// </summary>
        public static IEnumerable<int> Starts(int length, int frameSize, int hopSize, bool startFromZero)
        {
            if (length <= 0)
            {
                yield break;
            }

            var start = startFromZero ? 0 : -((frameSize + 1) / 2);
            while (start < length)
            {
                yield return start;
                var last = startFromZero ? start + frameSize >= length : start + frameSize / 2 >= length;
                if (last)
                {
                    yield break;
                }
                start += hopSize;
            }
        }

        /// <summary>Copies signal[start..start+window.Length) into frame, zero-padding outside the signal, and applies the window.</summary>
        public static void Fill(ReadOnlySpan<float> signal, int start, double[] window, Span<double> frame)
        {
            if (start >= 0 && start + window.Length <= signal.Length)
            {
                VectorMath.Widen(signal.Slice(start, window.Length), frame);
                VectorMath.Multiply(frame[..window.Length], window, frame);
                return;
            }
            for (var i = 0; i < window.Length; i++)
            {
                var j = start + i;
                frame[i] = j >= 0 && j < signal.Length ? signal[j] * window[i] : 0;
            }
        }
    }

    public readonly record struct SpectralPeak(double Frequency, double Magnitude);

    public enum PeakPicking
    {
        /// <summary>
        /// Essentia's rules, including the forced peak at the first bin of the range. Use for model features so they
        /// match the AcousticBrainz data the models are trained on.
        /// </summary>
        AcousticBrainz,

        /// <summary>Only true local maxima. Use when the value itself must be right, such as key compatibility between tracks.</summary>
        Accurate,
    }

    public static class SpectralPeaks
    {
        /// <summary>
        /// Peaks of a magnitude spectrum in ascending frequency, following Essentia's PeakDetection rules:
        /// <list type="bullet">
        /// <item>The scan starts at the first bin at or above minFrequency. In <see cref="PeakPicking.AcousticBrainz"/>
        /// mode, if the spectrum falls from that bin it is a peak at the exact bin frequency (no interpolation), even
        /// though it is not a local maximum. With a 4096-point FFT that is bin 4 = 43.07 Hz, which is why
        /// AcousticBrainz's tuning reads 434.19 Hz and its HPCPs lean towards F. <see cref="PeakPicking.Accurate"/>
        /// mode starts one bin lower instead, so a real maximum at the first bin is still found and interpolated, and
        /// drops anything that interpolates below minFrequency.</item>
        /// <item>Interior single-bin peaks are refined with parabolic interpolation; plateaus report their midpoint
        /// and first value.</item>
        /// <item>The threshold is strict and tested on the raw value; the scan stops at the first interpolated peak
        /// above maxFrequency.</item>
        /// </list>
        /// </summary>
        public static void Find(ReadOnlySpan<double> x, int sampleRate, double minFrequency, double maxFrequency,
            double threshold, List<SpectralPeak> peaks, PeakPicking picking = PeakPicking.AcousticBrainz)
        {
            peaks.Clear();
            var n = x.Length;
            if (n < 3)
            {
                return;
            }

            var binHz = sampleRate / 2.0 / (n - 1);
            var i = Math.Max(0, (int)Math.Ceiling(minFrequency / binHz));
            if (picking == PeakPicking.Accurate)
            {
                i = Math.Max(1, i - 1);
            }
            else if (i + 1 < n && x[i] > x[i + 1] && x[i] > threshold)
            {
                peaks.Add(new SpectralPeak(i * binHz, x[i]));
            }

            while (i + 1 < n - 1)
            {
                while (i + 1 < n - 1 && x[i] >= x[i + 1])
                {
                    i++;
                }
                while (i + 1 < n - 1 && x[i] < x[i + 1])
                {
                    i++;
                }

                var j = i;
                while (j + 1 < n - 1 && x[j] == x[j + 1])
                {
                    j++;
                }

                if (j + 1 < n - 1 && x[j + 1] < x[j] && x[j] > threshold)
                {
                    double position;
                    double value;
                    if (j == i)
                    {
                        var a = x[j - 1];
                        var b = x[j];
                        var c = x[j + 1];
                        var denom = a - 2 * b + c;
                        var offset = denom != 0 ? 0.5 * (a - c) / denom : 0;
                        position = j + offset;
                        value = b - 0.25 * (a - c) * offset;
                    }
                    else
                    {
                        position = (i + j) / 2.0;
                        value = x[i];
                    }

                    var frequency = position * binHz;
                    if (frequency > maxFrequency)
                    {
                        break;
                    }
                    if (picking == PeakPicking.AcousticBrainz || frequency >= minFrequency)
                    {
                        peaks.Add(new SpectralPeak(frequency, value));
                    }
                }

                // On finite input the next pass always moves past j; a NaN fails every comparison and would stall here.
                i = Math.Max(j, i + 1);
            }
        }
    }
}
