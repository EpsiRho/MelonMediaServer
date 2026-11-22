using Nova.Audio;
using Nova.Dsp;

namespace Nova.Features
{
    /// <summary>
    /// ReplayGain as AcousticBrainz computed it (metadata.audio_properties.replay_gain): equal-loudness filter,
    /// mean-square power of non-overlapping 50 ms frames, the 95th-percentile frame in dB, subtracted from the
    /// level of Essentia's reference pink noise.
    /// </summary>
    public static class ReplayGain
    {
        private const double ReferenceDb = -31.462667465209961;

        /// <summary>The analysis applies the gain plus a 6 dB preamp, as Essentia's loader (and most players) do.</summary>
        private const double PreampDb = 6.0;

        public static double Compute(MonoSignal signal) => FromEqualLoudness(EqualLoudness.Apply(signal.Samples), signal.SampleRate);

        /// <summary>Replay gain from a signal that has already been through <see cref="EqualLoudness.Apply"/>.</summary>
        public static double FromEqualLoudness(float[] filtered, int sampleRate)
        {
            var frameSize = (int)(0.05 * sampleRate);
            var frames = filtered.Length / frameSize;
            if (frames == 0)
            {
                return 0;
            }

            var powers = new double[frames];
            for (var f = 0; f < frames; f++)
            {
                var sum = 0.0;
                for (var i = f * frameSize; i < (f + 1) * frameSize; i++)
                {
                    sum += (double)filtered[i] * filtered[i];
                }
                powers[f] = sum / frameSize;
            }

            Array.Sort(powers);
            var p95 = powers[(int)(0.95 * frames)];
            var db = p95 < 1e-9 ? -90 : 10 * Math.Log10(p95);
            return ReferenceDb - db;
        }

        /// <summary>The signal every AcousticBrainz descriptor was computed on: scaled by the gain plus the 6 dB preamp.</summary>
        public static MonoSignal Normalize(MonoSignal signal, double gainDb)
        {
            var factor = (float)Math.Pow(10, (gainDb + PreampDb) / 20);
            var samples = new float[signal.Samples.Length];
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] = signal.Samples[i] * factor;
            }
            return new MonoSignal(samples, signal.SampleRate);
        }
    }
}
