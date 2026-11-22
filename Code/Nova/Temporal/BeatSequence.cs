using Nova.Audio;

namespace Nova.Temporal
{
    /// <summary>
    /// One row of features per beat: the track resampled from clock time to musical time, so two tracks at different
    /// tempos line up beat for beat. A beat runs from its time to the next beat's; the last one lasts as long as the
    /// interval before it, cut at the end of the signal. Frame values are averaged over the frames centred inside the
    /// beat; onsets use a window opened a 32nd note early so a hit on the beat counts for that beat. Multi-dimensional columns are stored flat: beat i of Onset is Onset[i·4 .. i·4+4).
    /// </summary>
    /// <param name="Loudness">RMS level of the beat in dB (after replay-gain normalisation), -100 for silence.</param>
    /// <param name="Onset">Onset strength in the four <see cref="BandOnsets"/> bands.</param>
    /// <param name="Chroma">Energy per pitch class (bin 0 = A), each frame scaled to a maximum of 1.</param>
    public sealed record BeatSequence(
        double[] Time,
        double[] Duration,
        double[] Loudness,
        double[] Onset,
        double[] Flux,
        double[] Centroid,
        double[] Chroma,
        double[] Mfcc)
    {
        public const int OnsetDims = BandOnsets.Bands;
        public const int ChromaDims = 12;
        public const int MfccDims = 13;
        private const double SilenceDb = -90;

        public int Count => Time.Length;

        public static BeatSequence Empty { get; } = new(
            Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(),
            Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>());

        public ReadOnlySpan<double> OnsetAt(int beat) => Onset.AsSpan(beat * OnsetDims, OnsetDims);

        public ReadOnlySpan<double> ChromaAt(int beat) => Chroma.AsSpan(beat * ChromaDims, ChromaDims);

        public ReadOnlySpan<double> MfccAt(int beat) => Mfcc.AsSpan(beat * MfccDims, MfccDims);

        public static BeatSequence Compute(MonoSignal signal, double[] beats, FrameSeries onsets, FrameSeries flux,
            FrameSeries centroid, FrameSeries chroma, FrameSeries mfcc)
        {
            if (beats.Length < 2)
            {
                return Empty;
            }
            var n = beats.Length;
            var time = (double[])beats.Clone();
            var duration = new double[n];
            for (var i = 0; i + 1 < n; i++)
            {
                duration[i] = beats[i + 1] - beats[i];
            }
            duration[n - 1] = Math.Max(0, Math.Min(duration[n - 2], signal.DurationSeconds - beats[n - 1]));

            var loudness = new double[n];
            var onset = new double[n * OnsetDims];
            var fluxes = new double[n];
            var centroids = new double[n];
            var chromas = new double[n * ChromaDims];
            var mfccs = new double[n * MfccDims];
            Span<double> one = stackalloc double[1];
            for (var i = 0; i < n; i++)
            {
                double start = time[i], end = time[i] + duration[i];
                loudness[i] = Level(signal, start, end);
                // A hit on the beat starts rising a few frames early (centred windows, beats a touch ahead), so the
                // onset window opens a 32nd note before the beat; otherwise the hit leaks into the previous beat.
                var lead = duration[i] / 8;
                onsets.Mean(start - lead, end - lead, onset.AsSpan(i * OnsetDims, OnsetDims));
                flux.Mean(start, end, one);
                fluxes[i] = one[0];
                centroid.Mean(start, end, one);
                centroids[i] = one[0];
                chroma.Mean(start, end, chromas.AsSpan(i * ChromaDims, ChromaDims));
                mfcc.Mean(start, end, mfccs.AsSpan(i * MfccDims, MfccDims));
            }
            // The beat tracker keeps ticking through digital silence; beats nobody can hear aren't a rhythm.
            if (loudness.All(l => l <= SilenceDb))
            {
                return Empty;
            }
            return new BeatSequence(time, duration, loudness, onset, fluxes, centroids, chromas, mfccs);
        }

        private static double Level(MonoSignal signal, double start, double end)
        {
            var first = Math.Clamp((int)(start * signal.SampleRate), 0, signal.Samples.Length);
            var stop = Math.Clamp((int)(end * signal.SampleRate), first, signal.Samples.Length);
            if (stop == first)
            {
                return -100;
            }
            var sum = 0.0;
            for (var i = first; i < stop; i++)
            {
                sum += (double)signal.Samples[i] * signal.Samples[i];
            }
            var meanSquare = sum / (stop - first);
            return meanSquare < 1e-10 ? -100 : 10 * Math.Log10(meanSquare);
        }
    }
}
