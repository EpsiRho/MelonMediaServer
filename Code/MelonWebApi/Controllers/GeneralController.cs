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
using MelonLib.API;

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
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/track", curId, new Dictionary<string, object>()
                {
                    { "id", id }
                });

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
                args.SendEvent("Track not found", 404, Program.mWebApi);
                return new ObjectResult("Track not found") { StatusCode = 404 };
            }

            var usernames = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            usernames.Add(curId);

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

            args.SendEvent("Track Sent", 200, Program.mWebApi);
            return new ObjectResult(track) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("tracks")]
        public ObjectResult GetTracks([FromQuery] List<string> ids)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/tracks", curId, new Dictionary<string, object>()
                {
                    { "ids", ids }
                });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

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

            args.SendEvent("Tracks sent", 200, Program.mWebApi);
            return new ObjectResult(tracks) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin")]
        [HttpGet("track/path")]
        public ObjectResult GetTrackPath(string id)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/track/path", curId, new Dictionary<string, object>()
                {
                    { "id", id }
                });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var trackFilter = Builders<Track>.Filter.Eq("_id", id);

            var track = TracksCollection.Find(trackFilter).FirstOrDefault();
            if (track == null)
            {
                args.SendEvent("Track not found", 404, Program.mWebApi);
                return new ObjectResult("Track not found") { StatusCode = 404 };
            }

            args.SendEvent("Track Path Sent", 200, Program.mWebApi);
            return new ObjectResult(track.Path) { StatusCode = 200 };
        }

        // Albums
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("album")]
        public ObjectResult GetAlbum(string id)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/album", curId, new Dictionary<string, object>()
                {
                    { "id", id }
                });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var albumFilter = Builders<Album>.Filter.Eq("_id", id);
            var albumProjection = Builders<Album>.Projection.Exclude(x=>x.AlbumArtPaths);
            var albumDocs = AlbumsCollection.Find(albumFilter).Project(albumProjection)
                                            .ToList();

            var docs = albumDocs.Select(x => BsonSerializer.Deserialize<ResponseAlbum>(x)).ToList();

            var album = docs.FirstOrDefault();
            if (album == null)
            {
                args.SendEvent("Album not found", 200, Program.mWebApi);
                return new ObjectResult("Album not found") { StatusCode = 404 };
            }

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

            args.SendEvent("Album sent", 200, Program.mWebApi);
            return new ObjectResult(album) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("albums")]
        public ObjectResult GetAlbums([FromQuery] List<string> ids)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/albums", curId, new Dictionary<string, object>()
                {
                    { "ids", ids }
                });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            List<string> userIds =
            [
                curId,
                .. UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id),
            ];

            List<ResponseAlbum> albums = new List<ResponseAlbum>();
            foreach (var id in ids)
            {
                var albumFilter = Builders<Album>.Filter.Eq("_id", id);
                var albumProjection = Builders<Album>.Projection.Exclude(x => x.AlbumArtPaths);
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

            args.SendEvent("Albums sent", 200, Program.mWebApi);
            return new ObjectResult(albums) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("album/tracks")]
        public ObjectResult GetAlbumTracks(string id, int page = 0, int count = 100, string sort = "AlbumPositionAsc")
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/album/tracks", curId, new Dictionary<string, object>()
                {
                    { "id", id },
                    { "page", page },
                    { "count", count }
                });

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
                args.SendEvent("Album not found", 200, Program.mWebApi);
                return new ObjectResult("Album not found") { StatusCode = 404 };
            }

            var tracks = MelonAPI.FindTracks(new List<string>() { $"Album._id;Eq;{id}" }, null, curId, page, count, sort);

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

            args.SendEvent("album tracks sent", 200, Program.mWebApi);

            return new ObjectResult(tracks) { StatusCode = 200 };
        }

        // Artists
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist")]
        public ObjectResult GetArtist(string id)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/artist", curId, new Dictionary<string, object>()
                {
                    { "id", id }
                });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var artistFilter = Builders<Artist>.Filter.Eq(x=>x._id, id);
            var artistProjection = Builders<Artist>.Projection.Exclude(x => x.ArtistBannerPaths)
                                                              .Exclude(x => x.ArtistPfpPaths)
                                                              .Exclude(x => x.ConnectedArtists);
            var artistDocs = ArtistCollection.Find(artistFilter).Project(artistProjection)
                                            .ToList();

            var artist = artistDocs.Select(x => BsonSerializer.Deserialize<ResponseArtist>(x)).FirstOrDefault();
            if (artist == null)
            {
                args.SendEvent("Artist not found", 404, Program.mWebApi);
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }

            List<string> userIds =
            [
                curId,
                .. UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id),
            ];
            try { artist.PlayCounts = artist.PlayCounts.Where(x => userIds.Contains(x.UserId)).ToList(); } catch (Exception) { }
            try { artist.SkipCounts = artist.SkipCounts.Where(x => userIds.Contains(x.UserId)).ToList(); } catch (Exception) { }
            try { artist.Ratings = artist.Ratings.Where(x => userIds.Contains(x.UserId)).ToList(); } catch (Exception) { }

            args.SendEvent("Artist sent", 200, Program.mWebApi);
            return new ObjectResult(artist) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artists")]
        public ObjectResult GetArtists([FromQuery] List<string> ids)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/artists", curId, new Dictionary<string, object>()
                {
                    { "ids", ids }
                });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var userIds = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            userIds.Add(curId);

            List<ResponseArtist> artists = new List<ResponseArtist>();
            foreach (var id in ids)
            {
                var artistFilter = Builders<Artist>.Filter.Eq(x => x._id, id);
                var artistProjection = Builders<Artist>.Projection.Exclude(x => x.ArtistBannerPaths)
                                                                  .Exclude(x => x.ArtistPfpPaths)
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

            args.SendEvent("Artists sent", 200, Program.mWebApi);
            return new ObjectResult(artists) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist/tracks")]
        public ObjectResult GetArtistTracks(string id, int page = 0, int count = 100, string sort = "ReleaseDateDesc")
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/artist", curId, new Dictionary<string, object>()
                {
                    { "id", id },
                    { "page", page },
                    { "count", count }
                });

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
                args.SendEvent("Artist not found", 404, Program.mWebApi);
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }

            var userIds = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            userIds.Add(curId);

            List<ResponseTrack> fixedTracks = new List<ResponseTrack>();
            //var f = Builders<Track>.Filter.ElemMatch(x => x.TrackArtists, artist => artist._id == id);
            //var tracks = TracksCollection.Find(f).ToList();
            var tracks = MelonAPI.FindTracks(new List<string>() { $"TrackArtists;Eq;{id}" }, null, curId, page, count, sort);

            if(tracks == null)
            {
                args.SendEvent("No tracks found", 404, Program.mWebApi);
                return new ObjectResult("No Tracks Found") { StatusCode = 404 };
            }

            foreach (var track in tracks)
            {
                try
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

                    fixedTracks.Add(new ResponseTrack(track));
                }
                catch (Exception)
                {

                }
            }

            args.SendEvent("Artist tracks sent", 200, Program.mWebApi);
            return new ObjectResult(fixedTracks) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist/releases")]
        public ObjectResult GetArtistReleases(string id, int page = 0, int count = 100, string sort = "ReleaseDateDesc")
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/artist", curId, new Dictionary<string, object>()
                {
                    { "id", id },
                    { "page", page },
                    { "count", count }
                });

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
                args.SendEvent("Artist not found", 404, Program.mWebApi);
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }

            var userIds = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            userIds.Add(curId);

            List<ResponseAlbum> fixedAlbums = new List<ResponseAlbum>();
            //var f = Builders<Album>.Filter.ElemMatch(x => x.AlbumArtists, artist => artist._id == id);
            //var albums = AlbumCollection.Find(f).ToList();

            var albums = MelonAPI.FindAlbums(new List<string>() { $"AlbumArtists;Eq;{id}" }, null, curId, page, count, sort);

            foreach (var album in albums)
            {
                try
                {
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

                    fixedAlbums.Add(new ResponseAlbum(album));
                }
                catch (Exception)
                {

                }
            }

            args.SendEvent("Artist releases sent", 200, Program.mWebApi);
            return new ObjectResult(albums) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist/seen-on")]
        public ObjectResult GetArtistSeenOn(string id, int page = 0, int count = 100, string sort = "ReleaseDateDesc")
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/artist", curId, new Dictionary<string, object>()
                {
                    { "id", id },
                    { "page", page },
                    { "count", count }
                });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var artistFilter = Builders<Artist>.Filter.Eq(x=>x._id, id);

            var artistDocs = ArtistsCollection.Find(artistFilter)
                                            .ToList();

            var artist = artistDocs.FirstOrDefault();
            if (artist == null)
            {
                args.SendEvent("Artist not found", 404, Program.mWebApi);
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }

            var userIds = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            userIds.Add(curId);

            List<ResponseAlbum> fixedAlbums = new List<ResponseAlbum>();
            //var f = Builders<Album>.Filter.ElemMatch(x => x.ContributingArtists, artist => artist._id == id);
            //var albums = AlbumCollection.Find(f).ToList();

            var albums = MelonAPI.FindAlbums(new List<string>() { $"ContributingArtists;Eq;{id}" }, null, curId, page, count, sort);

            foreach (var album in albums)
            {
                try
                {
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

                    fixedAlbums.Add(new ResponseAlbum(album));
                }
                catch (Exception)
                {

                }
            }

            args.SendEvent("Artist seen-on sent", 200, Program.mWebApi);
            return new ObjectResult(fixedAlbums) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist/connections")]
        public ObjectResult GetArtistConnections(string id, uint page = 0, uint count = 100)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/artist", curId, new Dictionary<string, object>()
                {
                    { "id", id },
                    { "page", page },
                    { "count", count }
                });

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
                args.SendEvent("Artist Not Found", 404, Program.mWebApi);
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }

            var userIds = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            userIds.Add(curId);


            List<ResponseArtist> artists = new List<ResponseArtist>();
            for (uint i = (page * count); i < ((page * count) + count); i++)
            {
                try
                {
                    var filter = Builders<Artist>.Filter.Eq(x => x._id, artist.ConnectedArtists[(int)i]._id);
                    var artistProjection = Builders<Artist>.Projection.Exclude(x => x.ArtistBannerPaths)
                                                              .Exclude(x => x.ArtistPfpPaths)
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

                    artists.Add(fullArtist);
                }
                catch (Exception)
                {

                }
            }

            args.SendEvent("Artist connections sent", 200, Program.mWebApi);
            return new ObjectResult(artists) { StatusCode = 200 };
        }

        // Lyrics
        [Authorize(Roles = "Admin,User")]
        [HttpGet("lyrics")]
        public ObjectResult GetLyrics(string id)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/lyrics", curId, new Dictionary<string, object>()
                {
                    { "id", id }
                });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var trackFilter = Builders<Track>.Filter.Eq("_id", id);
            var track = TracksCollection.Find(trackFilter).FirstOrDefault();

            if(track == null)
            {
                args.SendEvent("Track Not Found", 404, Program.mWebApi);
                return new ObjectResult("Track Not Found") { StatusCode = 404 };
            }

            if(track.LyricsPath == "")
            {
                args.SendEvent("Track Does Not Have Lyrics", 404, Program.mWebApi);
                return new ObjectResult("Track Does Not Have Lyrics") { StatusCode = 404 };
            }

            string txt = System.IO.File.ReadAllText(track.LyricsPath);

            args.SendEvent("Lyrics sent", 200, Program.mWebApi);
            return new ObjectResult(txt) { StatusCode = 200 };
        }
    }
}
