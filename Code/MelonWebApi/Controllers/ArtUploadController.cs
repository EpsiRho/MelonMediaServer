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

        /// <summary>
        /// Uploads artwork for a specific track.
        /// </summary>
        /// <param name="id">The unique identifier of the track.</param>
        /// <param name="image">The artwork to be uploaded.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">If the artwork is successfully uploaded.</response>
        /// <response code="400">If the input parameters are invalid.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If the track or track file is not found.</response>
        [Authorize(Roles = "Admin")]
        [HttpPost("track-art")]
        [Consumes("multipart/form-data")]
        public ObjectResult UploadTrackArt(string id, IFormFile image)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/art/upload/track-art", curId, new Dictionary<string, object>()
                {
                    { "id", id }
                });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var track = TracksCollection.Find(Builders<Track>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if(track == null)
            {
                args.SendEvent("Track not found", 404, Program.mWebApi);
                return new ObjectResult("Track not found") { StatusCode = 404 };
            }

            ATL.Track file = null;
            try
            {
                file = new ATL.Track(track.Path);
            }
            catch (Exception)
            {
                args.SendEvent("Track file not found", 404, Program.mWebApi);
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
                args.SendEvent("Image error", 400, Program.mWebApi);
                return new ObjectResult("Image error") { StatusCode = 400 };
            }

            track.TrackArtCount++;
            TracksCollection.ReplaceOne(Builders<Track>.Filter.Eq(x => x._id, id), track);

            args.SendEvent("Track art uploaded", 200, Program.mWebApi);
            return new ObjectResult("Track art uploaded") { StatusCode = 200 };
        }

        /// <summary>
        /// Uploads artwork for a specific album.
        /// </summary>
        /// <param name="id">The unique identifier of the album.</param>
        /// <param name="image">The artwork to be uploaded.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">If the artwork is successfully uploaded.</response>
        /// <response code="400">If the input parameters are invalid.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If the album is not found.</response>
        [Authorize(Roles = "Admin")]
        [HttpPost("album-art")]
        [Consumes("multipart/form-data")]
        public ObjectResult UploadAlbumArt(string id, IFormFile image)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/art/upload/album-art", curId, new Dictionary<string, object>()
                {
                    { "id", id }
                });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");

            var album = AlbumsCollection.Find(Builders<Album>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if (album == null)
            {
                args.SendEvent("Album not found", 404, Program.mWebApi);
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
                args.SendEvent("Image error", 400, Program.mWebApi);
                return new ObjectResult("Image error") { StatusCode = 400 };
            }

            if (album.AlbumArtPaths == null)
            {
                album.AlbumArtPaths = new List<string>();
            }

            album.AlbumArtPaths.Add($"{album._id}-{album.AlbumArtCount}.jpg");
            album.AlbumArtCount++;
            AlbumsCollection.ReplaceOne(Builders<Album>.Filter.Eq(x => x._id, id), album);

            args.SendEvent("Album art uploaded", 200, Program.mWebApi);
            return new ObjectResult("Album art uploaded") { StatusCode = 200 };
        }

        /// <summary>
        /// Uploads a profile picture for a specific artist.
        /// </summary>
        /// <param name="id">The unique identifier of the artist.</param>
        /// <param name="image">The artwork to be uploaded.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">If the artwork is successfully uploaded.</response>
        /// <response code="400">If the input parameters are invalid.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If the artist is not found.</response>
        [Authorize(Roles = "Admin")]
        [HttpPost("artist-pfp")]
        [Consumes("multipart/form-data")]
        public ObjectResult UploadArtistPfp(string id, IFormFile image)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/art/upload/artist-pfp", curId, new Dictionary<string, object>()
                {
                    { "id", id }
                });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");

            var artist = ArtistsCollection.Find(Builders<Artist>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if (artist == null)
            {
                args.SendEvent("Artist not found", 404, Program.mWebApi);
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
                args.SendEvent("Image error", 400, Program.mWebApi);
                return new ObjectResult("Image error") { StatusCode = 400 };
            }

            if(artist.ArtistPfpPaths == null)
            {
                artist.ArtistPfpPaths = new List<string>();
            }

            artist.ArtistPfpPaths.Add($"{artist._id}-{artist.ArtistPfpArtCount}.jpg");
            artist.ArtistPfpArtCount++;
            ArtistsCollection.ReplaceOne(Builders<Artist>.Filter.Eq(x => x._id, id), artist);

            args.SendEvent("Artist pfp uploaded", 200, Program.mWebApi);
            return new ObjectResult("Artist pfp uploaded") { StatusCode = 200 };
        }

        /// <summary>
        /// Uploads an artist banner for a specific artist.
        /// </summary>
        /// <param name="id">The unique identifier of the artist.</param>
        /// <param name="image">The artwork to be uploaded.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">If the artwork is successfully uploaded.</response>
        /// <response code="400">If the input parameters are invalid.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If the artist is not found.</response>
        [Authorize(Roles = "Admin")]
        [HttpPost("artist-banner")]
        [Consumes("multipart/form-data")]
        public ObjectResult UploadArtistBanner(string id, IFormFile image)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/art/upload/artist-banner", curId, new Dictionary<string, object>()
                {
                    { "id", id }
                });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");

            var artist = ArtistsCollection.Find(Builders<Artist>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if (artist == null)
            {
                args.SendEvent("Artist not found", 404, Program.mWebApi);
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
                args.SendEvent("Image error", 400, Program.mWebApi);
                return new ObjectResult("Image error") { StatusCode = 400 };
            }

            if (artist.ArtistBannerPaths == null)
            {
                artist.ArtistBannerPaths = new List<string>();
            }

            artist.ArtistBannerPaths.Add($"{artist._id}-{artist.ArtistBannerArtCount}.jpg");
            artist.ArtistBannerArtCount++;
            ArtistsCollection.ReplaceOne(Builders<Artist>.Filter.Eq(x => x._id, id), artist);

            args.SendEvent("Artist banner uploaded", 200, Program.mWebApi);
            return new ObjectResult("Artist banner uploaded") { StatusCode = 200 };
        }

        /// <summary>
        /// Uploads the artwork for a specific playlist, replaces the current one if any.
        /// </summary>
        /// <param name="id">The unique identifier of the playlist.</param>
        /// <param name="image">The artwork to be uploaded.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">If the artwork is successfully uploaded.</response>
        /// <response code="400">If the input parameters are invalid.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If the playlist is not found.</response>
        [Authorize(Roles = "Admin")]
        [HttpPost("playlist-art")]
        [Consumes("multipart/form-data")]
        public ObjectResult UploadPlaylistArt(string id, IFormFile image)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/art/upload/playlist-art", curId, new Dictionary<string, object>()
                {
                    { "id", id }
                });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var PlaylistsCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var playlist = PlaylistsCollection.Find(Builders<Playlist>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if (playlist == null)
            {
                args.SendEvent("Playlist not found", 404, Program.mWebApi);
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
                args.SendEvent("Image error", 400, Program.mWebApi);
                return new ObjectResult("Image error") { StatusCode = 400 };
            }

            playlist.ArtworkPath = $"{playlist._id}.jpg";
            PlaylistsCollection.ReplaceOne(Builders<Playlist>.Filter.Eq(x => x._id, id), playlist);

            args.SendEvent("Playlist art uploaded", 200, Program.mWebApi);
            return new ObjectResult("Playlist art uploaded") { StatusCode = 200 };
        }

        /// <summary>
        /// Uploads the artwork for a specific collection, replaces the current one if any.
        /// </summary>
        /// <param name="id">The unique identifier of the collection.</param>
        /// <param name="image">The artwork to be uploaded.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">If the artwork is successfully uploaded.</response>
        /// <response code="400">If the input parameters are invalid.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If the collection is not found.</response>
        [Authorize(Roles = "Admin")]
        [HttpPost("collection-art")]
        [Consumes("multipart/form-data")]
        public ObjectResult UploadCollectionArt(string id, IFormFile image)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/art/upload/collection-art", curId, new Dictionary<string, object>()
                {
                    { "id", id }
                });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var CollectionsCollection = mongoDatabase.GetCollection<Collection>("Collections");

            var collection = CollectionsCollection.Find(Builders<Collection>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if (collection == null)
            {
                args.SendEvent("Collection not found", 404, Program.mWebApi);
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
                args.SendEvent("Image error", 400, Program.mWebApi);
                return new ObjectResult("Image error") { StatusCode = 400 };
            }

            collection.ArtworkPath = $"{collection._id}.jpg";
            CollectionsCollection.ReplaceOne(Builders<Collection>.Filter.Eq(x => x._id, id), collection);

            args.SendEvent("Collection art uploaded", 200, Program.mWebApi);
            return new ObjectResult("Collection art uploaded") { StatusCode = 200 };
        }

        /// <summary>
        /// Uploads artwork to replace the default artwork.
        /// </summary>
        /// <param name="image">The artwork to be uploaded.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">If the artwork is successfully uploaded.</response>
        /// <response code="400">If the input parameters are invalid.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        [Authorize(Roles = "Admin")]
        [HttpPost("default-art")]
        [Consumes("multipart/form-data")]
        public ObjectResult UploadDefaultArt(IFormFile image)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/art/upload/default-art", curId, new Dictionary<string, object>());

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
                args.SendEvent("Image error", 400, Program.mWebApi);
                return new ObjectResult("Image error") { StatusCode = 400 };
            }

            args.SendEvent("Default art uploaded", 404, Program.mWebApi);
            return new ObjectResult("Default art uploaded") { StatusCode = 200 };
        }

    }
}
