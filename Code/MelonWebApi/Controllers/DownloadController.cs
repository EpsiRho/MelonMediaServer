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
using Humanizer.Bytes;
using NAudio.Lame;
using Concentus.Structs;
using Concentus.Enums;
using Concentus.Oggfile;
using NAudio.Wave.SampleProviders;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Http.HttpResults;
using Azure;
using System;
using MongoDB.Driver.Core.Events;

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
        [Authorize(Roles = "Admin,User")]
        [HttpGet("track")]
        public async Task<IActionResult> DownloadTrack(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var tFilter = Builders<Track>.Filter.Eq(x=>x._id, id);
            var track = TCollection.Find(tFilter).FirstOrDefault();
            if (track == null)
            {
                return NotFound();
            }

            FileStream fileStream = new FileStream(track.Path, FileMode.Open, FileAccess.Read);

            if (fileStream == null)
            {
                return NotFound();
            }

            string filename = Path.GetFileName(track.Path);
            return new FileStreamResult(fileStream, $"audio/{track.Format.Replace(".", "")}")
            {
                EnableRangeProcessing = true,
                FileDownloadName = filename
            };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("track-transcode")]
        public async Task<IActionResult> DownloadTrackTranscode(string id, int transcodeBitrate = 256)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var tFilter = Builders<Track>.Filter.Eq(x => x._id, id);
            var track = TCollection.Find(tFilter).FirstOrDefault();
            if (track == null)
            {
                return NotFound();
            }

            FileStream fileStream = new FileStream(track.Path, FileMode.Open, FileAccess.Read);

            if (fileStream == null)
            {
                return NotFound();
            }

            string filename = Path.GetFileName(track.Path);
            var split = filename.Split(".");
            filename = filename.Replace(split[split.Length - 1], "mp3");

            using (var reader = new AudioFileReader(track.Path))
            {
                // Wave format to convert to
                WaveFormat targetFormat = new Mp3WaveFormat(reader.WaveFormat.SampleRate, reader.WaveFormat.Channels, 0, transcodeBitrate);
                LameConfig config = new LameConfig()
                {
                    BitRate = transcodeBitrate
                };


                // If the request has a range header, send the file through FileStreamResult to manage it
                var rangeHeader = Request.Headers[HeaderNames.Range];
                if (!string.IsNullOrEmpty(rangeHeader))
                {
                    MemoryStream ms = new MemoryStream();
                    using (var writer = new LameMP3FileWriter(ms, reader.WaveFormat, config))
                    {
                        reader.CopyTo(writer);
                        writer.Flush();
                    }
                    ms.Position = 0;
                    return new FileStreamResult(ms, "audio/mpeg")
                    {
                        EnableRangeProcessing = true,
                        FileDownloadName = filename
                    };
                }

                // Otherwise, send file as it's transcoded (faster, but no ability for seek)
                using (var writer = new LameMP3FileWriter(HttpContext.Response.Body, reader.WaveFormat, config))
                {
                    byte[] buffer = new byte[4096];
                    HttpContext.Response.Headers["Accept-Ranges"] = "bytes";
                    HttpContext.Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{filename}\"");
                    HttpContext.Response.StatusCode = 200;
                    HttpContext.Response.ContentType = "audio/mpeg";
                    int bytesRead;
                    while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        writer.Write(buffer, 0, buffer.Length);
                    }
                    writer.Flush();
                }
                return ControllerBase.Empty;
            }
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("track-wave")]
        public ObjectResult DownloadTrackWaveform(string id, float width)
        {
            var w = (int)width;
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");

            if(width == 0)
            {
                return new ObjectResult("Width parameter is required") { StatusCode = 400 };
            }

            var tFilter = Builders<Track>.Filter.Eq(x=>x._id, id);
            var track = TCollection.Find(tFilter).FirstOrDefault();
            if (track == null)
            {
                return new ObjectResult("Track not found") { StatusCode = 404 };
            }

            using (var reader = new AudioFileReader(track.Path))
            {
                // Number of samples to process
                var sampleCount = (int)(reader.Length / (reader.WaveFormat.BitsPerSample / 8));
                // The number of samples per point in the final waveform
                var samplesPerPixel = sampleCount / w;
                var waveform = new float[w];
                var buffer = new float[samplesPerPixel];
                int waveformIndex = 0;

                int samplesRead;
                int count = track.Path.Contains(".wav") ? (reader.WaveFormat.BlockAlign / reader.WaveFormat.Channels) : buffer.Length;
                while ((samplesRead = reader.Read(buffer, 0, count)) > 0)
                {
                    float max = buffer.Take(samplesRead).Select(v => Math.Abs(v)).Max();
                    waveform[waveformIndex++] = max;
                    if (waveformIndex >= w) break;
                    if(max == 0)
                    {
                        Debug.WriteLine("penis");
                    }
                }

                return new ObjectResult(waveform) { StatusCode = 200 };
            }
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("track-art")]
        public async Task<IActionResult> DownloadTrackArt(string id, int index = -1)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");


            var tFilter = Builders<Track>.Filter.Eq(x => x._id, id);
            var track = TCollection.Find(tFilter).ToList()[0];


            ATL.Track file = null;
            try
            {
                file = new ATL.Track(track.Path);
            }
            catch (Exception)
            {
                return NotFound();
            }

            try 
            { 
                index = index == -1 ? track.TrackArtDefault : index;
                var pic = file.EmbeddedPictures[index];
                MemoryStream ms = new MemoryStream(pic.PictureData);
                ms.Seek(0, SeekOrigin.Begin);

                return File(ms, $"image/jpeg");
                

            }
            catch(Exception)
            {
                return File(StateManager.GetDefaultImage(), "image/jpeg");
            }
            
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("album-art")]
        public async Task<IActionResult> DownloadAlbumArt(string id, int index = -1)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var ACollection = mongoDatabase.GetCollection<Album>("Albums");


            try
            {
                var aFilter = Builders<Album>.Filter.Eq(x => x._id, id);
                var album = ACollection.Find(aFilter).ToList()[0];

                index = index == -1 ? album.AlbumArtDefault : index;
                FileStream file = new FileStream($"{StateManager.melonPath}/AlbumArts/{album.AlbumArtPaths[index]}", FileMode.Open, FileAccess.Read);
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                //ms.Write(bytes, 0, (int)file.Length);
                return File(bytes, "image/jpeg");
            }
            catch (Exception)
            {
                return File(StateManager.GetDefaultImage(), "image/jpeg");
            }
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist-pfp")]
        public async Task<IActionResult> DownloadArtistPfp(string id, int index = -1)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var ACollection = mongoDatabase.GetCollection<Artist>("Artists");


            try
            {
                var aFilter = Builders<Artist>.Filter.Eq(x => x._id, id);
                var artist = ACollection.Find(aFilter).ToList()[0];

                index = index == -1 ? artist.ArtistPfpDefault : index;
                FileStream file = new FileStream($"{StateManager.melonPath}/ArtistPfps/{artist.ArtistPfpPaths[index]}", FileMode.Open, FileAccess.Read);
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                //ms.Write(bytes, 0, (int)file.Length);
                return File(bytes, "image/jpeg");
            }
            catch (Exception)
            {
                return File(StateManager.GetDefaultImage(), "image/jpeg");
            }
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist-banner")]
        public async Task<IActionResult> DownloadArtistBanner(string id, int index = -1)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var ACollection = mongoDatabase.GetCollection<Artist>("Artists");


            try
            {
                var aFilter = Builders<Artist>.Filter.Eq(x => x._id, id);
                var artist = ACollection.Find(aFilter).ToList()[0];

                index = index == -1 ? artist.ArtistBannerArtDefault : index;
                FileStream file = new FileStream($"{StateManager.melonPath}/ArtistBanners/{artist.ArtistBannerPaths[index]}", FileMode.Open, FileAccess.Read);
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                //ms.Write(bytes, 0, (int)file.Length);
                return File(bytes, "image/jpeg");
            }
            catch (Exception)
            {
                return File(StateManager.GetDefaultImage(), "image/jpeg");
            }
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("playlist-art")]
        public async Task<IActionResult> DownloadPlaylistArt(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");


            try
            {
                var pFilter = Builders<Playlist>.Filter.Eq(x => x._id, id);
                var playlist = PCollection.Find(pFilter).ToList()[0];

                // Load image data in MemoryStream
                //MemoryStream ms = new MemoryStream();
                FileStream file = new FileStream($"{StateManager.melonPath}/PlaylistArts/{playlist.ArtworkPath}", FileMode.Open, FileAccess.Read);
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                //ms.Write(bytes, 0, (int)file.Length);
                return File(bytes, "image/jpeg");
            }
            catch (Exception)
            {
                return File(StateManager.GetDefaultImage(), "image/jpeg");
            }
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("collection-art")]
        public async Task<IActionResult> DownloadCollectionArt(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var CCollection = mongoDatabase.GetCollection<Collection>("Collections");


            try
            {
                var cFilter = Builders<Collection>.Filter.Eq(x => x._id, id);
                var collection = CCollection.Find(cFilter).ToList()[0];

                // Load image data in MemoryStream
                //MemoryStream ms = new MemoryStream();
                FileStream file = new FileStream($"{StateManager.melonPath}/CollectionArts/{collection.ArtworkPath}", FileMode.Open, FileAccess.Read);
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                //ms.Write(bytes, 0, (int)file.Length);
                return File(bytes, "image/jpeg");
            }
            catch (Exception)
            {
                return File(StateManager.GetDefaultImage(), "image/jpeg");
            }
        }
    }
}
