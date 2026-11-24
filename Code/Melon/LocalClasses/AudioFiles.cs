using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Melon.LocalClasses;

/// <summary>
/// Opens track files for decoding and serving. Windows Media Foundation (which NAudio's AudioFileReader uses, and so do
/// Windows clients) won't open a FLAC whose first audio frame starts past 4 MiB: it fails with 0xC00D36C4 "byte stream
/// type unsupported". That happens when the file embeds a huge cover (seen: 4–6 MB PNGs) or padding. For those files
/// the audio is read through a view of the file without its PICTURE and PADDING blocks; the audio bytes are untouched,
/// and seek tables stay valid because their offsets count from the first frame.
/// </summary>
public static class AudioFiles
{
    /// <summary>Largest first-frame offset Media Foundation accepts (measured: 4,194,281 opened, 4,194,329 didn't).</summary>
    private const long MediaFoundationLimit = 4L * 1024 * 1024 - 64;

    /// <summary>The file as a seekable read-only stream that Media Foundation can open (the file itself unless it needs trimming).</summary>
    public static Stream OpenRead(string path)
    {
        var header = FlacHeaderFor(path, out var audioOffset);
        var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, true);
        return header == null ? file : new FlacViewStream(header, file, audioOffset);
    }

    /// <summary>True if the file is a FLAC that Media Foundation can only open through <see cref="OpenRead"/>.</summary>
    public static bool NeedsTrim(string path) => FlacHeaderFor(path, out _) != null;

    /// <summary>A replacement for NAudio's AudioFileReader that also opens FLACs with oversized metadata.</summary>
    public static AudioReader OpenReader(string path) => new(path);

    /// <summary>
    /// For a FLAC whose audio starts too far in, the metadata to put in front of the audio instead (STREAMINFO, SEEKTABLE,
    /// tags and cue sheet; never pictures or padding). Null when the file isn't such a FLAC.
    /// </summary>
    private static byte[]? FlacHeaderFor(string path, out long audioOffset)
    {
        audioOffset = 0;
        if (!path.EndsWith(".flac", StringComparison.OrdinalIgnoreCase)) return null;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096);
            var head = new byte[10];
            if (fs.Read(head, 0, 10) < 10) return null;
            long start = 0;
            if (head[0] == 'I' && head[1] == 'D' && head[2] == '3')
                start = 10 + ((head[6] & 0x7f) << 21 | (head[7] & 0x7f) << 14 | (head[8] & 0x7f) << 7 | (head[9] & 0x7f));
            fs.Position = start;
            var magic = new byte[4];
            if (fs.Read(magic, 0, 4) < 4 || magic[0] != 'f' || magic[1] != 'L' || magic[2] != 'a' || magic[3] != 'C') return null;

            var keep = new List<(int Type, byte[] Body)>();
            long pos = start + 4;
            var bh = new byte[4];
            while (true)
            {
                fs.Position = pos;
                if (fs.Read(bh, 0, 4) < 4) return null;
                int type = bh[0] & 0x7f;
                int len = bh[1] << 16 | bh[2] << 8 | bh[3];
                bool last = (bh[0] & 0x80) != 0;
                if (type is 0 or 3 or 4 or 5 && len <= 1024 * 1024)
                {
                    var body = new byte[len];
                    fs.ReadExactly(body);
                    keep.Add((type, body));
                }
                pos += 4 + len;
                if (last) break;
                if (pos >= fs.Length) return null;
            }
            if (pos <= MediaFoundationLimit) return null;
            if (keep.Count == 0 || keep[0].Type != 0) return null; // STREAMINFO must come first
            audioOffset = pos;

            using var ms = new MemoryStream();
            ms.Write("fLaC"u8);
            for (int i = 0; i < keep.Count; i++)
            {
                var (type, body) = keep[i];
                ms.WriteByte((byte)(type | (i == keep.Count - 1 ? 0x80 : 0)));
                ms.WriteByte((byte)(body.Length >> 16)); ms.WriteByte((byte)(body.Length >> 8)); ms.WriteByte((byte)body.Length);
                ms.Write(body);
            }
            return ms.ToArray();
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    /// <summary>A rebuilt FLAC header followed by the original file from its first audio frame.</summary>
    private sealed class FlacViewStream : Stream
    {
        private readonly byte[] _header;
        private readonly FileStream _file;
        private readonly long _audioOffset;
        private long _pos;

        public FlacViewStream(byte[] header, FileStream file, long audioOffset)
        {
            _header = header; _file = file; _audioOffset = audioOffset;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _header.Length + _file.Length - _audioOffset;
        public override long Position { get => _pos; set => _pos = Math.Clamp(value, 0, Length); }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            int total = 0;
            if (_pos < _header.Length && buffer.Length > 0)
            {
                int n = (int)Math.Min(buffer.Length, _header.Length - _pos);
                _header.AsSpan((int)_pos, n).CopyTo(buffer);
                _pos += n; total += n; buffer = buffer[n..];
            }
            if (buffer.Length > 0 && _pos < Length)
            {
                _file.Position = _audioOffset + (_pos - _header.Length);
                int n = _file.Read(buffer);
                _pos += n; total += n;
            }
            return total;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (_pos < _header.Length) return Read(buffer.Span);
            if (buffer.Length == 0 || _pos >= Length) return 0;
            _file.Position = _audioOffset + (_pos - _header.Length);
            int n = await _file.ReadAsync(buffer, ct);
            _pos += n;
            return n;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

        public override long Seek(long offset, SeekOrigin origin) => Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _pos + offset,
            _ => Length + offset,
        };

        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _file.Dispose(); base.Dispose(disposing); }
    }

    /// <summary>
    /// Interleaved 32-bit float samples, like NAudio's AudioFileReader (which it is, for every file that doesn't need
    /// trimming). Length and Position are in bytes of float output.
    /// </summary>
    public sealed class AudioReader : WaveStream, ISampleProvider
    {
        private readonly AudioFileReader? _plain;
        private readonly StreamMediaFoundationReader? _mf;
        private readonly SampleChannel? _channel;
        private readonly Stream? _stream;
        private readonly int _sourceBytesPerFrame, _destBytesPerFrame;
        private readonly object _lock = new();

        public AudioReader(string path)
        {
            if (!NeedsTrim(path)) { _plain = new AudioFileReader(path); return; }
            var stream = _stream = OpenRead(path);
            try
            {
                _mf = new StreamMediaFoundationReader(stream);
                _channel = new SampleChannel(_mf, false);
                _sourceBytesPerFrame = _mf.WaveFormat.BlockAlign;
                _destBytesPerFrame = sizeof(float) * _channel.WaveFormat.Channels;
            }
            catch
            {
                _mf?.Dispose();
                stream.Dispose();
                throw;
            }
        }

        public override WaveFormat WaveFormat => _plain?.WaveFormat ?? _channel!.WaveFormat;

        public override long Length => _plain?.Length ?? _mf!.Length / _sourceBytesPerFrame * _destBytesPerFrame;

        public override long Position
        {
            get => _plain?.Position ?? _mf!.Position / _sourceBytesPerFrame * _destBytesPerFrame;
            set
            {
                if (_plain != null) { _plain.Position = value; return; }
                lock (_lock) _mf!.Position = value / _destBytesPerFrame * _sourceBytesPerFrame;
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (_plain != null) return _plain.Read(buffer, offset, count);
            lock (_lock) return _channel!.Read(buffer, offset, count);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_plain != null) return _plain.Read(buffer, offset, count);
            var wb = new WaveBuffer(buffer);
            return Read(wb.FloatBuffer, offset / 4, count / 4) * 4;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _plain?.Dispose();
                _mf?.Dispose();
                _stream?.Dispose(); // StreamMediaFoundationReader doesn't dispose the stream it was given
            }
            base.Dispose(disposing);
        }
    }
}
