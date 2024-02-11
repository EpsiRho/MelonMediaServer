using Melon.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using SharpCompress.Common;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing;
using Melon.LocalClasses;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using System.Web.Http.Filters;
using MongoDB.Bson.Serialization;
using System.Security.Claims;
using System.Net;
using ATL.Playlist;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/art/upload")]
    public class ArtUploadController : ControllerBase
    {
        private readonly ILogger<ArtUploadController> _logger;

        public ArtUploadController(ILogger<ArtUploadController> logger)
        {
            _logger = logger;
        }

        // Tracks
        [Authorize(Roles = "Admin")]
        [HttpPost("track-art")]
        [Consumes("multipart/form-data")]
        public ObjectResult UploadTrackArt(string id, IFormFile image)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var track = TracksCollection.Find(Builders<Track>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if(track == null)
            {
                return new ObjectResult("Track not found") { StatusCode = 404 };
            }

            ATL.Track file = null;
            try
            {
                file = new ATL.Track(track.Path);
            }
            catch (Exception)
            {
                return new ObjectResult("Track file not found") { StatusCode = 404 };
            }

            try
            {
                using var fileStream = image.OpenReadStream();
                byte[] bytes = new byte[image.Length];
                fileStream.Read(bytes, 0, (int)image.Length);
                file.EmbeddedPictures.Add(ATL.PictureInfo.fromBinaryData(bytes));
                file.Save();
            }
            catch (Exception)
            {
                return new ObjectResult("Image error") { StatusCode = 400 };
            }

            track.TrackArtCount++;
            TracksCollection.ReplaceOne(Builders<Track>.Filter.Eq(x => x._id, id), track);

            return new ObjectResult("Track art uploaded") { StatusCode = 200 };
        }

        // Albums
        [Authorize(Roles = "Admin")]
        [HttpPost("album-art")]
        [Consumes("multipart/form-data")]
        public ObjectResult UploadAlbumArt(string id, IFormFile image)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");

            var album = AlbumsCollection.Find(Builders<Album>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if (album == null)
            {
                return new ObjectResult("Album not found") { StatusCode = 404 };
            }

            // Save the file
            var filePath =$"{StateManager.melonPath}/AlbumArts/{album._id}-{album.AlbumArtCount}.jpg";
            if (!Directory.Exists($"{StateManager.melonPath}/AlbumArts"))
            {
                Directory.CreateDirectory($"{StateManager.melonPath}/AlbumArts");
            }

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    image.CopyTo(stream);
                }
            }
            catch (Exception)
            {
                return new ObjectResult("Image error") { StatusCode = 400 };
            }

            if (album.AlbumArtPaths == null)
            {
                album.AlbumArtPaths = new List<string>();
            }

            album.AlbumArtPaths.Add($"{album._id}-{album.AlbumArtCount}.jpg");
            album.AlbumArtCount++;
            AlbumsCollection.ReplaceOne(Builders<Album>.Filter.Eq(x => x._id, id), album);

            return new ObjectResult("Album art uploaded") { StatusCode = 200 };
        }

        // Artists
        [Authorize(Roles = "Admin")]
        [HttpPost("artist-pfp")]
        [Consumes("multipart/form-data")]
        public ObjectResult UploadArtistPfp(string id, IFormFile image)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");

            var artist = ArtistsCollection.Find(Builders<Artist>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if (artist == null)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }

            // Save the file
            var filePath = $"{StateManager.melonPath}/ArtistPfps/{artist._id}-{artist.ArtistPfpArtCount}.jpg";
            if (!Directory.Exists($"{StateManager.melonPath}/ArtistPfps"))
            {
                Directory.CreateDirectory($"{StateManager.melonPath}/ArtistPfps");
            }

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    image.CopyTo(stream);
                }
            }
            catch (Exception)
            {
                return new ObjectResult("Image error") { StatusCode = 400 };
            }

            if(artist.ArtistPfpPaths == null)
            {
                artist.ArtistPfpPaths = new List<string>();
            }

            artist.ArtistPfpPaths.Add($"{artist._id}-{artist.ArtistPfpArtCount}.jpg");
            artist.ArtistPfpArtCount++;
            ArtistsCollection.ReplaceOne(Builders<Artist>.Filter.Eq(x => x._id, id), artist);

            return new ObjectResult("Artist pfp uploaded") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin")]
        [HttpPost("artist-banner")]
        [Consumes("multipart/form-data")]
        public ObjectResult UploadArtistBanner(string id, IFormFile image)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");

            var artist = ArtistsCollection.Find(Builders<Artist>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if (artist == null)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }

            // Save the file
            var filePath = $"{StateManager.melonPath}/ArtistBanners/{artist._id}-{artist.ArtistBannerArtCount}.jpg";
            if (!Directory.Exists($"{StateManager.melonPath}/ArtistBanners"))
            {
                Directory.CreateDirectory($"{StateManager.melonPath}/ArtistBanners");
            }

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    image.CopyTo(stream);
                }
            }
            catch (Exception)
            {
                return new ObjectResult("Image error") { StatusCode = 400 };
            }

            if (artist.ArtistBannerPaths == null)
            {
                artist.ArtistBannerPaths = new List<string>();
            }

            artist.ArtistBannerPaths.Add($"{artist._id}-{artist.ArtistBannerArtCount}.jpg");
            artist.ArtistBannerArtCount++;
            ArtistsCollection.ReplaceOne(Builders<Artist>.Filter.Eq(x => x._id, id), artist);

            return new ObjectResult("Artist banner uploaded") { StatusCode = 200 };
        }

        // Playlists
        [Authorize(Roles = "Admin")]
        [HttpPost("playlist-art")]
        [Consumes("multipart/form-data")]
        public ObjectResult UploadPlaylistArt(string id, IFormFile image)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var PlaylistsCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var playlist = PlaylistsCollection.Find(Builders<Playlist>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if (playlist == null)
            {
                return new ObjectResult("Playlist not found") { StatusCode = 404 };
            }

            // Save the file
            var filePath = $"{StateManager.melonPath}/PlaylistArts/{playlist._id}.jpg";
            if (!Directory.Exists($"{StateManager.melonPath}/PlaylistArts"))
            {
                Directory.CreateDirectory($"{StateManager.melonPath}/PlaylistArts");
            }

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    image.CopyTo(stream);
                }
            }
            catch (Exception)
            {
                return new ObjectResult("Image error") { StatusCode = 400 };
            }

            playlist.ArtworkPath = $"{playlist._id}.jpg";
            PlaylistsCollection.ReplaceOne(Builders<Playlist>.Filter.Eq(x => x._id, id), playlist);

            return new ObjectResult("Playlist art uploaded") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin")]
        [HttpPost("collection-art")]
        [Consumes("multipart/form-data")]
        public ObjectResult UploadCollectionArt(string id, IFormFile image)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var CollectionsCollection = mongoDatabase.GetCollection<Collection>("Collections");

            var collection = CollectionsCollection.Find(Builders<Collection>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if (collection == null)
            {
                return new ObjectResult("Collection not found") { StatusCode = 404 };
            }

            // Save the file
            var filePath = $"{StateManager.melonPath}/CollectionArts/{collection._id}.jpg";
            if (!Directory.Exists($"{StateManager.melonPath}/CollectionArts"))
            {
                Directory.CreateDirectory($"{StateManager.melonPath}/CollectionArts");
            }

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    image.CopyTo(stream);
                }
            }
            catch (Exception)
            {
                return new ObjectResult("Image error") { StatusCode = 400 };
            }

            collection.ArtworkPath = $"{collection._id}.jpg";
            CollectionsCollection.ReplaceOne(Builders<Collection>.Filter.Eq(x => x._id, id), collection);

            return new ObjectResult("Collection art uploaded") { StatusCode = 200 };
        }


        [Authorize(Roles = "Admin")]
        [HttpPost("default-art")]
        [Consumes("multipart/form-data")]
        public ObjectResult UploadDefaultArt(IFormFile image)
        {
            // Save the file
            var filePath = $"{StateManager.melonPath}/Assets/defaultArtwork.jpg";
            if (!Directory.Exists($"{StateManager.melonPath}/Assets"))
            {
                Directory.CreateDirectory($"{StateManager.melonPath}/Assets");
            }

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    image.CopyTo(stream);
                }
            }
            catch (Exception)
            {
                return new ObjectResult("Image error") { StatusCode = 400 };
            }

            return new ObjectResult("Default art uploaded") { StatusCode = 200 };
        }

    }
}
