using Nova.Audio;
using Nova.Dsp;
using Nova.Features;

namespace Nova.Temporal
{
    /// <summary>
    /// Everything Nova can say about a track in time order, which AcousticBrainz's summaries throw away (F1): the beat
    /// grid, one row of features per beat, the folded onset pattern with swing and syncopation, tempo steadiness,
    /// bar-level structure, clock-time rows that don't depend on the grid, and how far to trust the grid at all.
    /// Computed on the replay-gain-normalised signal, like every other descriptor.
    /// </summary>
    public sealed record TemporalFeatures(
        BeatGrid Grid,
        BeatSequence Beats,
        OnsetPattern Pattern,
        TempoStability Tempo,
        BarStructure Structure,
        ClockSeries Clock,
        RhythmValidity Validity)
    {
        /// <summary>
        /// Bumped whenever the output changes meaning or layout, so stored features from different versions are never
        /// mixed. 1: the first F1 run (never written to a corpus). 2: validity, clock rows, 48-step and per-bar patterns,
        /// syncopation per phase. 3: salient-pulse line 0.25 (was 0.3), which changes rhythm status, coverage and support. 4: per-band spectral
        /// flatness in the clock rows (noise against tones), for the Energy head.
        /// </summary>
        public const int SchemaVersion = 4;

        /// <summary>Longer tracks (DJ mixes, live sets) get no temporal features unless asked: their cost is unbounded.</summary>
        public const double DefaultMaxSeconds = 30 * 60;

        /// <summary>Frame-wise descriptors the temporal features read from <see cref="SpectralDescriptors"/>.</summary>
        internal static readonly string[] SpectralFrames = { "mfcc", "spectral_flux", "spectral_centroid" };

        /// <summary>From frames Nova has already computed; only the band-onset and band-flatness passes are new.</summary>
        public static TemporalFeatures Compute(MonoSignal signal, double[] beats, FrameSeries mfcc, FrameSeries flux,
            FrameSeries centroid, FrameSeries chroma, FrameSeries? onsets = null, FrameSeries? flatness = null)
        {
            onsets ??= BandOnsets.Compute(signal);
            flatness ??= BandFlatness.Compute(signal);
            var sequence = BeatSequence.Compute(signal, beats, onsets, flux, centroid, chroma, mfcc);
            var grid = BeatGrid.Compute(sequence);
            var clock = ClockSeries.Compute(signal, onsets, flatness, sequence.Count >= 2 ? TempoStability.Median(sequence.Duration) : 0);
            return new TemporalFeatures(
                grid,
                sequence,
                OnsetPattern.Compute(grid, onsets),
                TempoStability.Compute(sequence.Time),
                BarStructure.Compute(grid, sequence),
                clock,
                RhythmValidity.Compute(signal.DurationSeconds, grid, sequence, clock));
        }

        /// <summary>Standalone, for a normalised signal: runs the beat tracker and the frame passes it needs itself.</summary>
        public static TemporalFeatures Compute(MonoSignal signal)
        {
            var tempo = Task.Run(() => Features.Tempo.Compute(signal));
            var spectral = Task.Run(() => SpectralDescriptors.ComputeWithFrames(signal, null, SpectralFrames).Frames);
            var chroma = Task.Run(() => Key.ComputeWithChroma(signal, new[] { PeakPicking.Accurate }, PeakPicking.Accurate).Chroma!);
            var onsets = Task.Run(() => BandOnsets.Compute(signal));
            var flatness = Task.Run(() => BandFlatness.Compute(signal));
            Task.WaitAll(tempo, spectral, chroma, onsets, flatness);
            var frames = spectral.Result;
            return Compute(signal, tempo.Result.Beats, frames["mfcc"], frames["spectral_flux"], frames["spectral_centroid"],
                chroma.Result, onsets.Result, flatness.Result);
        }
    }
}
