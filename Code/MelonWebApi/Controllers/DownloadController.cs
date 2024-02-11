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


            var tFilter = Builders<Track>.Filter.Eq("TrackId", id);
            var track = TCollection.Find(tFilter).ToList()[0];

            FileStream fileStream = new FileStream(track.Path, FileMode.Open, FileAccess.Read);

            if (fileStream == null)
                return NotFound(); 

            string filename = Path.GetFileName(track.Path);
            return File(fileStream, "application/octet-stream", $"{filename}"); 
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
