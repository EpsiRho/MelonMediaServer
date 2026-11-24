using System.Buffers.Binary;
using NAudio.Wave;
using Nova.Dsp;

namespace MelonWebApi.Services
{
    /// <summary>Runs an NAudio sample stream through a Nova <see cref="Equalizer"/>.</summary>
    public sealed class EqSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly Equalizer _eq;

        public EqSampleProvider(ISampleProvider source, EqualizerSettings settings)
        {
            _source = source;
            _eq = new Equalizer(settings, source.WaveFormat.SampleRate, source.WaveFormat.Channels);
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            var read = _source.Read(buffer, offset, count);
            var whole = read - read % WaveFormat.Channels;
            if (whole > 0) _eq.Process(buffer.AsSpan(offset, whole));
            return read;
        }
    }

    /// <summary>
    /// A track decoded, equalized and presented as a PCM WAV file, produced as it's read. Playback starts at once (no
    /// full-track encode first), and it's seekable with a known length, so HTTP Range requests work: a seek decodes
    /// from a little before the target so the filters settle, then continues from there.
    /// <para>Output is 16-bit with TPDF dither, or 24-bit when the source has more than 16 bits.</para>
    /// </summary>
    public sealed class EqWaveStream : Stream
    {
        private const int HeaderBytes = 44;
        private const int BlockFrames = 4096;
        /// <summary>Audio decoded before a seek target and thrown away while the filters settle (the slowest
        /// band, a narrow 10 Hz bell, decays well under this).</summary>
        private const double WarmupSeconds = 0.5;

        private readonly Melon.LocalClasses.AudioFiles.AudioReader _reader;
        private readonly Equalizer _eq;
        private readonly int _channels, _bytesPerSample, _blockAlign;
        private readonly long _frames, _length;
        private readonly byte[] _header;
        private readonly float[] _samples;
        private readonly float[] _readBuffer;
        private readonly byte[] _pcm;
        private long _position;
        private long _decoderFrame;   // next frame the decoder + filters will produce
        private long _pcmFrame = -1;  // first frame held in _pcm
        private int _pcmFrames;       // frames held in _pcm
        private uint _rng = 0x9E3779B9;

        public EqWaveStream(string path, EqualizerSettings settings, int sourceBits)
        {
            _reader = Melon.LocalClasses.AudioFiles.OpenReader(path);
            try
            {
                var format = _reader.WaveFormat;
                _channels = format.Channels;
                _eq = new Equalizer(settings, format.SampleRate, _channels);
                _bytesPerSample = sourceBits > 16 ? 3 : 2;
                _blockAlign = _channels * _bytesPerSample;
                _frames = _reader.Length / (sizeof(float) * _channels);
                _length = HeaderBytes + _frames * _blockAlign;
                _header = Header(format.SampleRate, _channels, _bytesPerSample * 8, _frames * _blockAlign);
                _samples = new float[BlockFrames * _channels];
                _readBuffer = new float[BlockFrames * _channels];
                _pcm = new byte[BlockFrames * _blockAlign];
            }
            catch
            {
                _reader.Dispose();
                throw;
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set => _position = Math.Clamp(value, 0, _length);
        }

        public override long Seek(long offset, SeekOrigin origin) => Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            _ => _length + offset,
        };

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var total = 0;
            while (buffer.Length > 0 && _position < _length)
            {
                int n;
                if (_position < HeaderBytes)
                {
                    n = (int)Math.Min(buffer.Length, HeaderBytes - _position);
                    _header.AsSpan((int)_position, n).CopyTo(buffer);
                }
                else
                {
                    var data = _position - HeaderBytes;
                    var frame = data / _blockAlign;
                    if (_pcmFrame < 0 || frame < _pcmFrame || frame >= _pcmFrame + _pcmFrames) Fill(frame);
                    var start = (int)(data - _pcmFrame * _blockAlign);
                    n = Math.Min(buffer.Length, _pcmFrames * _blockAlign - start);
                    _pcm.AsSpan(start, n).CopyTo(buffer);
                }
                buffer = buffer[n..];
                _position += n;
                total += n;
            }
            return total;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Task.FromResult(Read(buffer, offset, count));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Read(buffer.Span));

        /// <summary>Decodes and filters the block starting at <paramref name="frame"/> into <see cref="_pcm"/>.</summary>
        private void Fill(long frame)
        {
            if (frame != _decoderFrame) SeekDecoder(frame);
            var frames = (int)Math.Min(BlockFrames, _frames - frame);
            var got = Decode(_samples.AsSpan(0, frames * _channels)) / _channels;
            _eq.Process(_samples.AsSpan(0, got * _channels));
            // The decoder's length is an estimate for some formats; pad a short ending with silence.
            Array.Clear(_samples, got * _channels, (frames - got) * _channels);
            Convert(_samples.AsSpan(0, frames * _channels), _pcm);
            _decoderFrame = frame + frames;
            _pcmFrame = frame;
            _pcmFrames = frames;
        }

        private void SeekDecoder(long frame)
        {
            var warmup = Math.Min(frame, (long)(_reader.WaveFormat.SampleRate * WarmupSeconds));
            _reader.Position = (frame - warmup) * _channels * sizeof(float);
            _eq.Reset();
            while (warmup > 0)
            {
                var chunk = (int)Math.Min(BlockFrames, warmup);
                var got = Decode(_samples.AsSpan(0, chunk * _channels)) / _channels;
                if (got == 0) break;
                _eq.Process(_samples.AsSpan(0, got * _channels));
                warmup -= got;
            }
            _decoderFrame = frame;
        }

        /// <summary>Reads whole frames until <paramref name="dst"/> is full or the file ends; returns samples read.</summary>
        private int Decode(Span<float> dst)
        {
            var filled = 0;
            while (filled < dst.Length)
            {
                var read = _reader.Read(_readBuffer, 0, dst.Length - filled);
                if (read <= 0) break;
                _readBuffer.AsSpan(0, read).CopyTo(dst[filled..]);
                filled += read;
            }
            return filled - filled % _channels;
        }

        private void Convert(ReadOnlySpan<float> src, Span<byte> dst)
        {
            if (_bytesPerSample == 3)
            {
                for (var i = 0; i < src.Length; i++)
                {
                    var v = (int)Math.Clamp(Math.Round(src[i] * 8388607.0), -8388608, 8388607);
                    dst[i * 3] = (byte)v;
                    dst[i * 3 + 1] = (byte)(v >> 8);
                    dst[i * 3 + 2] = (byte)(v >> 16);
                }
                return;
            }
            for (var i = 0; i < src.Length; i++)
            {
                // TPDF dither (two uniform values, ±1 LSB) decorrelates rounding error from the music.
                var dither = (Next() - Next()) * (1.0 / uint.MaxValue);
                var v = (short)Math.Clamp(Math.Round(src[i] * 32767.0 + dither), -32768, 32767);
                BinaryPrimitives.WriteInt16LittleEndian(dst[(i * 2)..], v);
            }
        }

        private double Next()
        {
            // xorshift32
            var x = _rng;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _rng = x;
            return x;
        }

        private static byte[] Header(int sampleRate, int channels, int bits, long dataBytes)
        {
            var h = new byte[HeaderBytes];
            var size = (uint)Math.Min(dataBytes, uint.MaxValue - HeaderBytes);
            "RIFF"u8.CopyTo(h);
            BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(4), size + HeaderBytes - 8);
            "WAVEfmt "u8.CopyTo(h.AsSpan(8));
            BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(16), 16);
            BinaryPrimitives.WriteUInt16LittleEndian(h.AsSpan(20), 1); // PCM
            BinaryPrimitives.WriteUInt16LittleEndian(h.AsSpan(22), (ushort)channels);
            BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(24), (uint)sampleRate);
            BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(28), (uint)(sampleRate * channels * bits / 8));
            BinaryPrimitives.WriteUInt16LittleEndian(h.AsSpan(32), (ushort)(channels * bits / 8));
            BinaryPrimitives.WriteUInt16LittleEndian(h.AsSpan(34), (ushort)bits);
            "data"u8.CopyTo(h.AsSpan(36));
            BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(40), size);
            return h;
        }

        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _reader.Dispose();
            base.Dispose(disposing);
        }
    }
}
