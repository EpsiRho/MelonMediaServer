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
        public async Task DownloadTrack(string id, string transcodeFormat = "", int transcodeBitrate = 256, int frameDurationMs = 20)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var tFilter = Builders<Track>.Filter.Eq(x=>x._id, id);
            var track = TCollection.Find(tFilter).FirstOrDefault();
            if (track == null)
            {
                HttpContext.Response.StatusCode = 404;
                return;
            }

            FileStream fileStream = new FileStream(track.Path, FileMode.Open, FileAccess.Read);

            if (fileStream == null)
            {
                HttpContext.Response.StatusCode = 404;
                return;
            }

            string filename = Path.GetFileName(track.Path);
            if (transcodeFormat != "")
            {
                using (var reader = new AudioFileReader(track.Path)) 
                {
                    WaveFormat targetFormat = null;
                    if (transcodeFormat == "mp3")
                    {
                        targetFormat = new Mp3WaveFormat(reader.WaveFormat.SampleRate, reader.WaveFormat.Channels, 0, transcodeBitrate);

                        LameConfig config = new LameConfig()
                        {
                            BitRate = transcodeBitrate
                        };

                        var split = filename.Split(".");
                        filename = filename.Replace(split[split.Length - 1], ".mp3");
                        HttpContext.Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{filename}\"");
                        HttpContext.Response.StatusCode = 200;
                        HttpContext.Response.ContentType = "audio/mpeg";

                        using (var writer = new LameMP3FileWriter(HttpContext.Response.Body, reader.WaveFormat, config))
                        {
                            reader.CopyTo(writer);
                        }
                        return;
                    }
                    else if (transcodeFormat == "opus")
                    {
                        int channels = reader.WaveFormat.Channels;
                        int outputSampleRate = 48000;
                        int frameSize = outputSampleRate * frameDurationMs / 1000;
                        var opusEncoder = new OpusEncoder(outputSampleRate, channels, OpusApplication.OPUS_APPLICATION_AUDIO)
                        {
                            Bitrate = transcodeBitrate * 1000
                        };

                        var split = filename.Split(".");
                        filename = filename.Replace(split[split.Length - 1], ".opus");
                        HttpContext.Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{filename}\"");
                        HttpContext.Response.StatusCode = 200;
                        HttpContext.Response.ContentType = "audio/mpeg";

                        var oggStream = new OpusOggWriteStream(opusEncoder, HttpContext.Response.Body);
                        int totalSamples = frameSize * channels;
                        float[] buffer = new float[totalSamples]; 
                        short[] shortBuffer = new short[totalSamples];
                        byte[] opusBuffer = new byte[4096];

                        var resampler = new WdlResamplingSampleProvider(reader, outputSampleRate);

                        int bytesRead;
                        try
                        {
                            while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                // Convert float to short
                                for (int i = 0; i < bytesRead; i++)
                                {
                                    shortBuffer[i] = (short)(buffer[i] * short.MaxValue);
                                }
                                oggStream.WriteSamples(shortBuffer, 0, bytesRead);
                            }
                        }
                        catch(Exception)
                        {

                        }
                        return;
                    }
                }
            }

            HttpContext.Response.ContentType = "audio/mpeg";
            HttpContext.Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{filename}\"");
            HttpContext.Response.StatusCode = 200;
            HttpContext.Response.ContentLength = fileStream.Length;

            fileStream.CopyTo(HttpContext.Response.Body);
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
