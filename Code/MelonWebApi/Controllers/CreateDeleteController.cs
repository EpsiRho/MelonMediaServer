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

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/")]
    public class CreateDeleteController : ControllerBase
    {
        private readonly ILogger<CreateDeleteController> _logger;

        public CreateDeleteController(ILogger<CreateDeleteController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Create a new Album.
        /// </summary>
        /// <param name="name">The name of the album.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <returns>The id for the newly created album.</returns>
        /// <response code="200">On successful creation of the album.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        [Authorize(Roles = "Admin")]
        [HttpPost("album/create")]
        public ObjectResult CreateAlbum(string name)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/album/create", curId, new Dictionary<string, object>()
            {
                { "name", name }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");

            Album album = new Album()
            {
                _id = ObjectId.GenerateNewId().ToString(),
                Name = name,
                Bio = "",
                AlbumArtCount = 0,
                AlbumArtists = new List<DbLink>(),
                AlbumArtPaths = new List<string>(),
                AlbumGenres = new List<string>(),
                ContributingArtists = new List<DbLink>(),
                PlayCounts = new List<UserStat>(),
                SkipCounts = new List<UserStat>(),
                Ratings = new List<UserStat>(),
                ServerURL = "",
                ReleaseDate = DateTime.MinValue,
                DateAdded = DateTime.UtcNow,
                Publisher = "",
                ReleaseStatus = "",
                ReleaseType = "",
                TotalDiscs = 0,
                TotalTracks = 0,
                AlbumArtDefault = 0
            };

            AlbumsCollection.InsertOne(album);

            args.SendEvent($"Album created {album._id}", 200, Program.mWebApi);
            return new ObjectResult(album._id) { StatusCode = 200 };
        }
        /// <summary>
        /// Delete an Album.
        /// </summary>
        /// <param name="id">The id of the album.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">On successful creation of the album.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If the album cannot be found.</response>
        [Authorize(Roles = "Admin")]
        [HttpPost("album/delete")]
        public ObjectResult DeleteAlbum(string id)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/album/delete", curId, new Dictionary<string, object>()
            {
                { "id", id }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var album = AlbumsCollection.Find(Builders<Album>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if(album == null)
            {
                args.SendEvent($"Album not found", 404, Program.mWebApi);
                return new ObjectResult("Album not found") { StatusCode = 404 };
            }

            var page = 0;
            var count = 100;
            while (true)
            {
                var tracks = TracksCollection.Find(Builders<Track>.Filter.Empty).Skip(page * count).Limit(count).ToList();
                foreach (var track in tracks)
                {
                    if (track.Album._id == id)
                    {
                        track.Album = null;
                        TracksCollection.ReplaceOne(Builders<Track>.Filter.Eq(x => x._id, track._id), track);
                    }
                }

                page++;
                if (tracks.Count() != 100)
                {
                    break;
                }
            }

            
            foreach(var path in album.AlbumArtPaths)
            {
                var filePath = $"{StateManager.melonPath}/AlbumArts/{path}";

                try
                {
                    System.IO.File.Delete(filePath);
                }
                catch (Exception)
                {

                }
            }

            AlbumsCollection.DeleteOne(Builders<Album>.Filter.Eq(x=>x._id, id));

            args.SendEvent($"Album deleted {album._id}", 200, Program.mWebApi);
            return new ObjectResult("Album deleted") { StatusCode = 200 };
        }

        /// <summary>
        /// Create a new Artist.
        /// </summary>
        /// <param name="name">The name of the artist.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <returns>The id for the newly created artist.</returns>
        /// <response code="200">On successful creation of the artist.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        [Authorize(Roles = "Admin")]
        [HttpPost("artist/create")]
        public ObjectResult CreateArtist(string name)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/artist/create", curId, new Dictionary<string, object>()
            {
                { "name", name }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");

            Artist artist = new Artist()
            {
                _id = ObjectId.GenerateNewId().ToString(),
                Name = name,
                Bio = "",
                ArtistPfpPaths = new List<string>(),
                ArtistBannerPaths = new List<string>(),
                ArtistPfpArtCount = 0,
                ArtistBannerArtCount = 0,
                ConnectedArtists = new List<DbLink>(),
                Genres = new List<string>(),
                PlayCounts = new List<UserStat>(),
                SkipCounts = new List<UserStat>(),
                Ratings = new List<UserStat>(),
                ServerURL = "",
                DateAdded = DateTime.UtcNow,
                ArtistBannerArtDefault = 0,
                ArtistPfpDefault = 0,
            };

            ArtistsCollection.InsertOne(artist);

            args.SendEvent($"Artist created {artist._id}", 200, Program.mWebApi);
            return new ObjectResult(artist._id) { StatusCode = 200 };
        }

        /// <summary>
        /// Delete an Artist.
        /// </summary>
        /// <param name="id">The id of the artist.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">On successful creation of the artist.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If the artist cannot be found.</response>
        [Authorize(Roles = "Admin")]
        [HttpPost("artist/delete")]
        public ObjectResult DeleteArtist(string id)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/artist/delete", curId, new Dictionary<string, object>()
            {
                { "id", id }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var artist = ArtistsCollection.Find(Builders<Artist>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if(artist == null)
            {
                new ObjectResult("Artist not found") { StatusCode = 404 };
            }

            var page = 0;
            var count = 100;
            while (true)
            {
                var albums = AlbumsCollection.Find(Builders<Album>.Filter.Empty).Skip(page * count).Limit(count).ToList();
                bool update = false;
                foreach (var album in albums)
                {
                    update = false;
                    if (album.AlbumArtists.Any(x => x._id == id))
                    {
                        album.AlbumArtists.Remove(album.AlbumArtists.Where(x => x._id == id).FirstOrDefault());
                        update = true;
                    }

                    if (album.ContributingArtists.Any(x => x._id == id))
                    {
                        album.ContributingArtists.Remove(album.ContributingArtists.Where(x => x._id == id).FirstOrDefault());
                        update = true;
                    }

                    if (update)
                    {
                        AlbumsCollection.ReplaceOne(Builders<Album>.Filter.Eq(x => x._id, album._id), album);
                    }
                }

                page++;
                if (albums.Count() != 100)
                {
                    break;
                }
            }
            page = 0;
            while (true)
            {
                var tracks = TracksCollection.Find(Builders<Track>.Filter.Empty).Skip(page * count).Limit(count).ToList();
                foreach (var track in tracks)
                {
                    if (track.TrackArtists.Any(x=>x._id == id))
                    {
                        track.TrackArtists.Remove(track.TrackArtists.Where(x => x._id == id).FirstOrDefault());
                        TracksCollection.ReplaceOne(Builders<Track>.Filter.Eq(x => x._id, track._id), track);
                    }
                }

                page++;
                if (tracks.Count() != 100)
                {
                    break;
                }
            }
            page = 0;
            while (true)
            {
                var artists = ArtistsCollection.Find(Builders<Artist>.Filter.Empty).Skip(page * count).Limit(count).ToList();
                foreach (var a in artists)
                {
                    if (a.ConnectedArtists.Any(x=>x._id == id))
                    {
                        a.ConnectedArtists.Remove(a.ConnectedArtists.Where(x => x._id == id).FirstOrDefault());
                        ArtistsCollection.ReplaceOne(Builders<Artist>.Filter.Eq(x => x._id, a._id), a);
                    }
                }

                page++;
                if (artists.Count() != 100)
                {
                    break;
                }
            }

            foreach (var path in artist.ArtistPfpPaths)
            {
                var filePath = $"{StateManager.melonPath}/ArtistPfps/{path}";

                try
                {
                    System.IO.File.Delete(filePath);
                }
                catch (Exception)
                {

                }
            }
            foreach (var path in artist.ArtistBannerPaths)
            {
                var filePath = $"{StateManager.melonPath}/ArtistBanners/{path}";

                try
                {
                    System.IO.File.Delete(filePath);
                }
                catch (Exception)
                {

                }
            }

            ArtistsCollection.DeleteOne(Builders<Artist>.Filter.Eq(x => x._id, id));

            args.SendEvent($"Artist deleted {artist._id}", 200, Program.mWebApi);
            return new ObjectResult("Artist deleted") { StatusCode = 200 };
        }

    }
}
