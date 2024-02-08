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
    public class GeneralController : ControllerBase
    {
        private readonly ILogger<GeneralController> _logger;

        public GeneralController(ILogger<GeneralController> logger)
        {
            _logger = logger;
        }

        // Tracks
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("track")]
        public ObjectResult GetTrack(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var trackFilter = Builders<Track>.Filter.Eq("_id", id);

            var trackProjection = Builders<Track>.Projection.Exclude(x => x.Path)
                                                            .Exclude(x => x.LyricsPath);
            var trackDocs = TracksCollection.Find(trackFilter).Project(trackProjection)
                                            .ToList();

            var docs = trackDocs.Select(x => BsonSerializer.Deserialize<ResponseTrack>(x)).ToList();

            var track = docs.FirstOrDefault();
            if(track == null)
            {
                return new ObjectResult("Track not found") { StatusCode = 404 };
            }

            var usernames = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            usernames.Add(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.Username, User.Identity.Name)).ToList().Select(x => x._id).FirstOrDefault());

            if (track.PlayCounts != null)
            {
                track.PlayCounts = track.PlayCounts.Where(x => usernames.Contains(x.UserId)).ToList();
            }

            if (track.SkipCounts != null)
            {
                track.SkipCounts = track.SkipCounts.Where(x => usernames.Contains(x.UserId)).ToList();
            }

            if (track.Ratings != null)
            {
                track.Ratings = track.Ratings.Where(x => usernames.Contains(x.UserId)).ToList();
            }

            return new ObjectResult(track) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("tracks")]
        public ObjectResult GetTracks([FromQuery] string[] ids)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            var userIds = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            userIds.Add(curId);

            List<ResponseTrack> tracks = new List<ResponseTrack>();
            foreach(var id in ids)
            {
                var trackFilter = Builders<Track>.Filter.Eq("_id", id);
                var trackProjection = Builders<Track>.Projection.Exclude(x => x.Path)
                                                            .Exclude(x => x.LyricsPath);
                var trackDocs = TracksCollection.Find(trackFilter).Project(trackProjection)
                                                .ToList();

                var track = trackDocs.Select(x => BsonSerializer.Deserialize<ResponseTrack>(x)).FirstOrDefault();
                if(track != null)
                {

                    if (track.PlayCounts != null)
                    {
                        track.PlayCounts = track.PlayCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                    }

                    if (track.SkipCounts != null)
                    {
                        track.SkipCounts = track.SkipCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                    }

                    if (track.Ratings != null)
                    {
                        track.Ratings = track.Ratings.Where(x => userIds.Contains(x.UserId)).ToList();
                    }

                    tracks.Add(track);
                }
            }


            return new ObjectResult(tracks) { StatusCode = 200 };
        }

        // Albums
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("album")]
        public ObjectResult GetAlbum(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var albumFilter = Builders<Album>.Filter.Eq("_id", id);
            var albumProjection = Builders<Album>.Projection.Exclude(x=>x.AlbumArtPaths)
                                                            .Exclude(x=>x.Tracks);
            var albumDocs = AlbumsCollection.Find(albumFilter).Project(albumProjection)
                                            .ToList();

            var docs = albumDocs.Select(x => BsonSerializer.Deserialize<ResponseAlbum>(x)).ToList();

            var album = docs.FirstOrDefault();
            if (album == null)
            {
                return new ObjectResult("Album not found") { StatusCode = 404 };
            }

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            var userIds = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            userIds.Add(curId);

            if (album.PlayCounts != null)
            {
                album.PlayCounts = album.PlayCounts.Where(x => userIds.Contains(x.UserId)).ToList();
            }

            if (album.SkipCounts != null)
            {
                album.SkipCounts = album.SkipCounts.Where(x => userIds.Contains(x.UserId)).ToList();
            }

            if (album.Ratings != null)
            {
                album.Ratings = album.Ratings.Where(x => userIds.Contains(x.UserId)).ToList();
            }

            return new ObjectResult(album) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("albums")]
        public ObjectResult GetAlbums([FromQuery] string[] ids)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                    .Where(c => c.Type == ClaimTypes.UserData)
                    .Select(c => c.Value).FirstOrDefault();
            List<string> userIds =
            [
                curId,
                .. UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id),
            ];

            List<ResponseAlbum> albums = new List<ResponseAlbum>();
            foreach (var id in ids)
            {
                var albumFilter = Builders<Album>.Filter.Eq("_id", id);
                var albumProjection = Builders<Album>.Projection.Exclude(x => x.AlbumArtPaths)
                                                            .Exclude(x => x.Tracks);
                var albumDocs = AlbumCollection.Find(albumFilter).Project(albumProjection)
                                                .ToList();


                var album = albumDocs.Select(x => BsonSerializer.Deserialize<ResponseAlbum>(x)).FirstOrDefault();
                if (album != null)
                {
                    try { album.PlayCounts = album.PlayCounts.Where(x => userIds.Contains(x.UserId)).ToList(); } catch (Exception) { }
                    try { album.SkipCounts = album.SkipCounts.Where(x => userIds.Contains(x.UserId)).ToList(); } catch (Exception) { }
                    try { album.Ratings = album.Ratings.Where(x => userIds.Contains(x.UserId)).ToList(); } catch (Exception) { }
                    albums.Add(album);
                }
            }


            return new ObjectResult(albums) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("album/tracks")]
        public ObjectResult GetAlbumTracks(string id, int page = 0, int count = 50)
        {
            if (page > 100000 || count > 100000)
            {
                return new ObjectResult("Page / Count must be below 100000") { StatusCode = 400 };
            }
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var albumFilter = Builders<Album>.Filter.Eq(x=>x._id, id);

            var albumDocs = AlbumsCollection.Find(albumFilter)
                                            .ToList();

            var album = albumDocs.FirstOrDefault();
            if (album == null)
            {
                return new ObjectResult("Album not found") { StatusCode = 404 };
            }

            count = count <= album.Tracks.Count() ? count : album.Tracks.Count();

            var ids = album.Tracks.GetRange((int)(page * count), (int)count).Select(x=>x._id);

            var filter = Builders<Track>.Filter.In("_id", ids);
            var trackProjection = Builders<Track>.Projection.Exclude(x => x.Path)
                                                    .Exclude(x => x.LyricsPath);
            var trackDocs = TracksCollection.Find(filter).Project(trackProjection)
                                            .ToList();

            var tracks = trackDocs.Select(x => BsonSerializer.Deserialize<ResponseTrack>(x)).ToList();

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            var userIds = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            userIds.Add(curId);

            foreach (var track in tracks)
            {


                if (track.PlayCounts != null)
                {
                    track.PlayCounts = track.PlayCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                }

                if (track.SkipCounts != null)
                {
                    track.SkipCounts = track.SkipCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                }

                if (track.Ratings != null)
                {
                    track.Ratings = track.Ratings.Where(x => userIds.Contains(x.UserId)).ToList();
                }

            }

            return new ObjectResult(tracks) { StatusCode = 200 };
        }

        // Artists
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist")]
        public ObjectResult GetArtist(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var artistFilter = Builders<Artist>.Filter.Eq(x=>x._id, id);
            var artistProjection = Builders<Artist>.Projection.Exclude(x => x.ArtistBannerPaths)
                                                              .Exclude(x => x.ArtistArtPaths)
                                                              .Exclude(x => x.Releases)
                                                              .Exclude(x => x.SeenOn)
                                                              .Exclude(x => x.Tracks)
                                                              .Exclude(x => x.ConnectedArtists);
            var artistDocs = ArtistCollection.Find(artistFilter).Project(artistProjection)
                                            .ToList();

            var artist = artistDocs.Select(x => BsonSerializer.Deserialize<ResponseArtist>(x)).FirstOrDefault();
            if (artist == null)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }
            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            List<string> userIds =
            [
                curId,
                .. UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id),
            ];
            try { artist.PlayCounts = artist.PlayCounts.Where(x => userIds.Contains(x.UserId)).ToList(); } catch (Exception) { }
            try { artist.SkipCounts = artist.SkipCounts.Where(x => userIds.Contains(x.UserId)).ToList(); } catch (Exception) { }
            try { artist.Ratings = artist.Ratings.Where(x => userIds.Contains(x.UserId)).ToList(); } catch (Exception) { }

            return new ObjectResult(artist) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artists")]
        public ObjectResult GetArtists([FromQuery] string[] ids)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            var userIds = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            userIds.Add(curId);

            List<ResponseArtist> artists = new List<ResponseArtist>();
            foreach (var id in ids)
            {
                var artistFilter = Builders<Artist>.Filter.Eq(x => x._id, id);
                var artistProjection = Builders<Artist>.Projection.Exclude(x => x.ArtistBannerPaths)
                                                              .Exclude(x => x.ArtistPfpArtCount)
                                                              .Exclude(x => x.Releases)
                                                              .Exclude(x => x.SeenOn)
                                                              .Exclude(x => x.Tracks)
                                                              .Exclude(x => x.ConnectedArtists);
                var albumDocs = ArtistCollection.Find(artistFilter).Project(artistProjection)
                                                .ToList();

                var artist = albumDocs.Select(x => BsonSerializer.Deserialize<ResponseArtist>(x)).FirstOrDefault();
                if (artist != null)
                {

                    if (artist.PlayCounts != null)
                    {
                        artist.PlayCounts = artist.PlayCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                    }

                    if (artist.SkipCounts != null)
                    {
                        artist.SkipCounts = artist.SkipCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                    }

                    if (artist.Ratings != null)
                    {
                        artist.Ratings = artist.Ratings.Where(x => userIds.Contains(x.UserId)).ToList();
                    }

                    artists.Add(artist);
                }
            }


            return new ObjectResult(artists) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist/tracks")]
        public ObjectResult GetArtistTracks(string id, uint page = 0, uint count = 50)
        {
            if(page > 100000 || count > 100000)
            {
                return new ObjectResult("Page / Count must be below 100000") { StatusCode = 400 };
            }

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var artistFilter = Builders<Artist>.Filter.Eq(x=>x._id, id);

            var artistDocs = ArtistsCollection.Find(artistFilter)
                                            .ToList();

            var artist = artistDocs.FirstOrDefault();
            if (artist == null)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            var userIds = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            userIds.Add(curId);

            List<ResponseTrack> tracks = new List<ResponseTrack>();
            for (uint i = (page * count); i < ((page * count) + count); i++)
            {
                try
                {
                    var filter = Builders<Track>.Filter.Eq(x => x._id, artist.Tracks[(int)i]._id);
                    var trackProjection = Builders<Track>.Projection.Exclude(x => x.Path)
                                                            .Exclude(x => x.LyricsPath);
                    var trackDocs = TracksCollection.Find(filter).Project(trackProjection)
                                                    .ToList();

                    var fullTrack = trackDocs.Select(x => BsonSerializer.Deserialize<ResponseTrack>(x)).FirstOrDefault();


                    if (fullTrack.PlayCounts != null)
                    {
                        fullTrack.PlayCounts = fullTrack.PlayCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                    }

                    if (fullTrack.SkipCounts != null)
                    {
                        fullTrack.SkipCounts = fullTrack.SkipCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                    }

                    if (fullTrack.Ratings != null)
                    {
                        fullTrack.Ratings = fullTrack.Ratings.Where(x => userIds.Contains(x.UserId)).ToList();
                    }

                    tracks.Add(fullTrack);
                }
                catch (Exception)
                {

                }
            }

            return new ObjectResult(tracks) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist/releases")]
        public ObjectResult GetArtistReleases(string id, uint page = 0, uint count = 50)
        {
            if (page > 100000 || count > 100000)
            {
                return new ObjectResult("Page / Count must be below 100000") { StatusCode = 400 };
            }
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var artistFilter = Builders<Artist>.Filter.Eq(x=>x._id, id);

            var artistDocs = ArtistsCollection.Find(artistFilter)
                                            .ToList();

            var artist = artistDocs.FirstOrDefault();
            if (artist == null)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }


            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var userIds = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            userIds.Add(curId);

            List<ResponseAlbum> albums = new List<ResponseAlbum>();
            for (uint i = (page * count); i < ((page * count) + count); i++)
            {
                try
                {
                    var filter = Builders<Album>.Filter.Eq(x => x._id, artist.Releases[(int)i]._id);
                    var albumProjection = Builders<Album>.Projection.Exclude(x => x.AlbumArtPaths)
                                                                    .Exclude(x => x.Tracks);
                    var albumDocs = AlbumCollection.Find(filter).Project(albumProjection)
                                                   .ToList();

                    var fullAlbum = albumDocs.Select(x => BsonSerializer.Deserialize<ResponseAlbum>(x)).FirstOrDefault();


                    if (fullAlbum.PlayCounts != null)
                    {
                        fullAlbum.PlayCounts = fullAlbum.PlayCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                    }

                    if (fullAlbum.SkipCounts != null)
                    {
                        fullAlbum.SkipCounts = fullAlbum.SkipCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                    }

                    if (fullAlbum.Ratings != null)
                    {
                        fullAlbum.Ratings = fullAlbum.Ratings.Where(x => userIds.Contains(x.UserId)).ToList();
                    }

                    albums.Add(fullAlbum);
                }
                catch (Exception)
                {

                }
            }

            return new ObjectResult(albums) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist/seen-on")]
        public ObjectResult GetArtistSeenOn(string id, uint page = 0, uint count = 50)
        {
            if (page > 100000 || count > 100000)
            {
                return new ObjectResult("Page / Count must be below 100000") { StatusCode = 400 };
            }
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var artistFilter = Builders<Artist>.Filter.Eq(x=>x._id, id);

            var artistDocs = ArtistsCollection.Find(artistFilter)
                                            .ToList();

            var artist = artistDocs.FirstOrDefault();
            if (artist == null)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }

            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var userIds = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            userIds.Add(curId);

            List<ResponseAlbum> albums = new List<ResponseAlbum>();
            for (uint i = (page * count); i < ((page * count) + count); i++)
            {
                try
                {
                    var filter = Builders<Album>.Filter.Eq(x => x._id, artist.SeenOn[(int)i]._id);
                    var albumProjection = Builders<Album>.Projection.Exclude(x => x.AlbumArtPaths)
                                                                    .Exclude(x => x.Tracks);
                    var albumDocs = AlbumCollection.Find(filter).Project(albumProjection)
                                                   .ToList();

                    var fullAlbum = albumDocs.Select(x => BsonSerializer.Deserialize<ResponseAlbum>(x)).FirstOrDefault();


                    if (fullAlbum.PlayCounts != null)
                    {
                        fullAlbum.PlayCounts = fullAlbum.PlayCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                    }

                    if (fullAlbum.SkipCounts != null)
                    {
                        fullAlbum.SkipCounts = fullAlbum.SkipCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                    }

                    if (fullAlbum.Ratings != null)
                    {
                        fullAlbum.Ratings = fullAlbum.Ratings.Where(x => userIds.Contains(x.UserId)).ToList();
                    }

                    albums.Add(fullAlbum);
                }
                catch (Exception)
                {

                }
            }

            return new ObjectResult(albums) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist/connections")]
        public ObjectResult GetArtistConnections(string id, uint page = 0, uint count = 50)
        {
            if (page > 100000 || count > 100000)
            {
                return new ObjectResult("Page / Count must be below 100000") { StatusCode = 400 };
            }
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var artistFilter = Builders<Artist>.Filter.Eq(x => x._id, id);

            var artistDocs = ArtistsCollection.Find(artistFilter)
                                            .ToList();

            var artist = artistDocs.FirstOrDefault();
            if (artist == null)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }

            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var userIds = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            userIds.Add(curId);


            List<ResponseArtist> albums = new List<ResponseArtist>();
            for (uint i = (page * count); i < ((page * count) + count); i++)
            {
                try
                {
                    var filter = Builders<Artist>.Filter.Eq(x => x._id, artist.ConnectedArtists[(int)i]._id);
                    var artistProjection = Builders<Artist>.Projection.Exclude(x => x.ArtistBannerPaths)
                                                              .Exclude(x => x.ArtistArtPaths)
                                                              .Exclude(x => x.Releases)
                                                              .Exclude(x => x.SeenOn)
                                                              .Exclude(x => x.Tracks)
                                                              .Exclude(x => x.ConnectedArtists);
                    var connDocs = ArtistsCollection.Find(filter).Project(artistProjection)
                                                   .ToList();

                    var fullArtist = connDocs.Select(x => BsonSerializer.Deserialize<ResponseArtist>(x)).FirstOrDefault();


                    if (fullArtist.PlayCounts != null)
                    {
                        fullArtist.PlayCounts = fullArtist.PlayCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                    }

                    if (fullArtist.SkipCounts != null)
                    {
                        fullArtist.SkipCounts = fullArtist.SkipCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                    }

                    if (fullArtist.Ratings != null)
                    {
                        fullArtist.Ratings = fullArtist.Ratings.Where(x => userIds.Contains(x.UserId)).ToList();
                    }

                    albums.Add(fullArtist);
                }
                catch (Exception)
                {

                }
            }

            return new ObjectResult(albums) { StatusCode = 200 };
        }

        // Lyrics
        [Authorize(Roles = "Admin,User")]
        [HttpGet("lyrics")]
        public ObjectResult GetLyrics(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var trackFilter = Builders<Track>.Filter.Eq("_id", id);
            var track = TracksCollection.Find(trackFilter).FirstOrDefault();

            if(track == null)
            {
                return new ObjectResult("Track Not Found") { StatusCode = 404 };
            }

            if(track.LyricsPath == "")
            {
                return new ObjectResult("Track Does Not Have Lyrics") { StatusCode = 404 };
            }

            string txt = System.IO.File.ReadAllText(track.LyricsPath);

            return new ObjectResult(txt) { StatusCode = 200 };
        }
    }
}
