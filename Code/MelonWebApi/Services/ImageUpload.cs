using SkiaSharp;

namespace MelonWebApi.Services;

/// <summary>
/// Turns an uploaded picture into bytes the server made itself. The client's file name, <c>Content-Type</c> and bytes are
/// never trusted: the upload must decode as an image, is turned upright and re-encoded (JPEG, or PNG when it has
/// transparency), and the file extension comes from that re-encode. Anything that isn't a picture, is too big, or decodes
/// to too many pixels is refused.
/// </summary>
internal static class ImageUpload
{
    /// <summary>Largest upload accepted, in bytes: the administrator's setting (local controllers only), 20 MB by default.</summary>
    public static long MaxBytes => MelonWebApi.Middleware.UploadLimitAttribute.Bytes(MelonWebApi.Middleware.UploadKind.Artwork);
    /// <summary>Longest side kept; larger pictures are scaled down when re-encoded.</summary>
    public const int MaxSide = 4096;
    // Guards against decompression bombs: a tiny PNG can claim 50,000×50,000 pixels.
    private const long MaxSourcePixels = 100L * 1000 * 1000;

    public sealed record Result(byte[] Bytes, string Extension, string Mime);

    /// <summary>
    /// The decoded and re-encoded picture, or null with the reason in <c>Error</c> when the upload isn't acceptable.
    /// <paramref name="forceJpeg"/> always writes JPEG (for files read back under a fixed name, like the default artwork).
    /// </summary>
    public static async Task<(Result? Image, string? Error)> Read(IFormFile? file, bool forceJpeg = false)
    {
        if (file == null || file.Length == 0) return (null, "No image");
        long max = MaxBytes;
        if (file.Length > max) return (null, $"Image is larger than {max / (1024 * 1024)} MB");

        byte[] source;
        using (var ms = new MemoryStream((int)file.Length))
        {
            await using var input = file.OpenReadStream();
            await input.CopyToAsync(ms);
            if (ms.Length > max) return (null, "Image too large");
            source = ms.ToArray();
        }

        try { return (Encode(source, forceJpeg), null); }
        catch (InvalidDataException e) { return (null, e.Message); }
        catch (Exception) { return (null, "Not a supported image"); }
    }

    /// <summary>Embedded-art variant for track files: same checks, returns the re-encoded bytes.</summary>
    public static Result Encode(byte[] source, bool forceJpeg = false)
    {
        using var data = SKData.CreateCopy(source);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("Not a supported image");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > MaxSourcePixels)
            throw new InvalidDataException("Image dimensions are too large");

        bool swap = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int upW = swap ? info.Height : info.Width, upH = swap ? info.Width : info.Height;
        double k = Math.Min(1.0, (double)MaxSide / Math.Max(upW, upH));
        int outW = Math.Max(1, (int)Math.Round(upW * k)), outH = Math.Max(1, (int)Math.Round(upH * k));

        bool opaque = forceJpeg || info.AlphaType == SKAlphaType.Opaque;
        // Decode with the picture's own transparency (Skia can't decode a transparent picture straight to opaque);
        // a JPEG result is flattened onto black when drawn below.
        var decodeInfo = new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888,
            info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul);
        using var decoded = new SKBitmap(decodeInfo);
        var result = codec.GetPixels(decodeInfo, decoded.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput)) throw new InvalidDataException("Image could not be decoded");

        using var surface = SKSurface.Create(new SKImageInfo(outW, outH, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new InvalidDataException("Image could not be decoded");
        var canvas = surface.Canvas;
        canvas.Clear(opaque ? SKColors.Black : SKColors.Transparent);
        ArtResizer.ApplyOrigin(canvas, codec.EncodedOrigin, outW, outH);
        using var image = SKImage.FromBitmap(decoded);
        var dest = swap ? new SKRect(0, 0, outH, outW) : new SKRect(0, 0, outW, outH);
        canvas.DrawImage(image, dest, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        canvas.Flush();

        using var snap = surface.Snapshot();
        using var encoded = opaque ? snap.Encode(SKEncodedImageFormat.Jpeg, 92) : snap.Encode(SKEncodedImageFormat.Png, 100);
        if (encoded == null) throw new InvalidDataException("Image could not be encoded");
        return opaque ? new Result(encoded.ToArray(), "jpeg", "image/jpeg") : new Result(encoded.ToArray(), "png", "image/png");
    }
}

/// <summary>Builds file paths from names that may have come from a request or the database, refusing any that escape the folder.</summary>
internal static class SafePath
{
    /// <summary>
    /// <paramref name="name"/> inside <paramref name="baseDir"/>. Throws <see cref="UnauthorizedAccessException"/> when the
    /// name has directory parts, is rooted, or would otherwise resolve outside the folder.
    /// </summary>
    public static string Combine(string baseDir, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name) || name.Contains(':') || name is "." or "..")
            throw new UnauthorizedAccessException($"Invalid file name: {name}");
        var root = Path.GetFullPath(baseDir);
        if (!root.EndsWith(Path.DirectorySeparatorChar)) root += Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, name));
        if (!full.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new UnauthorizedAccessException($"Invalid file name: {name}");
        return full;
    }
}
