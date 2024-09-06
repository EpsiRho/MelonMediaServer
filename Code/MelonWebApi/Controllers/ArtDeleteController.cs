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
using System.ComponentModel.DataAnnotations;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/art/delete")]
    public class ArtDeleteController : ControllerBase
    {
        private readonly ILogger<ArtDeleteController> _logger;

        public ArtDeleteController(ILogger<ArtDeleteController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Deletes the artwork associated with a specific track.
        /// </summary>
        /// <param name="id">The unique identifier of the track.</param>
        /// <param name="pos">The position of the artwork to be deleted, starting at 0.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">If the artwork is successfully deleted.</response>
        /// <response code="400">If the input parameters are invalid.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If the track or track file is not found.</response>
        [Authorize(Roles = "Admin")]
        [HttpPost("track-art")]
        public ObjectResult DeleteTrackArt([Required(ErrorMessage = "Track ID is required")] string id, 
                                           [Required(ErrorMessage = "Position is required")] [Range(0, int.MaxValue, ErrorMessage = "Position must be positive")] int pos)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/art/delete/track-art", curId, new Dictionary<string, object>()
                {
                    { "id", id },
                    { "pos", pos }
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

            if(track.TrackArtCount < pos)
            {
                args.SendEvent("Invalid position", 400, Program.mWebApi);
                return new ObjectResult("Invalid position") { StatusCode = 400 };
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

            file.EmbeddedPictures.RemoveAt(pos);
            file.Save();

            track.TrackArtCount--;
            TracksCollection.ReplaceOne(Builders<Track>.Filter.Eq(x => x._id, id), track);

            args.SendEvent("Track art removed", 200, Program.mWebApi);
            return new ObjectResult("Track art removed") { StatusCode = 200 };
        }


        /// <summary>
        /// Deletes the profile picture associated with a specific artist.
        /// </summary>
        /// <param name="id">The unique identifier of the artist.</param>
        /// <param name="pos">The position of the artwork to be deleted, starting at 0.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">If the artwork is successfully deleted.</response>
        /// <response code="400">If the input parameters are invalid.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If the artist or artwork file is not found.</response>
        [Authorize(Roles = "Admin")]
        [HttpPost("artist-pfp")]
        public ObjectResult DeleteArtistPfp([Required(ErrorMessage = "Artist ID is required")] string id,
                                            [Required(ErrorMessage = "Position is required")] [Range(0, int.MaxValue, ErrorMessage = "Position must be positive")] int pos)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/art/delete/artist-pfp", curId, new Dictionary<string, object>()
                {
                    { "id", id },
                    { "pos", pos }
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

            if (artist.ArtistPfpArtCount < pos)
            {
                args.SendEvent("Invalid position", 400, Program.mWebApi);
                return new ObjectResult("Invalid position") { StatusCode = 400 };
            }

            var files = System.IO.Directory.GetFiles($"{StateManager.melonPath}/ArtistPfps/");
            var filePath = files.FirstOrDefault(x=>x.Contains($"{StateManager.melonPath}/ArtistPfps/{artist._id}-{pos}"));

            try
            {
                System.IO.File.Delete(filePath);
            }
            catch (Exception)
            {
                args.SendEvent("File error", 404, Program.mWebApi);
                return new ObjectResult("File error") { StatusCode = 404 };
            }

            artist.ArtistPfpPaths.Remove($"{artist._id}-{pos}.jpg");
            artist.ArtistPfpArtCount--;
            ArtistsCollection.ReplaceOne(Builders<Artist>.Filter.Eq(x => x._id, id), artist);

            args.SendEvent("Artist pfp removed", 200, Program.mWebApi);
            return new ObjectResult("Artist pfp removed") { StatusCode = 200 };
        }

        /// <summary>
        /// Deletes the banner artwork associated with a specific artist.
        /// </summary>
        /// <param name="id">The unique identifier of the artist.</param>
        /// <param name="pos">The position of the artwork to be deleted, starting at 0.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">If the artwork is successfully deleted.</response>
        /// <response code="400">If the input parameters are invalid.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If the artist or artwork file is not found.</response>
        [Authorize(Roles = "Admin")]
        [HttpPost("artist-banner")]
        public ObjectResult DeleteArtistBanner([Required(ErrorMessage = "Artist ID is required")] string id,
                                               [Required(ErrorMessage = "Position is required")] [Range(0, int.MaxValue, ErrorMessage = "Position must be positive")] int pos)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/art/delete/artist-banner", curId, new Dictionary<string, object>()
                {
                    { "id", id },
                    { "pos", pos }
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

            if (artist.ArtistPfpArtCount < pos)
            {
                args.SendEvent("Invalid position", 400, Program.mWebApi);
                return new ObjectResult("Invalid position") { StatusCode = 400 };
            }

            var files = System.IO.Directory.GetFiles($"{StateManager.melonPath}/ArtistBanners/");
            var filePath = files.FirstOrDefault(x => x.Contains($"{StateManager.melonPath}/ArtistBanners/{artist._id}-{pos}"));

            try
            {
                System.IO.File.Delete(filePath);
            }
            catch (Exception)
            {

                args.SendEvent("File error", 404, Program.mWebApi);
                return new ObjectResult("File error") { StatusCode = 404 };
            }

            artist.ArtistBannerPaths.Remove($"{artist._id}-{pos}.jpg");
            artist.ArtistBannerArtCount--;
            ArtistsCollection.ReplaceOne(Builders<Artist>.Filter.Eq(x => x._id, id), artist);

            args.SendEvent("Artist banner removed", 200, Program.mWebApi);
            return new ObjectResult("Artist banner removed") { StatusCode = 200 };
        }

        /// <summary>
        /// Deletes the artwork associated with a specific playlist.
        /// </summary>
        /// <param name="id">The unique identifier of the playlist.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">If the artwork is successfully deleted.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If the playlist or artwork file is not found.</response>
        [Authorize(Roles = "Admin")]
        [HttpPost("playlist-art")]
        public ObjectResult DeletePlaylistArt([Required(ErrorMessage = "Playlist ID is required")] string id)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/art/delete/playlist-art", curId, new Dictionary<string, object>()
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

            var files = System.IO.Directory.GetFiles($"{StateManager.melonPath}/PlaylistArts/");
            var filePath = files.FirstOrDefault(x => x.Contains($"{StateManager.melonPath}/PlaylistArts/{playlist._id}"));

            try
            {
                System.IO.File.Delete(filePath);
            }
            catch (Exception)
            {
                args.SendEvent("File error", 404, Program.mWebApi);
                return new ObjectResult("File error") { StatusCode = 404 };
            }

            playlist.ArtworkPath = "";
            PlaylistsCollection.ReplaceOne(Builders<Playlist>.Filter.Eq(x => x._id, id), playlist);

            args.SendEvent("Playlist art removed", 200, Program.mWebApi);
            return new ObjectResult("Playlist art removed") { StatusCode = 200 };
        }

        /// <summary>
        /// Deletes the artwork associated with a specific collection.
        /// </summary>
        /// <param name="id">The unique identifier of the collection.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">If the artwork is successfully deleted.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If the collection or artwork file is not found.</response>
        [Authorize(Roles = "Admin")]
        [HttpPost("collection-art")]
        public ObjectResult DeleteCollectionArt([Required(ErrorMessage = "Collection ID is required")] string id)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/art/delete/collection-art", curId, new Dictionary<string, object>()
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

            var files = System.IO.Directory.GetFiles($"{StateManager.melonPath}/CollectionArts/");
            var filePath = files.FirstOrDefault(x => x.Contains($"{StateManager.melonPath}/CollectionArts/{collection._id}"));

            try
            {
                System.IO.File.Delete(filePath);
            }
            catch (Exception)
            {
                args.SendEvent("File error", 404, Program.mWebApi);
                return new ObjectResult("File error") { StatusCode = 404 };
            }

            collection.ArtworkPath = "";
            CollectionsCollection.ReplaceOne(Builders<Collection>.Filter.Eq(x => x._id, id), collection);

            args.SendEvent("Collection art removed", 200, Program.mWebApi);
            return new ObjectResult("Collection art removed") { StatusCode = 200 };
        }

        /// <summary>
        /// Deletes the custom artwork used when no artwork is found.
        /// </summary>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">If the artwork is successfully deleted.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If the collection or artwork file is not found.</response>
        [Authorize(Roles = "Admin")]
        [HttpPost("default-art")]
        public ObjectResult DeleteDefaultArt()
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/art/delete/default-art", curId, new Dictionary<string, object>());

            var files = System.IO.Directory.GetFiles($"{StateManager.melonPath}/Assets/");
            var filePath = files.FirstOrDefault(x => x.Contains($"{StateManager.melonPath}/Assets/defaultArtwork"));

            try
            {
                System.IO.File.Delete(filePath);
            }
            catch (Exception)
            {
                args.SendEvent("File Not Found", 404, Program.mWebApi);
                return new ObjectResult("File Not Found") { StatusCode = 404 };
            }

            args.SendEvent("Default art removed", 200, Program.mWebApi);
            return new ObjectResult("Default art removed") { StatusCode = 200 };
        }

    }
}
