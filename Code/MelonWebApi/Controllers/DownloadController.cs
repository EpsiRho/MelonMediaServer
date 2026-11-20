using Melon.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
                default:
                    return "application/octet-stream"; // Fallback MIME type
            }
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("track")]
        public async Task<IActionResult> DownloadTrack(string id)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/download/track", curId, new Dictionary<string, object>()
            {
                { "id", id }
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

            FileStream fileStream = new FileStream(track.Path, FileMode.Open, FileAccess.Read);

            if (fileStream == null)
            {
                args.SendEvent("Track file not found", 404, Program.mWebApi);
                return NotFound();
            }

            string filename = Path.GetFileName(track.Path);
            args.SendEvent("Sent track file", 200, Program.mWebApi);
            return new FileStreamResult(fileStream, $"{GetMimeType(track.Format)}")
            {
                EnableRangeProcessing = true,
                FileDownloadName = filename
            };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("track-transcode")]
        public async Task<IActionResult> DownloadTrackTranscode(string id, int transcodeBitrate = 256)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/download/track-transcode", curId, new Dictionary<string, object>()
            {
                { "id", id }
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
            try { cached = await TranscodeCache.GetAsync(track._id, track.Path, transcodeBitrate, HttpContext.RequestAborted); }
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

            using (var reader = new AudioFileReader(track.Path))
            {
                // AudioFileReader yields interleaved 32-bit float samples.
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

                args.SendEvent("Track waveform sent", 200, Program.mWebApi);
                return new ObjectResult(waveform) { StatusCode = 200 };
            }
        }
        [HttpGet("track-art")]
        public async Task<IActionResult> DownloadTrackArt(string id, int index = -1)
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
                MemoryStream ms = new MemoryStream(pic.PictureData);
                ms.Seek(0, SeekOrigin.Begin);
                args.SendEvent("Track artwork sent", 200, Program.mWebApi);
                return File(ms, $"{pic.MimeType}");
            }
            catch(Exception)
            {
                args.SendEvent("Default artwork sent", 200, Program.mWebApi);
                return File(StateManager.GetDefaultImage(), "image/jpeg");
            }
            
        }
        [HttpGet("album-art")]
        public async Task<IActionResult> DownloadAlbumArt(string id, int index = -1)
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
                return File(StateManager.GetDefaultImage(), "image/jpeg");
            }

            // If Album has uploaded art, use index to get it.
            if(album.AlbumArtCount >= 1)
            {
                try
                {
                    index = index == -1 ? album.AlbumArtDefault : index;
                    FileStream file = new FileStream($"{StateManager.melonPath}/AlbumArts/{album.AlbumArtPaths[index]}", FileMode.Open, FileAccess.Read);
                    byte[] bytes = new byte[file.Length];
                    file.Read(bytes, 0, (int)file.Length);
                    args.SendEvent("Album artwork sent", 200, Program.mWebApi);
                    file.Dispose();
                    var mime = album.AlbumArtPaths[index].Split(".")[1];
                    return File(bytes, $"image/{mime}");
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
                    MemoryStream ms = new MemoryStream(pic.PictureData);
                    ms.Seek(0, SeekOrigin.Begin);
                    args.SendEvent("Album artwork sent", 200, Program.mWebApi);
                    return File(ms, $"{pic.MimeType}");
                }
                catch (Exception)
                {

                }
            }

            args.SendEvent("Default artwork sent", 200, Program.mWebApi);
            return File(StateManager.GetDefaultImage(), "image/jpeg");
        }
        [HttpGet("artist-pfp")]
        public async Task<IActionResult> DownloadArtistPfp(string id, int index = -1)
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
                FileStream file = new FileStream($"{StateManager.melonPath}/ArtistPfps/{artist.ArtistPfpPaths[index]}", FileMode.Open, FileAccess.Read);
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                args.SendEvent("Artist pfp sent", 200, Program.mWebApi);
                file.Dispose();
                var mime = artist.ArtistPfpPaths[index].Split(".")[1];
                return File(bytes, $"image/{mime}");
            }
            catch (Exception)
            {
                args.SendEvent("Default artwork sent", 200, Program.mWebApi);
                return File(StateManager.GetDefaultImage(), "image/jpeg");
            }
        }
        [HttpGet("artist-banner")]
        public async Task<IActionResult> DownloadArtistBanner(string id, int index = -1)
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
                FileStream file = new FileStream($"{StateManager.melonPath}/ArtistBanners/{artist.ArtistBannerPaths[index]}", FileMode.Open, FileAccess.Read);
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                args.SendEvent("Artist banner sent", 200, Program.mWebApi);
                file.Dispose();
                var mime = artist.ArtistBannerPaths[index].Split(".")[1];
                return File(bytes, $"image/{mime}");
            }
            catch (Exception)
            {
                args.SendEvent("Default artwork sent", 200, Program.mWebApi);
                return File(StateManager.GetDefaultImage(), "image/jpeg");
            }
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("playlist-art")]
        public async Task<IActionResult> DownloadPlaylistArt(string id)
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

                FileStream file = new FileStream($"{StateManager.melonPath}/PlaylistArts/{playlist.ArtworkPath}", FileMode.Open, FileAccess.Read);
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                args.SendEvent("Playlist artwork sent", 200, Program.mWebApi);
                file.Dispose();
                var mime = playlist.ArtworkPath.Split(".")[1];
                return File(bytes, $"image/{mime}");
            }
            catch (Exception)
            {
                args.SendEvent("Default artwork sent", 200, Program.mWebApi);
                return File(StateManager.GetDefaultImage(), "image/jpeg");
            }
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("collection-art")]
        public async Task<IActionResult> DownloadCollectionArt(string id)
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

                FileStream file = new FileStream($"{StateManager.melonPath}/CollectionArts/{collection.ArtworkPath}", FileMode.Open, FileAccess.Read);
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                args.SendEvent("Collection artwork sent", 200, Program.mWebApi);
                file.Dispose();
                var mime = collection.ArtworkPath.Split(".")[1];
                return File(bytes, $"image/{mime}");
            }
            catch (Exception)
            {
                args.SendEvent("Default artwork sent", 200, Program.mWebApi);
                return File(StateManager.GetDefaultImage(), "image/jpeg");
            }
        }
    }
    /// <summary>Per-track MP3 transcode cache (one encode per id+bitrate, shared by concurrent requests, LRU-trimmed).</summary>
    internal static class TranscodeCache
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<Task<string>>> _inflight = new();
        private static readonly string Dir = Path.Combine(Path.GetTempPath(), "MelonTranscodes");
        private const long MaxBytes = 2L * 1024 * 1024 * 1024;

        public static async Task<string> GetAsync(string id, string sourcePath, int kbps, CancellationToken ct)
        {
            Directory.CreateDirectory(Dir);
            var stamp = File.GetLastWriteTimeUtc(sourcePath).Ticks;
            var dest = Path.Combine(Dir, $"{id}_{kbps}_{stamp}.mp3");
            if (File.Exists(dest)) { try { File.SetLastAccessTimeUtc(dest, DateTime.UtcNow); } catch { } return dest; }
            var lazy = _inflight.GetOrAdd(dest, d => new Lazy<Task<string>>(() => Task.Run(() => Encode(sourcePath, d, kbps))));
            try { return await lazy.Value.WaitAsync(ct); }
            finally { if (lazy.Value.IsCompleted) _inflight.TryRemove(dest, out _); }
        }

        private static string Encode(string sourcePath, string dest, int kbps)
        {
            var part = dest + "." + Guid.NewGuid().ToString("N") + ".part";
            try
            {
                using (var reader = new AudioFileReader(sourcePath))
                using (var fs = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
                using (var writer = new LameMP3FileWriter(fs, reader.WaveFormat, new LameConfig { BitRate = kbps, OutputSampleRate = reader.WaveFormat.SampleRate }))
                {
                    reader.CopyTo(writer);
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
