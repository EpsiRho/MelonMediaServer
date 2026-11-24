using Melon.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MongoDB.Bson;
using MongoDB.Driver;
using SharpCompress.Common;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing;
using Melon.LocalClasses;
using Microsoft.AspNetCore.Authorization;
using ATL.Playlist;
using NAudio.Wave;
using System.Diagnostics;
using Microsoft.AspNetCore.Components.Forms;
using NAudio.Lame;
using Concentus.Structs;
using Concentus.Enums;
using Concentus.Oggfile;
using NAudio.Wave.SampleProviders;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Http.HttpResults;
using System;
using MongoDB.Driver.Core.Events;
using System.Security.Claims;
using MongoDB.Bson.Serialization;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/download")]
    [MelonWebApi.Middleware.MediaAccess]
    public class DownloadController : ControllerBase
    {
        private readonly ILogger<DownloadController> _logger;

        public DownloadController(ILogger<DownloadController> logger)
        {
            _logger = logger;
        }
        private string GetMimeType(string extension)
        {
            switch (extension.ToLower())
            {
                case "mp3":
                    return "audio/mpeg";
                case "m4a":
                case "mp4":
                    return "audio/mp4";
                case "wav":
                    return "audio/wav";
                case "wma":
                    return "audio/x-ms-wma";
                case "flac":
                    return "audio/flac";
                case "ogg":
                case "oga":
                    return "audio/ogg";
                case "opus":
                    return "audio/opus";
                case "aac":
                    return "audio/aac";
                case "aif":
                case "aiff":
                    return "audio/aiff";
                default:
                    return "application/octet-stream"; // Fallback MIME type
            }
        }
        /// <summary>
        /// The track file as stored. With <paramref name="eq"/> (an EQ filter id the user owns or that is public), the
        /// server decodes the track, applies the filter and sends it as a WAV that starts at once and supports Range.
        /// </summary>
        [Authorize(Roles = "Admin,User")]
        [HttpGet("track")]
        public async Task<IActionResult> DownloadTrack(string id, string eq = null)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/download/track", curId, new Dictionary<string, object>()
            {
                { "id", id },
                { "eq", eq }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var tFilter = Builders<Track>.Filter.Eq(x=>x._id, id);
            var track = TCollection.Find(tFilter).FirstOrDefault();
            if (track == null)
            {
                args.SendEvent("Track not found", 404, Program.mWebApi);
                return NotFound();
            }

            if (!string.IsNullOrEmpty(eq))
            {
                var filter = EqFilters.FindUsable(eq, curId);
                if (filter == null)
                {
                    args.SendEvent("EQ filter not found", 404, Program.mWebApi);
                    return NotFound("EQ filter not found");
                }
                Services.EqWaveStream wave;
                try { wave = new Services.EqWaveStream(track.Path, EqFilters.ToSettings(filter), int.TryParse(track.BitsPerSample, out var bits) ? bits : 16); }
                catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidDataException or FormatException)
                {
                    args.SendEvent("EQ unavailable for this file", 415, Program.mWebApi);
                    return StatusCode(415, "EQ unavailable for this file format.");
                }
                args.SendEvent("Sent equalized track", 200, Program.mWebApi);
                return new FileStreamResult(wave, "audio/wav")
                {
                    EnableRangeProcessing = true,
                    FileDownloadName = Path.GetFileNameWithoutExtension(track.Path) + ".wav"
                };
            }

            // The file as stored, except a FLAC whose huge embedded cover pushes the audio past what Windows players
            // can open: that one is sent without its picture/padding blocks (the audio bytes are the same).
            Stream fileStream = AudioFiles.OpenRead(track.Path);

            string filename = Path.GetFileName(track.Path);
            args.SendEvent("Sent track file", 200, Program.mWebApi);
            return new FileStreamResult(fileStream, $"{GetMimeType(track.Format)}")
            {
                EnableRangeProcessing = true,
                FileDownloadName = filename
            };
        }
        /// <summary>The track as MP3 at <paramref name="transcodeBitrate"/> kbps, with the EQ filter <paramref name="eq"/> applied when given.</summary>
        [Authorize(Roles = "Admin,User")]
        [HttpGet("track-transcode")]
        public async Task<IActionResult> DownloadTrackTranscode(string id, int transcodeBitrate = 256, string eq = null)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/download/track-transcode", curId, new Dictionary<string, object>()
            {
                { "id", id },
                { "eq", eq }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var tFilter = Builders<Track>.Filter.Eq(x => x._id, id);
            var track = TCollection.Find(tFilter).FirstOrDefault();
            if (track == null)
            {
                args.SendEvent("Track not found", 404, Program.mWebApi);
                return NotFound();
            }

            Nova.Dsp.EqualizerSettings eqSettings = null;
            if (!string.IsNullOrEmpty(eq))
            {
                var filter = EqFilters.FindUsable(eq, curId);
                if (filter == null)
                {
                    args.SendEvent("EQ filter not found", 404, Program.mWebApi);
                    return NotFound("EQ filter not found");
                }
                eqSettings = EqFilters.ToSettings(filter);
            }

            FileStream fileStream = new FileStream(track.Path, FileMode.Open, FileAccess.Read);

            if (fileStream == null)
            {
                args.SendEvent("Track file not found", 404, Program.mWebApi);
                return NotFound();
            }

            string filename = Path.GetFileName(track.Path);
            var split = filename.Split(".");
            filename = filename.Replace(split[split.Length - 1], "mp3");

            fileStream.Dispose();
            if (transcodeBitrate < 32 || transcodeBitrate > 320) transcodeBitrate = 256;

            // Encode once into a cache file and serve that with range support. Media players (Windows Media Foundation,
            // browsers) issue several Range requests per track; previously each one re-encoded the whole file in
            // memory before sending a byte, which could stall playback start for many seconds under load.
            string cached;
            try { cached = await TranscodeCache.GetAsync(track._id, track.Path, transcodeBitrate, eqSettings, HttpContext.RequestAborted); }
            catch (OperationCanceledException) { return new EmptyResult(); }
            catch (Exception)
            {
                args.SendEvent("Transcode failed", 500, Program.mWebApi);
                return StatusCode(500, "Transcode failed");
            }

            args.SendEvent("Track file sent", 200, Program.mWebApi);
            return new FileStreamResult(new FileStream(cached, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16, true), "audio/mpeg")
            {
                EnableRangeProcessing = true,
                FileDownloadName = filename
            };
        }
        /// <summary>Deprecated: use api/visuals/waveform. Kept so existing clients keep working.</summary>
        [Authorize(Roles = "Admin,User")]
        [HttpGet("track-wave")]
        public ObjectResult DownloadTrackWaveform(string id, float width)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/download/track", curId, new Dictionary<string, object>()
            {
                { "id", id },
                { "width", width }
            });

            var w = (int)width;
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");

            if(width == 0)
            {
                args.SendEvent("Width parameter is required", 400, Program.mWebApi);
                return new ObjectResult("Width parameter is required") { StatusCode = 400 };
            }

            var tFilter = Builders<Track>.Filter.Eq(x=>x._id, id);
            var track = TCollection.Find(tFilter).FirstOrDefault();
            if (track == null)
            {
                args.SendEvent("Track not found", 404, Program.mWebApi);
                return new ObjectResult("Track not found") { StatusCode = 404 };
            }

            float[] waveform;
            try { waveform = VisualsController.ComputeWaveform(track.Path, w); }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidDataException or FormatException)
            {
                // Media Foundation can't decode every format; that isn't a server error.
                args.SendEvent("Waveform unavailable for this file", 415, Program.mWebApi);
                return new ObjectResult("Waveform unavailable for this file format.") { StatusCode = 415 };
            }
            args.SendEvent("Track waveform sent", 200, Program.mWebApi);
            return new ObjectResult(waveform) { StatusCode = 200 };
        }
                /// <summary>
        /// Sends artwork as is, or with <paramref name="size"/> &gt; 0 fitted within size×size pixels (cached; see
        /// <see cref="Services.ArtResizer"/>) so clients showing small cards don't download multi-megabyte originals.
        /// </summary>
        private async Task<IActionResult> Art(byte[] bytes, string mime, int size)
        {
            var (data, type) = await Services.ArtResizer.Fit(bytes, mime, size);
            return File(data, type);
        }
        [EnableRateLimiting(Program.AnonymousArtRateLimit)]
        [HttpGet("track-art")]
        public async Task<IActionResult> DownloadTrackArt(string id, int index = -1, int size = 0)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/download/track-art", curId, new Dictionary<string, object>()
            {
                { "id", id },
                { "index", index }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var tFilter = Builders<Track>.Filter.Eq(x => x._id, id);
            var track = TCollection.Find(tFilter).FirstOrDefault();


            ATL.Track file = null;
            try
            {
                file = new ATL.Track(track.Path);
                index = index == -1 ? track.TrackArtDefault : index;
                var pic = file.EmbeddedPictures[index];
                args.SendEvent("Track artwork sent", 200, Program.mWebApi);
                return await Art(pic.PictureData, pic.MimeType, size);
            }
            catch(Exception)
            {
                args.SendEvent("Default artwork sent", 200, Program.mWebApi);
                return await Art(StateManager.GetDefaultImage(), "image/jpeg", size);
            }
            
        }
        [EnableRateLimiting(Program.AnonymousArtRateLimit)]
        [HttpGet("album-art")]
        public async Task<IActionResult> DownloadAlbumArt(string id, int index = -1, int size = 0)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/download/album-art", curId, new Dictionary<string, object>()
            {
                { "id", id },
                { "index", index }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var ACollection = mongoDatabase.GetCollection<Album>("Albums");

            var aFilter = Builders<Album>.Filter.Eq(x => x._id, id);
            var album = ACollection.Find(aFilter).FirstOrDefault();

            if(album == null)
            {
                args.SendEvent("Default artwork sent", 000, Program.mWebApi);
                return await Art(StateManager.GetDefaultImage(), "image/jpeg", size);
            }

            // If Album has uploaded art, use index to get it.
            if(album.AlbumArtCount >= 1)
            {
                try
                {
                    index = index == -1 ? album.AlbumArtDefault : index;
                    FileStream file = new FileStream(Services.SafePath.Combine($"{StateManager.melonPath}/AlbumArts", album.AlbumArtPaths[index]), FileMode.Open, FileAccess.Read);
                    byte[] bytes = new byte[file.Length];
                    file.Read(bytes, 0, (int)file.Length);
                    args.SendEvent("Album artwork sent", 200, Program.mWebApi);
                    file.Dispose();
                    var mime = album.AlbumArtPaths[index].Split(".")[1];
                    return await Art(bytes, $"image/{mime}", size);
                }
                catch (Exception)
                {
                    Serilog.Log.Error($"[MWAPI] ({DateTime.Now}) Album artwork should have override on <{album._id}> with index <{index}> and album artwork count <{album.AlbumArtCount}>");
                }
            }

            // Otherwise, get album art by track, using index on that track
            var filter = Builders<Track>.Filter.Eq(x => x.Album._id, album._id);
            var trackDocs = TCollection.Find(filter).SortBy(track => track.Disc).ThenBy(track => track.Position).ToList();

            foreach (var track in trackDocs)
            {
                var tFilter = Builders<Track>.Filter.Eq(x => x._id, track._id);
                var found = TCollection.Find(tFilter).FirstOrDefault();


                ATL.Track file = null;
                try
                {
                    file = new ATL.Track(found.Path);
                    if(file.EmbeddedPictures.Count == 0)
                    {
                        continue;
                    }
                    index = index == -1 ? found.TrackArtDefault : index;
                    var pic = file.EmbeddedPictures[index];
                    args.SendEvent("Album artwork sent", 200, Program.mWebApi);
                    return await Art(pic.PictureData, pic.MimeType, size);
                }
                catch (Exception)
                {

                }
            }

            args.SendEvent("Default artwork sent", 200, Program.mWebApi);
            return await Art(StateManager.GetDefaultImage(), "image/jpeg", size);
        }
        [EnableRateLimiting(Program.AnonymousArtRateLimit)]
        [HttpGet("artist-pfp")]
        public async Task<IActionResult> DownloadArtistPfp(string id, int index = -1, int size = 0)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/download/artist-pfp", curId, new Dictionary<string, object>()
            {
                { "id", id },
                { "index", index }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ACollection = mongoDatabase.GetCollection<Artist>("Artists");

            try
            {
                var aFilter = Builders<Artist>.Filter.Eq(x => x._id, id);
                var artist = ACollection.Find(aFilter).FirstOrDefault();

                index = index == -1 ? artist.ArtistPfpDefault : index;
                FileStream file = new FileStream(Services.SafePath.Combine($"{StateManager.melonPath}/ArtistPfps", artist.ArtistPfpPaths[index]), FileMode.Open, FileAccess.Read);
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                args.SendEvent("Artist pfp sent", 200, Program.mWebApi);
                file.Dispose();
                var mime = artist.ArtistPfpPaths[index].Split(".")[1];
                return await Art(bytes, $"image/{mime}", size);
            }
            catch (Exception)
            {
                args.SendEvent("Default artwork sent", 200, Program.mWebApi);
                return await Art(StateManager.GetDefaultImage(), "image/jpeg", size);
            }
        }
        [EnableRateLimiting(Program.AnonymousArtRateLimit)]
        [HttpGet("artist-banner")]
        public async Task<IActionResult> DownloadArtistBanner(string id, int index = -1, int size = 0)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/download/artist-banner", curId, new Dictionary<string, object>()
            {
                { "id", id },
                { "index", index }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ACollection = mongoDatabase.GetCollection<Artist>("Artists");

            try
            {
                var aFilter = Builders<Artist>.Filter.Eq(x => x._id, id);
                var artist = ACollection.Find(aFilter).FirstOrDefault();

                index = index == -1 ? artist.ArtistBannerArtDefault : index;
                FileStream file = new FileStream(Services.SafePath.Combine($"{StateManager.melonPath}/ArtistBanners", artist.ArtistBannerPaths[index]), FileMode.Open, FileAccess.Read);
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                args.SendEvent("Artist banner sent", 200, Program.mWebApi);
                file.Dispose();
                var mime = artist.ArtistBannerPaths[index].Split(".")[1];
                return await Art(bytes, $"image/{mime}", size);
            }
            catch (Exception)
            {
                args.SendEvent("Default artwork sent", 200, Program.mWebApi);
                return await Art(StateManager.GetDefaultImage(), "image/jpeg", size);
            }
        }
        /// <summary>
        /// A user's profile picture, fitted within size×size when <paramref name="size"/> is set.
        /// 404 when they haven't set one, so clients can draw their own placeholder (initials).
        /// </summary>
        [Authorize(Roles = "Admin,User,Pass")]
        [EnableRateLimiting(Program.AnonymousArtRateLimit)]
        [HttpGet("user-pfp")]
        public async Task<IActionResult> DownloadUserPfp(string id, int size = 0)
        {
            var curId = User.FindFirstValue(ClaimTypes.UserData);
            id = string.IsNullOrEmpty(id) ? curId : id;
            var args = new WebApiEventArgs("api/download/user-pfp", curId, new Dictionary<string, object>() { { "id", id } });
            var users = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString).GetDatabase("Melon").GetCollection<User>("Users");
            var user = users.Find(Builders<User>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if (user == null || string.IsNullOrEmpty(user.ProfilePicPath))
            {
                args.SendEvent("No profile picture", 404, Program.mWebApi);
                return NotFound();
            }
            try
            {
                var bytes = await System.IO.File.ReadAllBytesAsync(Services.SafePath.Combine($"{StateManager.melonPath}/UserPfps", user.ProfilePicPath));
                args.SendEvent("Profile picture sent", 200, Program.mWebApi);
                return await Art(bytes, $"image/{Path.GetExtension(user.ProfilePicPath).TrimStart('.')}", size);
            }
            catch (Exception)
            {
                args.SendEvent("No profile picture", 404, Program.mWebApi);
                return NotFound();
            }
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [EnableRateLimiting(Program.AnonymousArtRateLimit)]
        [HttpGet("playlist-art")]
        public async Task<IActionResult> DownloadPlaylistArt(string id, int size = 0)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/download/playlist-art", curId, new Dictionary<string, object>()
            {
                { "id", id }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            try
            {
                var pFilter = Builders<Playlist>.Filter.Eq(x => x._id, id);
                var playlist = PCollection.Find(pFilter).FirstOrDefault();
                if (playlist != null && !playlist.PublicViewing && !playlist.PublicEditing && playlist.Owner != curId && playlist.Editors?.Contains(curId) != true && playlist.Viewers?.Contains(curId) != true)
                    return NotFound();

                FileStream file = new FileStream(Services.SafePath.Combine($"{StateManager.melonPath}/PlaylistArts", playlist.ArtworkPath), FileMode.Open, FileAccess.Read);
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                args.SendEvent("Playlist artwork sent", 200, Program.mWebApi);
                file.Dispose();
                var mime = playlist.ArtworkPath.Split(".")[1];
                return await Art(bytes, $"image/{mime}", size);
            }
            catch (Exception)
            {
                args.SendEvent("Default artwork sent", 200, Program.mWebApi);
                return await Art(StateManager.GetDefaultImage(), "image/jpeg", size);
            }
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [EnableRateLimiting(Program.AnonymousArtRateLimit)]
        [HttpGet("collection-art")]
        public async Task<IActionResult> DownloadCollectionArt(string id, int size = 0)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/download/collection-art", curId, new Dictionary<string, object>()
            {
                { "id", id }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var CCollection = mongoDatabase.GetCollection<Collection>("Collections");

            try
            {
                var cFilter = Builders<Collection>.Filter.Eq(x => x._id, id);
                var collection = CCollection.Find(cFilter).FirstOrDefault();
                if (collection != null && !collection.PublicViewing && !collection.PublicEditing && collection.Owner != curId && collection.Editors?.Contains(curId) != true && collection.Viewers?.Contains(curId) != true)
                    return NotFound();

                FileStream file = new FileStream(Services.SafePath.Combine($"{StateManager.melonPath}/CollectionArts", collection.ArtworkPath), FileMode.Open, FileAccess.Read);
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                args.SendEvent("Collection artwork sent", 200, Program.mWebApi);
                file.Dispose();
                var mime = collection.ArtworkPath.Split(".")[1];
                return await Art(bytes, $"image/{mime}", size);
            }
            catch (Exception)
            {
                args.SendEvent("Default artwork sent", 200, Program.mWebApi);
                return await Art(StateManager.GetDefaultImage(), "image/jpeg", size);
            }
        }
    }
    /// <summary>Per-track MP3 transcode cache (one encode per id+bitrate+EQ, shared by concurrent requests, LRU-trimmed).</summary>
    internal static class TranscodeCache
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<Task<string>>> _inflight = new();
        private static readonly string Dir = Path.Combine(Path.GetTempPath(), "MelonTranscodes");
        private const long MaxBytes = 2L * 1024 * 1024 * 1024;

        public static async Task<string> GetAsync(string id, string sourcePath, int kbps, Nova.Dsp.EqualizerSettings eq, CancellationToken ct)
        {
            Directory.CreateDirectory(Dir);
            var stamp = File.GetLastWriteTimeUtc(sourcePath).Ticks;
            // The EQ part is a hash of the filter's sound settings, so editing a filter gets a fresh encode.
            var eqKey = eq == null ? "" : "_eq" + EqFilters.Fingerprint(eq);
            var dest = Path.Combine(Dir, $"{id}_{kbps}_{stamp}{eqKey}.mp3");
            if (File.Exists(dest)) { try { File.SetLastAccessTimeUtc(dest, DateTime.UtcNow); } catch { } return dest; }
            var lazy = _inflight.GetOrAdd(dest, d => new Lazy<Task<string>>(() => Task.Run(() => Encode(sourcePath, d, kbps, eq))));
            try { return await lazy.Value.WaitAsync(ct); }
            finally { if (lazy.Value.IsCompleted) _inflight.TryRemove(dest, out _); }
        }

        private static string Encode(string sourcePath, string dest, int kbps, Nova.Dsp.EqualizerSettings eq)
        {
            var part = dest + "." + Guid.NewGuid().ToString("N") + ".part";
            try
            {
                using (var reader = AudioFiles.OpenReader(sourcePath))
                {
                    // LAME refuses input above 48 kHz outright (not just output), so hi-res sources are resampled first.
                    int rate = Mp3Rate(reader.WaveFormat.SampleRate);
                    // EQ runs at the source rate, before any resampling.
                    ISampleProvider source = eq == null ? reader : new Services.EqSampleProvider(reader, eq);
                    IWaveProvider input = rate == reader.WaveFormat.SampleRate
                        ? (eq == null ? reader : source.ToWaveProvider())
                        : new NAudio.Wave.SampleProviders.WdlResamplingSampleProvider(source, rate).ToWaveProvider();
                    using var fs = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16);
                    using var writer = new LameMP3FileWriter(fs, input.WaveFormat, new LameConfig { BitRate = kbps, OutputSampleRate = rate });
                    var buffer = new byte[input.WaveFormat.AverageBytesPerSecond];
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0) writer.Write(buffer, 0, read);
                    writer.Flush();
                }
                File.Move(part, dest, true);
            }
            catch
            {
                try { File.Delete(part); } catch { }
                throw;
            }
            _ = Task.Run(Trim);
            return dest;
        }

        /// <summary>
        /// MP3 tops out at 48 kHz, and LAME rejects hi-res input (88.2 / 96 / 192 kHz FLAC), which made every such
        /// transcode fail, so those tracks never played at an MP3 quality setting. Resample to the nearest rate MP3 allows, keeping the 44.1 kHz family together (88.2 / 176.4 → 44.1).
        /// </summary>
        private static int Mp3Rate(int rate)
        {
            int[] valid = { 8000, 11025, 12000, 16000, 22050, 24000, 32000, 44100, 48000 };
            if (valid.Contains(rate)) return rate;
            if (rate > 48000) return rate % 11025 == 0 ? 44100 : 48000;
            return valid.Where(v => v <= rate).DefaultIfEmpty(8000).Max();
        }

        private static void Trim()
        {
            try
            {
                var files = new DirectoryInfo(Dir).EnumerateFiles("*.mp3").OrderBy(f => f.LastAccessTimeUtc).ToList();
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
}
