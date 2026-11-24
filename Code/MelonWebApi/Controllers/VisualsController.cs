using System.Security.Claims;
using Melon.Interface;
using Melon.LocalClasses;
using Melon.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using MongoDB.Driver;
using NAudio.Wave;

namespace MelonWebApi.Controllers
{
    /// <summary>
    /// Pre-computed data for client visualizations: the scrubber waveform, FFTVIS frequency frames in a client-chosen
    /// format, beat and hit times, and moments (soft, energetic, builds, drops). Everything but the waveform is generated
    /// on first request and cached on disk; clients can warm the next tracks with <c>prefetch</c>.
    /// </summary>
    [ApiController]
    [Route("api/visuals")]
    public class VisualsController : ControllerBase
    {
        private const string Disabled = "Visualizations are disabled on this server.";

        private string? CurrentUser => ((ClaimsIdentity?)User.Identity)?.Claims
            .Where(c => c.Type == ClaimTypes.UserData).Select(c => c.Value).FirstOrDefault();

        private static IMongoCollection<Track> Tracks() =>
            new MongoClient(StateManager.MelonSettings.MongoDbConnectionString).GetDatabase("Melon").GetCollection<Track>("Tracks");

        private static Track? FindTrack(string id) =>
            string.IsNullOrEmpty(id) ? null : Tracks().Find(Builders<Track>.Filter.Eq(x => x._id, id)).FirstOrDefault();

        // ── Settings ─────────────────────────────────────────────────────────

        /// <summary>Whether visualizations are on, the default format and the limits for requested formats.</summary>
        [Authorize(Roles = "Admin,User")]
        [HttpGet("settings")]
        public IActionResult GetSettings()
        {
            var s = VisualsService.Instance.GetSettings();
            return Ok(new { s.Enabled, s.Defaults, s.Limits });
        }

        /// <summary>Settings plus cache usage, for administrators.</summary>
        [Authorize(Roles = "Admin")]
        [HttpGet("status")]
        public IActionResult GetStatus() => Ok(VisualsService.Instance.GetStatus());

        /// <summary>Replaces the visualization settings. Limits are applied before defaults.</summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("settings")]
        public IActionResult SetSettings([FromBody] VisualsSettings settings)
        {
            var args = new WebApiEventArgs("api/visuals/settings", CurrentUser, new Dictionary<string, object>());
            try
            {
                var admin = ServerAdministration.Instance;
                admin.SetVisualLimits(settings.Limits);
                admin.SetVisualDefaults(settings.Defaults);
                admin.SetVisualsCache(settings.CacheMegabytes, settings.MaxConcurrentJobs);
                admin.SetVisualsEnabled(settings.Enabled);
            }
            catch (ArgumentException ex)
            {
                args.SendEvent(ex.Message, 400, Program.mWebApi);
                return BadRequest(ex.Message);
            }
            args.SendEvent("Visualization settings updated", 200, Program.mWebApi);
            return Ok(VisualsService.Instance.GetSettings());
        }

        /// <summary>Deletes cached visualization files. Returns the bytes freed.</summary>
        [Authorize(Roles = "Admin")]
        [HttpDelete("cache")]
        public IActionResult ClearCache() => Ok(new { freedBytes = ServerAdministration.Instance.ClearVisualsCache() });

        // ── Data ─────────────────────────────────────────────────────────────

        /// <summary>
        /// RMS loudness of <paramref name="width"/> equal slices of the track, normalised to the loudest (0–1), for a
        /// scrubber waveform. (Moved from api/download/track-wave, which still works.)
        /// </summary>
        [Authorize(Roles = "Admin,User")]
        [MelonWebApi.Middleware.MediaAccess]
        [HttpGet("waveform")]
        public ObjectResult Waveform(string id, float width)
        {
            var args = new WebApiEventArgs("api/visuals/waveform", CurrentUser, new Dictionary<string, object> { { "id", id }, { "width", width } });
            int w = (int)width;
            if (w <= 0 || w > 100_000)
            {
                args.SendEvent("Width parameter is required", 400, Program.mWebApi);
                return new ObjectResult("Width parameter is required (1–100,000).") { StatusCode = 400 };
            }
            var track = FindTrack(id);
            if (track == null)
            {
                args.SendEvent("Track not found", 404, Program.mWebApi);
                return new ObjectResult("Track not found") { StatusCode = 404 };
            }
            float[] waveform;
            try { waveform = ComputeWaveform(track.Path, w); }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidDataException or FormatException)
            {
                // Media Foundation can't decode every format (e.g. "byte stream type unsupported"); that isn't a server error.
                args.SendEvent("Waveform unavailable for this file", 415, Program.mWebApi);
                return new ObjectResult("Waveform unavailable for this file format.") { StatusCode = 415 };
            }
            args.SendEvent("Track waveform sent", 200, Program.mWebApi);
            return new ObjectResult(waveform) { StatusCode = 200 };
        }

