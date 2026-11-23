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
using MongoDB.Bson.Serialization;
using System.Security.Claims;
using System.Net;
using ATL.Playlist;
using MelonWebApi.Services;
using MelonWebApi.Middleware;

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
        [UploadLimit(UploadKind.Artwork)]
        public async Task<ObjectResult> UploadTrackArt(string id, IFormFile image)
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

            var (picture, error) = await ImageUpload.Read(image);
            if (picture == null)
            {
                args.SendEvent(error, 400, Program.mWebApi);
                return new ObjectResult(error) { StatusCode = 400 };
            }

            try
            {
                file.EmbeddedPictures.Add(ATL.PictureInfo.fromBinaryData(picture.Bytes));
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
        [UploadLimit(UploadKind.Artwork)]
        public async Task<ObjectResult> UploadArtistPfp(string id, IFormFile image)
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

            var (name, failed) = await SaveImage(image, "ArtistPfps", $"{artist._id}-{artist.ArtistPfpArtCount}", args);
            if (failed != null) return failed;

            if(artist.ArtistPfpPaths == null)
            {
                artist.ArtistPfpPaths = new List<string>();
            }

            artist.ArtistPfpPaths.Add(name);
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
        [UploadLimit(UploadKind.Artwork)]
        public async Task<ObjectResult> UploadArtistBanner(string id, IFormFile image)
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

            var (name, failed) = await SaveImage(image, "ArtistBanners", $"{artist._id}-{artist.ArtistBannerArtCount}", args);
            if (failed != null) return failed;

            if (artist.ArtistBannerPaths == null)
            {
                artist.ArtistBannerPaths = new List<string>();
            }

            artist.ArtistBannerPaths.Add(name);
            artist.ArtistBannerArtCount++;
            ArtistsCollection.ReplaceOne(Builders<Artist>.Filter.Eq(x => x._id, id), artist);

            args.SendEvent("Artist banner uploaded", 200, Program.mWebApi);
            return new ObjectResult("Artist banner uploaded") { StatusCode = 200 };
        }

        /// <summary>
        /// Uploads an album artwork for a specific album.
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
        /// <response code="404">If the artist is not found.</response>
        [Authorize(Roles = "Admin")]
        [HttpPost("album-art")]
        [Consumes("multipart/form-data")]
        [UploadLimit(UploadKind.Artwork)]
        public async Task<ObjectResult> UploadAlbumArtwork(string id, IFormFile image)
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
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");

            var album = AlbumCollection.Find(Builders<Album>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if (album == null)
            {
                args.SendEvent("Album not found", 404, Program.mWebApi);
                return new ObjectResult("Album not found") { StatusCode = 404 };
            }

            var (name, failed) = await SaveImage(image, "AlbumArts", $"{album._id}-{album.AlbumArtCount}", args);
            if (failed != null) return failed;

            if (album.AlbumArtPaths == null)
            {
                album.AlbumArtPaths = new List<string>();
            }

            album.AlbumArtPaths.Add(name);
            album.AlbumArtCount++;
            AlbumCollection.ReplaceOne(Builders<Album>.Filter.Eq(x => x._id, id), album);

            args.SendEvent("Album artwork uploaded", 200, Program.mWebApi);
            return new ObjectResult("Album artwork uploaded") { StatusCode = 200 };
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
        [Authorize(Roles = "Admin,User")]
        [HttpPost("playlist-art")]
        [Consumes("multipart/form-data")]
        [UploadLimit(UploadKind.Artwork)]
        public async Task<ObjectResult> UploadPlaylistArt(string id, IFormFile image)
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
            if (!MayChangeArt(playlist.Owner, curId))
            {
                args.SendEvent("Only the owner can change this playlist's artwork", 403, Program.mWebApi);
                return new ObjectResult("Only the owner can change this playlist's artwork") { StatusCode = 403 };
            }

            var (name, failed) = await SaveImage(image, "PlaylistArts", playlist._id, args);
            if (failed != null) return failed;

            playlist.ArtworkPath = name;
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
        [Authorize(Roles = "Admin,User")]
        [HttpPost("collection-art")]
        [Consumes("multipart/form-data")]
        [UploadLimit(UploadKind.Artwork)]
        public async Task<ObjectResult> UploadCollectionArt(string id, IFormFile image)
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
            if (!MayChangeArt(collection.Owner, curId))
            {
                args.SendEvent("Only the owner can change this collection's artwork", 403, Program.mWebApi);
                return new ObjectResult("Only the owner can change this collection's artwork") { StatusCode = 403 };
            }

            var (name, failed) = await SaveImage(image, "CollectionArts", collection._id, args);
            if (failed != null) return failed;

            collection.ArtworkPath = name;
            CollectionsCollection.ReplaceOne(Builders<Collection>.Filter.Eq(x => x._id, id), collection);

            args.SendEvent("Collection art uploaded", 200, Program.mWebApi);
            return new ObjectResult("Collection art uploaded") { StatusCode = 200 };
        }

        /// <summary>
        /// Sets a user's profile picture. Users set their own; admins can replace anyone's (abuse moderation).
        /// </summary>
        /// <param name="id">The user to change (omit for yourself).</param>
        /// <param name="image">The picture (PNG or JPEG; re-encoded server-side).</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin, User (own picture only)
        /// </remarks>
        /// <response code="200">If the picture was saved.</response>
        /// <response code="400">If the file isn't a usable image.</response>
        /// <response code="403">If a non-admin tries to change someone else's picture.</response>
        /// <response code="404">If the user doesn't exist.</response>
        [Authorize(Roles = "Admin,User")]
        [HttpPost("user-pfp")]
        [Consumes("multipart/form-data")]
        [UploadLimit(UploadKind.Artwork)]
        public async Task<ObjectResult> UploadUserPfp(string id, IFormFile image)
        {
            var curId = User.FindFirstValue(ClaimTypes.UserData);
            id = string.IsNullOrEmpty(id) ? curId : id;
            var args = new WebApiEventArgs("api/art/upload/user-pfp", curId, new Dictionary<string, object>() { { "id", id } });
            if (id != curId && !User.IsInRole("Admin"))
            {
                args.SendEvent("You can only change your own profile picture", 403, Program.mWebApi);
                return new ObjectResult("You can only change your own profile picture") { StatusCode = 403 };
            }

            var users = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString).GetDatabase("Melon").GetCollection<User>("Users");
            var user = users.Find(Builders<User>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if (user == null)
            {
                args.SendEvent("User not found", 404, Program.mWebApi);
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            var (name, failed) = await SaveImage(image, "UserPfps", user._id, args);
            if (failed != null) return failed;
            // A new picture in the other format would leave the old file behind.
            if (!string.IsNullOrEmpty(user.ProfilePicPath) && user.ProfilePicPath != name)
                try { System.IO.File.Delete(SafePath.Combine(Path.Combine(StateManager.melonPath, "UserPfps"), user.ProfilePicPath)); } catch { }
            users.UpdateOne(Builders<User>.Filter.Eq(x => x._id, id), Builders<User>.Update.Set(x => x.ProfilePicPath, name));

            args.SendEvent("Profile picture uploaded", 200, Program.mWebApi);
            return new ObjectResult("Profile picture uploaded") { StatusCode = 200 };
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
        [UploadLimit(UploadKind.Artwork)]
        public async Task<ObjectResult> UploadDefaultArt(IFormFile image)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/art/upload/default-art", curId, new Dictionary<string, object>());

            // StateManager.GetDefaultImage reads defaultArtwork.jpg, so this one is always JPEG under that exact name.
            var (_, failed) = await SaveImage(image, "Assets", "defaultArtwork", args, forceJpeg: true, extension: "jpg");
            if (failed != null) return failed;

            args.SendEvent("Default art uploaded", 200, Program.mWebApi);
            return new ObjectResult("Default art uploaded") { StatusCode = 200 };
        }

        /// <summary>
        /// Playlist/collection art: the owner changes their own; admins can change anyone's (to replace an abusive picture).
        /// </summary>
        private bool MayChangeArt(string owner, string curId) =>
            User.IsInRole("Admin") || (!string.IsNullOrEmpty(owner) && (owner == curId || owner == User.Identity?.Name));

        /// <summary>
        /// Decodes and re-encodes <paramref name="image"/> (see <see cref="ImageUpload"/>) and writes it as
        /// <c>&lt;melonPath&gt;/&lt;folder&gt;/&lt;stem&gt;.&lt;ext&gt;</c>. The extension comes from the re-encode, never from the request.
        /// Returns the file name to store, or the error response to send.
        /// </summary>
        private static async Task<(string Name, ObjectResult Failed)> SaveImage(IFormFile image, string folder, string stem, WebApiEventArgs args, bool forceJpeg = false, string extension = null)
        {
            var (picture, error) = await ImageUpload.Read(image, forceJpeg);
            if (picture == null)
            {
                args.SendEvent(error, 400, Program.mWebApi);
                return (null, new ObjectResult(error) { StatusCode = 400 });
            }

            try
            {
                var dir = Path.Combine(StateManager.melonPath, folder);
                Directory.CreateDirectory(dir);
                var name = $"{stem}.{extension ?? picture.Extension}";
                await System.IO.File.WriteAllBytesAsync(SafePath.Combine(dir, name), picture.Bytes);
                return (name, null);
            }
            catch (Exception)
            {
                args.SendEvent("Image error", 400, Program.mWebApi);
                return (null, new ObjectResult("Image error") { StatusCode = 400 });
            }
        }

    }
}
