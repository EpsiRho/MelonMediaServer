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

        // Albums
        [Authorize(Roles = "Admin")]
        [HttpPost("album/create")]
        public ObjectResult CreateAlbum(string name)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");

            Album album = new Album()
            {
                _id = ObjectId.GenerateNewId().ToString(),
                AlbumName = name,
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
                Tracks = new List<DbLink>()
            };

            AlbumsCollection.InsertOne(album);

            return new ObjectResult(album._id) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin")]
        [HttpPost("album/delete")]
        public ObjectResult DeleteAlbum(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            AlbumsCollection.DeleteOne(Builders<Album>.Filter.Eq(x=>x._id, id));

            var page = 0;
            var count = 100;
            while (true)
            {
                var artists = ArtistsCollection.Find(Builders<Artist>.Filter.Empty).Skip(page * count).Limit(count).ToList();
                bool update = false;
                foreach (var artist in artists)
                {
                    update = false;
                    if (artist.Releases.Any(x => x._id == id))
                    {
                        artist.Releases.Remove(artist.Releases.Where(x => x._id == id).FirstOrDefault());
                        update = true;
                    }

                    if (artist.SeenOn.Any(x => x._id == id))
                    {
                        artist.SeenOn.Remove(artist.SeenOn.Where(x => x._id == id).FirstOrDefault());
                        update = true;
                    }

                    if (update)
                    {
                        ArtistsCollection.ReplaceOne(Builders<Artist>.Filter.Eq(x => x._id, artist._id), artist);
                    }
                }

                page++;
                if (artists.Count() != 100)
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

            return new ObjectResult("Album deleted") { StatusCode = 200 };
        }

        // Artists
        [Authorize(Roles = "Admin")]
        [HttpPost("artist/create")]
        public ObjectResult CreateArtist(string name)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");

            Artist artist = new Artist()
            {
                _id = ObjectId.GenerateNewId().ToString(),
                ArtistName = name,
                Bio = "",
                Releases = new List<DbLink>(),
                ArtistPfpPaths = new List<string>(),
                ArtistBannerPaths = new List<string>(),
                ArtistPfpArtCount = 0,
                ArtistBannerArtCount = 0,
                ConnectedArtists = new List<DbLink>(),
                Genres = new List<string>(),
                SeenOn = new List<DbLink>(),
                PlayCounts = new List<UserStat>(),
                SkipCounts = new List<UserStat>(),
                Ratings = new List<UserStat>(),
                ServerURL = "",
                DateAdded = DateTime.UtcNow,
                Tracks = new List<DbLink>()
            };

            ArtistsCollection.InsertOne(artist);

            return new ObjectResult(artist._id) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin")]
        [HttpPost("artist/delete")]
        public ObjectResult DeleteArtist(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            ArtistsCollection.DeleteOne(Builders<Artist>.Filter.Eq(x => x._id, id));

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
                foreach (var artist in artists)
                {
                    if (artist.ConnectedArtists.Any(x=>x._id == id))
                    {
                        artist.ConnectedArtists.Remove(artist.ConnectedArtists.Where(x => x._id == id).FirstOrDefault());
                        ArtistsCollection.ReplaceOne(Builders<Artist>.Filter.Eq(x => x._id, artist._id), artist);
                    }
                }

                page++;
                if (artists.Count() != 100)
                {
                    break;
                }
            }

            return new ObjectResult("Artist deleted") { StatusCode = 200 };
        }

    }
}