        internal static float[] ComputeWaveform(string path, int w)
        {
            using var reader = AudioFiles.OpenReader(path);
            // The reader yields interleaved 32-bit float samples.
            var sampleCount = reader.Length / sizeof(float);
            var samplesPerPixel = (int)Math.Max(reader.WaveFormat.Channels, sampleCount / w);
            samplesPerPixel -= samplesPerPixel % reader.WaveFormat.Channels; // keep channel frames together
            var waveform = new float[w];
            var buffer = new float[samplesPerPixel];

            // RMS per bucket instead of peak: mastered music hits ~0 dBFS peaks almost everywhere, which made
            // the peak waveform flat. RMS tracks perceived loudness, then normalize to the loudest bucket.
            int i = 0;
            while (i < w)
            {
                int read = 0, n;
                while (read < buffer.Length && (n = reader.Read(buffer, read, buffer.Length - read)) > 0) read += n;
                if (read == 0) break;
                double sum = 0;
                for (int k = 0; k < read; k++) sum += buffer[k] * buffer[k];
                waveform[i++] = (float)Math.Sqrt(sum / read);
            }
            float loudest = waveform.Max();
            if (loudest > 0)
                for (int k = 0; k < w; k++) waveform[k] = (float)Math.Round(waveform[k] / loudest, 4);
            return waveform;
        }

