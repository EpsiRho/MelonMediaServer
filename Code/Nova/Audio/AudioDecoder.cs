using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Nova.Audio
{
    /// <summary>
    /// Decodes any file ffmpeg can read into a mono float signal at the analysis sample rate.
    /// ffmpeg runs as a separate process, so its license doesn't apply to Nova.
    /// </summary>
    public sealed class AudioDecoder
    {
        public const int AnalysisSampleRate = 44100;

        private readonly string _ffmpegPath;

        public AudioDecoder(string? ffmpegPath = null)
        {
            _ffmpegPath = ffmpegPath ?? Environment.GetEnvironmentVariable("NOVA_FFMPEG") ?? "ffmpeg";
        }

        /// <summary>
        /// Decodes the first audio stream, resamples to 44.1 kHz with soxr and averages the channels,
        /// which matches Essentia's "mix" downmix.
        /// </summary>
        public MonoSignal Decode(string path) => Decode(path, null, null);

        /// <summary>
        /// Decodes only [start, start + seconds) of the file, with ffmpeg seeking before it decodes, so a window of a
        /// long track doesn't cost the whole track. A null bound means the file's own start or end.
        /// </summary>
        public MonoSignal Decode(string path, double? start, double? seconds)
        {
            var info = new ProcessStartInfo(_ffmpegPath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var window = new List<string>();
            if (start is > 0)
            {
                window.AddRange(["-ss", start.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)]);
            }
            if (seconds is { } length)
            {
                window.AddRange(["-t", length.ToString("R", System.Globalization.CultureInfo.InvariantCulture)]);
            }
            foreach (var arg in new[] { "-nostdin", "-hide_banner", "-loglevel", "error" }.Concat(window).Concat(new[]
            {
                "-i", path,
                "-map", "0:a:0", "-vn",
                "-af", $"aresample=resampler=soxr:precision=28:osr={AnalysisSampleRate}",
                "-ac", "2",
                "-f", "f32le", "-acodec", "pcm_f32le",
                "pipe:1",
            }))
            {
                info.ArgumentList.Add(arg);
            }

            using var process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start ffmpeg at '{_ffmpegPath}'.");
            var stderr = process.StandardError.ReadToEndAsync();

            var samples = new List<float>(AnalysisSampleRate * 60 * 5);
            var buffer = new byte[1 << 16];
            var pending = 0;
            var stream = process.StandardOutput.BaseStream;
            int read;
            while ((read = stream.Read(buffer, pending, buffer.Length - pending)) > 0)
            {
                var available = pending + read;
                var whole = available - available % 8; // one stereo frame = 2 floats = 8 bytes
                var frames = MemoryMarshal.Cast<byte, float>(buffer.AsSpan(0, whole));
                for (var i = 0; i < frames.Length; i += 2)
                {
                    samples.Add(0.5f * (frames[i] + frames[i + 1]));
                }

                pending = available - whole;
                if (pending > 0)
                {
                    Buffer.BlockCopy(buffer, whole, buffer, 0, pending);
                }
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"ffmpeg failed to decode '{path}': {stderr.Result.Trim()}");
            }

            return new MonoSignal(samples.ToArray(), AnalysisSampleRate);
        }
    }

    public sealed record MonoSignal(float[] Samples, int SampleRate)
    {
        public double DurationSeconds => (double)Samples.Length / SampleRate;
    }
}
