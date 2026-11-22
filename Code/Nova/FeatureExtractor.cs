using Nova.Audio;
using Nova.Dsp;
using Nova.Features;
using Nova.Temporal;

namespace Nova
{
    /// <param name="Key">
    /// AcousticBrainz-compatible key and tuning (tonal.key_key / key_scale / key_strength / tuning_frequency).
    /// Feed this to models trained on AcousticBrainz data; it inherits that data's bias towards F-related keys.
    /// </param>
    /// <param name="MusicalKey">Key and tuning from true spectral peaks only. Use this for key compatibility between tracks.</param>
    /// <param name="ReplayGain">Gain in dB (metadata.audio_properties.replay_gain); every descriptor is computed after applying it plus 6 dB.</param>
    /// <param name="DynamicComplexity">Mean deviation in dB of the loudness curve from its average (lowlevel.dynamic_complexity).</param>
    /// <param name="LowLevel">Frame-wise descriptors keyed by their AcousticBrainz lowlevel names.</param>
    /// <param name="Temporal">Features in time order (F1), only when the extractor was created with temporal: true.</param>
    /// <param name="TemporalSkipped">Why Temporal is null although it was asked for (a track over the length limit).</param>
    public sealed record TrackFeatures(
        double DurationSeconds,
        double ReplayGain,
        double AverageLoudness,
        double DynamicComplexity,
        double Bpm,
        double[] Beats,
        KeyResult Key,
        KeyResult MusicalKey,
        IReadOnlyDictionary<string, DescriptorStats> LowLevel,
        TemporalFeatures? Temporal = null,
        string? TemporalSkipped = null);

    /// <summary>
    /// Entry point: decode a file and compute the descriptors implemented so far.
    /// Names and value ranges follow AcousticBrainz's low-level JSON so results can be compared directly.
    /// </summary>
    public sealed class FeatureExtractor
    {
        private static readonly PeakPicking[] KeyModes = { PeakPicking.AcousticBrainz, PeakPicking.Accurate };

        private readonly AudioDecoder _decoder;
        private readonly bool _temporal;
        private readonly double _temporalMaxSeconds;

        /// <param name="temporal">Also compute <see cref="TemporalFeatures"/>; the AcousticBrainz descriptors are unchanged either way.</param>
        /// <param name="temporalMaxSeconds">Skip the temporal features for longer tracks (default 30 minutes).</param>
        public FeatureExtractor(AudioDecoder? decoder = null, bool temporal = false,
            double temporalMaxSeconds = TemporalFeatures.DefaultMaxSeconds)
        {
            _decoder = decoder ?? new AudioDecoder();
            _temporal = temporal;
            _temporalMaxSeconds = temporalMaxSeconds;
        }

        public TrackFeatures Extract(string path) => Extract(_decoder.Decode(path));

        public TrackFeatures Extract(MonoSignal decoded)
        {
            CheckSamples(decoded.Samples);

            // AcousticBrainz normalised loudness before any analysis; absolute energies depend on it.
            // The equal-loudness filter is linear, so the copy filtered for replay gain, scaled by the same gain,
            // is also the filtered normalised signal that the spectral descriptors need.
            var filtered = new MonoSignal(EqualLoudness.Apply(decoded.Samples), decoded.SampleRate);
            var gain = Features.ReplayGain.FromEqualLoudness(filtered.Samples, decoded.SampleRate);
            var signal = Features.ReplayGain.Normalize(decoded, gain);
            var filteredSignal = Features.ReplayGain.Normalize(filtered, gain);
            var tooLong = _temporal && signal.DurationSeconds > _temporalMaxSeconds;
            var withTemporal = _temporal && !tooLong;

            var loudness = Task.Run(() => AverageLoudness.Compute(signal));
            var dynamics = Task.Run(() => Features.DynamicComplexity.Compute(signal));
            var tempo = Task.Run(() => Tempo.Compute(signal));
            // The temporal features reuse the key pass's per-frame chroma (accurate peaks) and the spectral pass's frames.
            var keys = Task.Run(() => Features.Key.ComputeWithChroma(signal, KeyModes, withTemporal ? PeakPicking.Accurate : null));
            var lowLevel = Task.Run(() => SpectralDescriptors.ComputeWithFrames(signal, filteredSignal.Samples,
                withTemporal ? TemporalFeatures.SpectralFrames : Array.Empty<string>()));
            var onsets = withTemporal ? Task.Run(() => BandOnsets.Compute(signal)) : Task.FromResult<FrameSeries?>(null)!;
            var flatness = withTemporal ? Task.Run(() => BandFlatness.Compute(signal)) : Task.FromResult<FrameSeries?>(null)!;
            Task.WaitAll(loudness, dynamics, tempo, keys, lowLevel, onsets, flatness);

            TemporalFeatures? temporal = null;
            if (withTemporal)
            {
                var frames = lowLevel.Result.Frames;
                temporal = TemporalFeatures.Compute(signal, tempo.Result.Beats, frames["mfcc"], frames["spectral_flux"],
                    frames["spectral_centroid"], keys.Result.Chroma!, onsets.Result, flatness.Result);
            }

            return new TrackFeatures(
                signal.DurationSeconds,
                gain,
                loudness.Result,
                dynamics.Result,
                tempo.Result.Bpm,
                tempo.Result.Beats,
                keys.Result.Keys[PeakPicking.AcousticBrainz],
                keys.Result.Keys[PeakPicking.Accurate],
                lowLevel.Result.Stats,
                temporal,
                tooLong ? $"longer than {_temporalMaxSeconds / 60:0.#} minutes" : null);
        }

        // Float files can hold anything. NaN, infinity or values far past full scale (a corrupt upload, or data that
        // isn't audio) would turn every descriptor into NaN, so they are refused instead.
        private const float MaxSample = 1000; // +60 dB over full scale

        private static void CheckSamples(float[] samples)
        {
            var bad = 0;
            foreach (var s in samples)
            {
                if (!(Math.Abs(s) <= MaxSample))
                {
                    bad++;
                }
            }
            if (bad > 0)
            {
                throw new InvalidDataException($"{bad:N0} of {samples.Length:N0} samples are NaN, infinite or over {MaxSample} times full scale; not usable audio.");
            }
        }
    }
}
