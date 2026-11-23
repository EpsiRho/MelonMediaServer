using System.Collections.Concurrent;
using System.Security.Cryptography;
using SkiaSharp;

namespace MelonWebApi.Services;

/// <summary>
/// Scaled-down copies of artwork for the art endpoints' optional <c>size</c> parameter. Clients showing a 160px card
/// don't need a multi-megabyte 2000px upload: each picture is shrunk once per size, kept in a disk cache keyed by the
/// picture's own content (so changed art gets a fresh copy), and the cache is trimmed oldest-first.
/// </summary>
internal static class ArtResizer
{
    public const int MinSize = 16, MaxSize = 2048;
    private static readonly int[] Buckets = [64, 128, 256, 512, 1024, MaxSize];
    private static readonly ConcurrentDictionary<string, Lazy<Task<(byte[] Bytes, string Mime)>>> _inflight = new();
    private static readonly string Dir = Path.Combine(Path.GetTempPath(), "MelonArtCache");
    private const long MaxBytes = 512L * 1024 * 1024;
    private static int _writes;

    /// <summary>
    /// The picture fitted within <paramref name="size"/>×<paramref name="size"/> pixels (aspect kept). Pictures already
    /// that small, sizes of 0 or less, and anything that can't be decoded come back unchanged.
    /// </summary>
    public static async Task<(byte[] Bytes, string Mime)> Fit(byte[] source, string mime, int size)
    {
        if (size <= 0 || source.Length == 0) return (source, mime);
        // Snap up to a few fixed sizes so callers can't force a fresh decode + encode with every distinct number.
        size = Buckets.FirstOrDefault(b => b >= size, MaxSize);
        var hash = Convert.ToHexString(SHA1.HashData(source))[..20];
        var key = $"{hash}_{size}";
        foreach (var ext in new[] { "jpg", "png" })
        {
            var hit = Path.Combine(Dir, $"{key}.{ext}");
            if (!File.Exists(hit)) continue;
            try
            {
                var bytes = await File.ReadAllBytesAsync(hit);
                try { File.SetLastAccessTimeUtc(hit, DateTime.UtcNow); } catch { }
                return (bytes, ext == "png" ? "image/png" : "image/jpeg");
            }
            catch { break; } // being replaced / trimmed right now: make it again
        }
        var lazy = _inflight.GetOrAdd(key, k => new Lazy<Task<(byte[], string)>>(() => Task.Run(() => Make(source, mime, size, k))));
        try { return await lazy.Value; }
        finally { if (lazy.Value.IsCompleted) _inflight.TryRemove(key, out _); }
    }

    private static (byte[] Bytes, string Mime) Make(byte[] source, string mime, int size, string key)
    {
        try
        {
            using var data = SKData.CreateCopy(source);
            using var codec = SKCodec.Create(data);
            if (codec == null) return (source, mime);
            var info = codec.Info;
            // Sideways-stored photos (EXIF orientation 5–8) swap width and height once turned upright.
            bool swap = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            int upW = swap ? info.Height : info.Width, upH = swap ? info.Width : info.Height;
            if (Math.Max(upW, upH) <= size) return (source, mime);

            double k = (double)size / Math.Max(upW, upH);
            int outW = Math.Max(1, (int)Math.Round(upW * k)), outH = Math.Max(1, (int)Math.Round(upH * k));

            // JPEGs can decode straight at 1/2, 1/4 or 1/8 scale: far less work and memory than full size.
            var scaled = codec.GetScaledDimensions((float)Math.Max(k, 1.0 / 8));
            if (scaled.Width < (swap ? outH : outW) || scaled.Height < (swap ? outW : outH)) scaled = new SKSizeI(info.Width, info.Height);
            bool opaque = info.AlphaType == SKAlphaType.Opaque;
            var decodeInfo = new SKImageInfo(scaled.Width, scaled.Height, SKColorType.Rgba8888, opaque ? SKAlphaType.Opaque : SKAlphaType.Premul);
            using var decoded = new SKBitmap(decodeInfo);
            var result = codec.GetPixels(decodeInfo, decoded.GetPixels());
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput)) return (source, mime);

            using var surface = SKSurface.Create(new SKImageInfo(outW, outH, SKColorType.Rgba8888, decodeInfo.AlphaType));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            ApplyOrigin(canvas, codec.EncodedOrigin, outW, outH);
            using var image = SKImage.FromBitmap(decoded);
            var dest = swap ? new SKRect(0, 0, outH, outW) : new SKRect(0, 0, outW, outH);
            canvas.DrawImage(image, dest, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            canvas.Flush();

            using var snap = surface.Snapshot();
            using var encoded = opaque ? snap.Encode(SKEncodedImageFormat.Jpeg, 88) : snap.Encode(SKEncodedImageFormat.Png, 100);
            if (encoded == null) return (source, mime);
            var bytes = encoded.ToArray();
            var outMime = opaque ? "image/jpeg" : "image/png";
            Save(key, opaque ? "jpg" : "png", bytes);
            return (bytes, outMime);
        }
        catch (Exception e)
        {
            Serilog.Log.Warning($"[MWAPI] Art resize to {size}px failed, sending original: {e.Message}");
            return (source, mime);
        }
    }

    /// <summary>Turns the canvas so the stored picture, drawn into its unrotated rectangle, comes out upright.</summary>
    internal static void ApplyOrigin(SKCanvas c, SKEncodedOrigin origin, int w, int h)
    {
        switch (origin)
        {
            case SKEncodedOrigin.TopRight: c.Translate(w, 0); c.Scale(-1, 1); break;
            case SKEncodedOrigin.BottomRight: c.Translate(w, h); c.RotateDegrees(180); break;
            case SKEncodedOrigin.BottomLeft: c.Translate(0, h); c.Scale(1, -1); break;
            case SKEncodedOrigin.LeftTop: c.Scale(-1, 1); c.RotateDegrees(90); break;
            case SKEncodedOrigin.RightTop: c.Translate(w, 0); c.RotateDegrees(90); break;
            case SKEncodedOrigin.RightBottom: c.Translate(w, h); c.Scale(1, -1); c.RotateDegrees(90); break;
            case SKEncodedOrigin.LeftBottom: c.Translate(0, h); c.RotateDegrees(-90); break;
        }
    }

    private static void Save(string key, string ext, byte[] bytes)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var dest = Path.Combine(Dir, $"{key}.{ext}");
            var part = dest + "." + Guid.NewGuid().ToString("N") + ".part";
            File.WriteAllBytes(part, bytes);
            File.Move(part, dest, true);
            if (Interlocked.Increment(ref _writes) % 64 == 0) _ = Task.Run(Trim);
        }
        catch { }
    }

    private static void Trim()
    {
        try
        {
            var files = new DirectoryInfo(Dir).EnumerateFiles().Where(f => f.Extension is ".jpg" or ".png").OrderBy(f => f.LastAccessTimeUtc).ToList();
            long total = files.Sum(f => f.Length);
            foreach (var f in files)
            {
                if (total <= MaxBytes) break;
                try { total -= f.Length; f.Delete(); } catch { }
            }
        }
        catch { }
    }
}