        /// <summary>
        /// The track's FFTVIS (.fvz, format v3) file. Every format field is optional: missing ones use the server
        /// default (GET settings) and out-of-range ones are clamped to the limits. The file's header records the format
        /// actually used. Generated on first request, then served from the cache.
        /// </summary>
        [Authorize(Roles = "Admin,User")]
        [MelonWebApi.Middleware.MediaAccess]
        [HttpGet("fvz")]
        public async Task<IActionResult> Fvz(string id, [FromQuery] VisualFormatRequest format)
        {
            var args = new WebApiEventArgs("api/visuals/fvz", CurrentUser, new Dictionary<string, object> { { "id", id } });
            if (!VisualsService.Instance.Enabled) { args.SendEvent(Disabled, 403, Program.mWebApi); return StatusCode(403, Disabled); }
            var track = FindTrack(id);
            if (track == null || !System.IO.File.Exists(track.Path)) { args.SendEvent("Track not found", 404, Program.mWebApi); return NotFound("Track not found"); }

            VisualFormat resolved;
            try { resolved = VisualsService.Instance.Resolve(format); }
            catch (ArgumentException ex) { args.SendEvent(ex.Message, 400, Program.mWebApi); return BadRequest(ex.Message); }

            string path;
            try { path = await VisualsService.Instance.GetFvzAsync(track._id, track.Path, resolved, HttpContext.RequestAborted); }
            catch (OperationCanceledException) { return new EmptyResult(); }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "FFTVIS generation failed for {Id}", id);
                args.SendEvent("Visualization could not be generated", 500, Program.mWebApi);
                return StatusCode(500, "Visualization could not be generated for this track.");
            }
            args.SendEvent("FFTVIS file sent", 200, Program.mWebApi);
            return Cached(path, "application/x-fvz", $"{track._id}.fvz");
        }

        /// <summary>Beat times with per-beat strength, sharpness and band levels, plus detected kick, snare and hat hits.</summary>
        [Authorize(Roles = "Admin,User")]
        [MelonWebApi.Middleware.MediaAccess]
        [HttpGet("beat-times")]
        public Task<IActionResult> BeatTimes(string id) => Analysis(id, beats: true);

        /// <summary>An energy curve plus labelled moments (soft, energetic, peak, build, drop, beatless, silence) and sections.</summary>
        [Authorize(Roles = "Admin,User")]
        [MelonWebApi.Middleware.MediaAccess]
        [HttpGet("moments")]
        public Task<IActionResult> Moments(string id) => Analysis(id, beats: false);

        private async Task<IActionResult> Analysis(string id, bool beats)
        {
            string api = beats ? "api/visuals/beat-times" : "api/visuals/moments";
            var args = new WebApiEventArgs(api, CurrentUser, new Dictionary<string, object> { { "id", id } });
            if (!VisualsService.Instance.Enabled) { args.SendEvent(Disabled, 403, Program.mWebApi); return StatusCode(403, Disabled); }
            var track = FindTrack(id);
            if (track == null || !System.IO.File.Exists(track.Path)) { args.SendEvent("Track not found", 404, Program.mWebApi); return NotFound("Track not found"); }

            (string Beats, string Moments) paths;
            try { paths = await VisualsService.Instance.GetAnalysisAsync(track._id, track.Path, HttpContext.RequestAborted); }
            catch (OperationCanceledException) { return new EmptyResult(); }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Visual analysis failed for {Id}", id);
                args.SendEvent("Analysis failed", 500, Program.mWebApi);
                return StatusCode(500, "This track could not be analysed.");
            }
            args.SendEvent(beats ? "Beat times sent" : "Moments sent", 200, Program.mWebApi);
            return Cached(beats ? paths.Beats : paths.Moments, "application/json", null);
        }

        /// <summary>
        /// Starts preparing data for upcoming tracks (typically the next few in the queue) in the background, in the
        /// format the client will ask for, so it's ready when playback reaches them. Returns 202 with the number queued.
        /// </summary>
        [Authorize(Roles = "Admin,User")]
        [HttpPost("prefetch")]
        public IActionResult Prefetch([FromBody] VisualPrefetchRequest request)
        {
            var args = new WebApiEventArgs("api/visuals/prefetch", CurrentUser, new Dictionary<string, object> { { "count", request.Ids?.Length ?? 0 } });
            if (!VisualsService.Instance.Enabled) { args.SendEvent(Disabled, 403, Program.mWebApi); return StatusCode(403, Disabled); }
            if (request.Ids == null || request.Ids.Length == 0) return BadRequest("Ids are required.");
            if (request.Ids.Length > VisualsService.MaxPrefetch) return BadRequest($"Prefetch at most {VisualsService.MaxPrefetch} tracks at a time.");

            var include = (request.Include is { Length: > 0 } ? request.Include : ["fvz", "beat-times", "moments"])
                .Select(i => i.Trim().ToLowerInvariant()).ToHashSet();
            string[] known = ["fvz", "beat-times", "moments"];
            if (include.Except(known).FirstOrDefault() is { } unknown) return BadRequest($"Unknown include \"{unknown}\". Use fvz, beat-times or moments.");

            VisualFormat format;
            try { format = VisualsService.Instance.Resolve(request.Format); }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }

            var ids = request.Ids.Distinct().ToArray();
            var found = Tracks().Find(Builders<Track>.Filter.In(x => x._id, ids)).ToList()
                .Where(t => System.IO.File.Exists(t.Path)).ToDictionary(t => t._id);
            var ordered = ids.Where(found.ContainsKey).Select(i => (found[i]._id, found[i].Path)).ToList();
            int queued = VisualsService.Instance.Prefetch(ordered, format, include.Contains("fvz"),
                include.Contains("beat-times") || include.Contains("moments"));
            args.SendEvent("Visualization prefetch queued", 202, Program.mWebApi);
            return Accepted(new { queued, notFound = ids.Where(i => !found.ContainsKey(i)).ToArray() });
        }

        /// <summary>Serves a cache file with an ETag from its name (which changes with the source file and format) so clients can revalidate cheaply.</summary>
        private IActionResult Cached(string path, string contentType, string? downloadName)
        {
            var etag = new EntityTagHeaderValue($"\"{Path.GetFileNameWithoutExtension(path).Replace(".", "-")}\"");
            Response.Headers.CacheControl = "private, max-age=86400";
            var result = PhysicalFile(path, contentType, System.IO.File.GetLastWriteTimeUtc(path), etag, enableRangeProcessing: false);
            if (downloadName != null) result.FileDownloadName = downloadName;
            return result;
        }
    }
}
