using Melon.LocalClasses;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MelonWebApi.Middleware
{
    public enum UploadKind { Artwork, PlaylistImport }

    /// <summary>
    /// Raises the request body limit for an upload endpoint to the size an administrator set in a local controller
    /// (<see cref="Melon.Models.Settings.MaxArtUploadMegabytes"/> / <see cref="Melon.Models.Settings.MaxPlaylistImportMegabytes"/>).
    /// Runs before model binding, so the limit applies while the form is read.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class UploadLimitAttribute(UploadKind kind) : Attribute, IResourceFilter, IOrderedFilter
    {
        // Headroom for multipart boundaries and the other form fields.
        private const long Overhead = 64 * 1024;
        public int Order => -2000;

        public static long Bytes(UploadKind kind)
        {
            var s = StateManager.MelonSettings;
            int mb = kind == UploadKind.Artwork ? s?.MaxArtUploadMegabytes ?? 20 : s?.MaxPlaylistImportMegabytes ?? 2;
            return Math.Max(1, mb) * 1024L * 1024L;
        }

        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            var limit = Bytes(kind);
            var http = context.HttpContext;
            var size = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (size is { IsReadOnly: false }) size.MaxRequestBodySize = limit + Overhead;
            if (http.Request.HasFormContentType && http.Features.Get<IFormFeature>()?.Form == null)
                http.Features.Set<IFormFeature>(new FormFeature(http.Request, new FormOptions { MultipartBodyLengthLimit = limit + Overhead }));
        }

        public void OnResourceExecuted(ResourceExecutedContext context) { }
    }
}
