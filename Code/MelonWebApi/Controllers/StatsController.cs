using Melon.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using SharpCompress.Common;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing;
using Melon.LocalClasses;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/stats")]
    public class StatsController : ControllerBase
    {
        private readonly ILogger<StatsController> _logger;

        public StatsController(ILogger<StatsController> logger)
        {
            _logger = logger;
        }

        [Authorize(Roles = "Admin,User")]
        [HttpPost("log-play")]
        public ObjectResult LogPlay(string id, string device = "", string dateTime = "")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");
            var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");

            // Get track, album, artists
            Track track = null;
            var tFilter = Builders<Track>.Filter.Eq("TrackId", id);
            track = TCollection.Find(tFilter).FirstOrDefault();

            if(track == null)
            {
                return new ObjectResult("Track Not Found") { StatusCode = 404 };
            }

            var albumFilter = Builders<Album>.Filter.Eq("AlbumId", track.Album.AlbumId);
            var album = AlbumCollection.Find(albumFilter).ToList()[0];

            // Update artists
            List<Artist> artists = new List<Artist>();
            foreach (var a in track.TrackArtists)
            {
                var artistFilter = Builders<Artist>.Filter.Eq("ArtistId", a.ArtistId);
                var artist = ArtistCollection.Find(artistFilter).ToList()[0];
                if (artist.PlayCounts == null)
                {
                    artist.PlayCounts = new List<UserStat>() { new UserStat() { Username = User.Identity.Name, Value = 1 } };
                }
                else 
                { 
                    var curPC = artist.PlayCounts.Where(x => x.Username == User.Identity.Name).FirstOrDefault();
                    if (curPC != null)
                    {
                        artist.PlayCounts[artist.PlayCounts.IndexOf(curPC)].Value++;
                    }
                    else
                    {
                        artist.PlayCounts.Add(new UserStat() { Username = User.Identity.Name, Value = 1 });
                    }
                }
                artists.Add(artist);
                ArtistCollection.ReplaceOne(artistFilter, artist);
            }

            // Update track
            if (track.PlayCounts == null)
            {
                track.PlayCounts = new List<UserStat>() { new UserStat() { Username = User.Identity.Name, Value = 1 } };
            }
            else
            {
                var curTC = track.PlayCounts.Where(x => x.Username == User.Identity.Name).FirstOrDefault();
                if (curTC != null)
                {
                    track.PlayCounts[track.PlayCounts.IndexOf(curTC)].Value++;
                }
                else
                {
                    track.PlayCounts.Add(new UserStat() { Username = User.Identity.Name, Value = 1 });
                }
            }

            // Update album
            if (album.PlayCounts == null)
            {
                album.PlayCounts = new List<UserStat>() { new UserStat() { Username = User.Identity.Name, Value = 1 } };
            }
            else
            {
                var curAC = album.PlayCounts.Where(x => x.Username == User.Identity.Name).FirstOrDefault();
                if (curAC != null)
                {
                    album.PlayCounts[album.PlayCounts.IndexOf(curAC)].Value++;
                }
                else
                {
                    album.PlayCounts.Add(new UserStat() { Username = User.Identity.Name, Value = 1 });
                }
            }
            TCollection.ReplaceOne(tFilter, track);
            AlbumCollection.ReplaceOne(albumFilter, album);

            // Add Play Stat
            PlayStat stat = new PlayStat();
            stat._id = new MelonId(ObjectId.GenerateNewId());
            stat.StatId = stat._id.ToString();
            stat.TrackId = track.TrackId; 
            stat.AlbumId = album.AlbumId;
            stat.Duration = track.Duration;
            stat.ArtistIds =
            [
                .. from a in artists
                   select a.ArtistId,
            ];
            stat.Device = device;
            stat.User = User.Identity.Name;
            if(dateTime != "")
            {
                try
                {
                    stat.LogDate = DateTime.Parse(dateTime).ToUniversalTime();
                }
                catch (Exception)
                {
                    return new ObjectResult("Invalid Datetime") { StatusCode = 400 };
                }
            }
            else
            {
                stat.LogDate = DateTime.Now.ToUniversalTime();
            }
            stat.Genres = new List<string>();
            foreach (var genre in track.TrackGenres)
            {
                stat.Genres.Add(genre);
            }

            StatsCollection.InsertOne(stat);

            return new ObjectResult("Play Logged") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("log-skip")]
        public ObjectResult LogSkip(string id, string device = "", string dateTime = "")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");
            var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");

            // Get track, album, artists
            Track track = null;
            var tFilter = Builders<Track>.Filter.Eq("TrackId", id);
            track = TCollection.Find(tFilter).FirstOrDefault();

            if (track == null)
            {
                return new ObjectResult("Track Not Found") { StatusCode = 404 };
            }

            var albumFilter = Builders<Album>.Filter.Eq("AlbumId", track.Album.AlbumId);
            var album = AlbumCollection.Find(albumFilter).ToList()[0];

            // Update artists
            List<Artist> artists = new List<Artist>();
            foreach (var a in track.TrackArtists)
            {
                var artistFilter = Builders<Artist>.Filter.Eq("ArtistId", a.ArtistId);
                var artist = ArtistCollection.Find(artistFilter).ToList()[0];
                if (artist.SkipCounts == null)
                {
                    artist.SkipCounts = new List<UserStat>() { new UserStat() { Username = User.Identity.Name, Value = 1 } };
                }
                else
                {
                    var curPC = artist.SkipCounts.Where(x => x.Username == User.Identity.Name).FirstOrDefault();
                    if (curPC != null)
                    {
                        artist.SkipCounts[artist.SkipCounts.IndexOf(curPC)].Value++;
                    }
                    else
                    {
                        artist.SkipCounts.Add(new UserStat() { Username = User.Identity.Name, Value = 1 });
                    }
                }
                artists.Add(artist);
                ArtistCollection.ReplaceOne(artistFilter, artist);
            }

            // Update track
            if (track.SkipCounts == null)
            {
                track.SkipCounts = new List<UserStat>() { new UserStat() { Username = User.Identity.Name, Value = 1 } };
            }
            else
            {
                var curTC = track.SkipCounts.Where(x => x.Username == User.Identity.Name).FirstOrDefault();
                if (curTC != null)
                {
                    track.SkipCounts[track.SkipCounts.IndexOf(curTC)].Value++;
                }
                else
                {
                    track.SkipCounts.Add(new UserStat() { Username = User.Identity.Name, Value = 1 });
                }
            }

            // Update album
            if (album.SkipCounts == null)
            {
                album.SkipCounts = new List<UserStat>() { new UserStat() { Username = User.Identity.Name, Value = 1 } };
            }
            else
            {
                var curAC = album.SkipCounts.Where(x => x.Username == User.Identity.Name).FirstOrDefault();
                if (curAC != null)
                {
                    album.SkipCounts[album.SkipCounts.IndexOf(curAC)].Value++;
                }
                else
                {
                    album.SkipCounts.Add(new UserStat() { Username = User.Identity.Name, Value = 1 });
                }
            }
            TCollection.ReplaceOne(tFilter, track);
            AlbumCollection.ReplaceOne(albumFilter, album);

            // Add Play Stat
            PlayStat stat = new PlayStat();
            stat._id = new MelonId(ObjectId.GenerateNewId());
            stat.StatId = stat._id.ToString();
            stat.TrackId = track.TrackId;
            stat.AlbumId = album.AlbumId;
            stat.Duration = track.Duration;
            stat.ArtistIds =
            [
                .. from a in artists
                   select a.ArtistId,
            ];
            stat.Device = device;
            stat.User = User.Identity.Name;
            if (dateTime != "")
            {
                try
                {
                    stat.LogDate = DateTime.Parse(dateTime).ToUniversalTime();
                }
                catch (Exception)
                {
                    return new ObjectResult("Invalid Datetime") { StatusCode = 400 };
                }
            }
            else
            {
                stat.LogDate = DateTime.Now.ToUniversalTime();
            }
            stat.Genres = new List<string>();
            foreach (var genre in track.TrackGenres)
            {
                stat.Genres.Add(genre);
            }

            StatsCollection.InsertOne(stat);

            return new ObjectResult("Skip Logged") { StatusCode = 200 };
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("listening-time")]
        public ObjectResult ListeningTime(string user, string ltDateTime = "", string gtDateTime = "", int page = 0, int count = 100)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var uFilter = Builders<User>.Filter.Eq(x => x.Username, user);
            var users = UsersCollection.Find(uFilter).ToList();

            var statFilter = Builders<PlayStat>.Filter.Empty;
            if (users.Count() == 0)
            {
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            if(user != User.Identity.Name)
            {
                if (!users[0].PublicStats)
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.User, user);
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.User, user);
            }


            if (ltDateTime != "")
            {
                DateTime ltdt = DateTime.Parse(ltDateTime);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }

            if (gtDateTime != "")
            {
                DateTime gtdt = DateTime.Parse(gtDateTime);
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
            }

            var stats = StatsCollection.Find(statFilter).ToList();

            double total = 0;
            foreach(var stat in stats)
            {
                total += Convert.ToDouble(stat.Duration);
            }

            return new ObjectResult(total) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("top-tracks")]
        public ObjectResult TopTracks(string user, string ltDateTime = "", string gtDateTime = "", string device = "", int page = 0, int count = 100)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var uFilter = Builders<User>.Filter.Eq(x => x.Username, user);
            var users = UsersCollection.Find(uFilter).ToList();

            var statFilter = Builders<PlayStat>.Filter.Regex(x => x.Device, new BsonRegularExpression(device,"i"));
            if (users.Count() == 0)
            {
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            if(user != User.Identity.Name)
            {
                if (!users[0].PublicStats)
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.User, user);
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.User, user);
            }


            if (ltDateTime != "")
            {
                DateTime ltdt = DateTime.Parse(ltDateTime);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }

            if (gtDateTime != "")
            {
                DateTime gtdt = DateTime.Parse(gtDateTime);
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
            }

            var stats = StatsCollection.Find(statFilter).ToList();

            var tracks = stats.GroupBy(stat => stat.TrackId)
                              .Select(group => new { Name = group.Key, Count = group.Count() })
                              .OrderByDescending(x => x.Count)
                              .Take(new Range(page * count, (page * count) + count))
                              .ToDictionary(g => g.Name, g => g.Count);

            return new ObjectResult(tracks) { StatusCode = 200 };
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("top-albums")]
        public ObjectResult TopAlbums(string user, string ltDateTime = "", string gtDateTime = "", string device = "", int page = 0, int count = 100)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var uFilter = Builders<User>.Filter.Eq(x => x.Username, user);
            var users = UsersCollection.Find(uFilter).ToList();

            var statFilter = Builders<PlayStat>.Filter.Regex(x => x.Device, new BsonRegularExpression(device, "i"));
            if (users.Count() == 0)
            {
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            if (user != User.Identity.Name)
            {
                if (!users[0].PublicStats)
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.User, user);
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.User, user);
            }

            if (ltDateTime != "")
            {
                DateTime ltdt = DateTime.Parse(ltDateTime);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }

            if (gtDateTime != "")
            {
                DateTime gtdt = DateTime.Parse(gtDateTime);
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
            }

            var stats = StatsCollection.Find(statFilter).ToList();

            var albums = stats.GroupBy(stat => stat.AlbumId)
                              .Select(group => new { Name = group.Key, Count = group.Count() })
                              .OrderByDescending(x => x.Count)
                              .Take(new Range(page * count, (page * count) + count))
                              .ToDictionary(g => g.Name, g => g.Count);

            return new ObjectResult(albums) { StatusCode = 200 };
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("top-artists")]
        public ObjectResult TopArtists(string user, string ltDateTime = "", string gtDateTime = "", string device = "", int page = 0, int count = 100)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var uFilter = Builders<User>.Filter.Eq(x => x.Username, user);
            var users = UsersCollection.Find(uFilter).ToList();

            var statFilter = Builders<PlayStat>.Filter.Regex(x => x.Device, new BsonRegularExpression(device, "i"));
            if (users.Count() == 0)
            {
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            if (user != User.Identity.Name)
            {
                if (!users[0].PublicStats)
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.User, user);
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.User, user);
            }

            if (ltDateTime != "")
            {
                DateTime ltdt = DateTime.Parse(ltDateTime);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }

            if (gtDateTime != "")
            {
                DateTime gtdt = DateTime.Parse(gtDateTime);
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
            }

            var stats = StatsCollection.Find(statFilter).ToList();

            var artists = stats.SelectMany(obj => obj.ArtistIds)
                               .GroupBy(x => x)
                               .Select(group => new { Name = group.Key, Count = group.Count() })
                               .OrderByDescending(x => x.Count)
                               .Take(new Range(page * count, (page * count) + count))
                               .ToDictionary(g => g.Name, g => g.Count);

            return new ObjectResult(artists) { StatusCode = 200 };
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("top-genres")]
        public ObjectResult TopGenres(string user, string ltDateTime = "", string gtDateTime = "", string device = "", int page = 0, int count = 100)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var uFilter = Builders<User>.Filter.Eq(x => x.Username, user);
            var users = UsersCollection.Find(uFilter).ToList();

            var statFilter = Builders<PlayStat>.Filter.Regex(x => x.Device, new BsonRegularExpression(device, "i"));
            if (users.Count() == 0)
            {
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            if (user != User.Identity.Name)
            {
                if (!users[0].PublicStats)
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.User, user);
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.User, user);
            }

            if (ltDateTime != "")
            {
                DateTime ltdt = DateTime.Parse(ltDateTime);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }

            if (gtDateTime != "")
            {
                DateTime gtdt = DateTime.Parse(gtDateTime);
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
            }

            var stats = StatsCollection.Find(statFilter).ToList();

            var genres = stats.SelectMany(obj => obj.Genres)
                               .GroupBy(x => x)
                               .Select(group => new { Name = group.Key, Count = group.Count() })
                               .OrderByDescending(x => x.Count)
                               .Take(new Range(page * count, (page * count) + count))
                               .ToDictionary(g => g.Name, g => g.Count);

            return new ObjectResult(genres) { StatusCode = 404 };
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("recent-tracks")]
        public ObjectResult RecentTracks(string user, int page = 0, int count = 25)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var uFilter = Builders<User>.Filter.Eq(x => x.Username, user);
            var users = UsersCollection.Find(uFilter).ToList();

            var statFilter = Builders<PlayStat>.Filter.Empty;
            if (users.Count() == 0)
            {
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            if (user != User.Identity.Name)
            {
                if (!users[0].PublicStats)
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.User, user);
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.User, user);
            }

            var recentStats = StatsCollection.Find(statFilter)
                                             .Sort(Builders<PlayStat>.Sort.Descending(x=>x.LogDate))
                                             .Skip(page*count)
                                             .Limit(count)
                                             .ToList();

            var tracks = recentStats.Select(stat => new { stat.TrackId, stat.LogDate });

            return new ObjectResult(tracks) { StatusCode = 200 };
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("recent-albums")]
        public ObjectResult RecentAlbums(string user, int page = 0, int count = 25)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var uFilter = Builders<User>.Filter.Eq(x => x.Username, user);
            var users = UsersCollection.Find(uFilter).ToList();

            var statFilter = Builders<PlayStat>.Filter.Empty;
            if (users.Count() == 0)
            {
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            if (user != User.Identity.Name)
            {
                if (!users[0].PublicStats)
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.User, user);
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.User, user);
            }

            var recentStats = StatsCollection.Find(statFilter)
                                             .Sort(Builders<PlayStat>.Sort.Descending(x => x.LogDate))
                                             .Skip(page * count)
                                             .Limit(count)
                                             .ToList();

            var albums = recentStats.Select(stat => new { stat.AlbumId, stat.LogDate });


            return new ObjectResult(albums) { StatusCode = 200 };
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("recent-artists")]
        public ObjectResult RecentArtists(string user, int page = 0, int count = 25)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var uFilter = Builders<User>.Filter.Eq(x => x.Username, user);
            var users = UsersCollection.Find(uFilter).ToList();

            var statFilter = Builders<PlayStat>.Filter.Empty;
            if (users.Count() == 0)
            {
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            if (user != User.Identity.Name)
            {
                if (!users[0].PublicStats)
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.User, user);
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.User, user);
            }

            var recentStats = StatsCollection.Find(statFilter)
                                             .Sort(Builders<PlayStat>.Sort.Descending(x => x.LogDate))
                                             .Skip(page * count)
                                             .Limit(count)
                                             .ToList();

            var albums = recentStats.Select(stat => new { stat.ArtistIds, stat.LogDate });


            return new ObjectResult(albums) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("rate-track")]
        public ObjectResult RateTrack(string id, long rating)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            // Get track
            Track track = null;
            var tFilter = Builders<Track>.Filter.Eq("TrackId", id);
            track = TCollection.Find(tFilter).FirstOrDefault();

            if (track == null)
            {
                return new ObjectResult("Track Not Found") { StatusCode = 404 };
            }

            // Update track
            if (track.Ratings == null)
            {
                track.Ratings = new List<UserStat>() { new UserStat() { Username = User.Identity.Name, Value = rating } };
            }
            else
            {
                var curRating = track.Ratings.Where(x => x.Username == User.Identity.Name).FirstOrDefault();
                if (curRating != null)
                {
                    track.Ratings[track.Ratings.IndexOf(curRating)].Value = rating;
                }
                else
                {
                    track.Ratings.Add(new UserStat() { Username = User.Identity.Name, Value = rating });
                }
            }

            TCollection.ReplaceOne(tFilter, track);

            return new ObjectResult("Track Rated") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("rate-album")]
        public ObjectResult RateAlbum(string id, long rating)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ACollection = mongoDatabase.GetCollection<Album>("Albums");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            // Get track
            Album album = null;
            var aFilter = Builders<Album>.Filter.Eq("AlbumId", id);
            album = ACollection.Find(aFilter).FirstOrDefault();

            if (album == null)
            {
                return new ObjectResult("Album Not Found") { StatusCode = 404 };
            }

            // Update track
            if (album.Ratings == null)
            {
                album.Ratings = new List<UserStat>() { new UserStat() { Username = User.Identity.Name, Value = rating } };
            }
            else
            {
                var curRating = album.Ratings.Where(x => x.Username == User.Identity.Name).FirstOrDefault();
                if (curRating != null)
                {
                    album.Ratings[album.Ratings.IndexOf(curRating)].Value = rating;
                }
                else
                {
                    album.Ratings.Add(new UserStat() { Username = User.Identity.Name, Value = rating });
                }
            }

            ACollection.ReplaceOne(aFilter, album);

            return new ObjectResult("Album Rated") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("rate-artist")]
        public ObjectResult RateArtist(string id, long rating)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ACollection = mongoDatabase.GetCollection<Artist>("Artists");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            // Get track
            Artist artist = null;
            var aFilter = Builders<Artist>.Filter.Eq("ArtistId", id);
            artist = ACollection.Find(aFilter).FirstOrDefault();

            if (artist == null)
            {
                return new ObjectResult("Artist Not Found") { StatusCode = 404 };
            }

            // Update track
            if (artist.Ratings == null)
            {
                artist.Ratings = new List<UserStat>() { new UserStat() { Username = User.Identity.Name, Value = rating } };
            }
            else
            {
                var curRating = artist.Ratings.Where(x => x.Username == User.Identity.Name).FirstOrDefault();
                if (curRating != null)
                {
                    artist.Ratings[artist.Ratings.IndexOf(curRating)].Value = rating;
                }
                else
                {
                    artist.Ratings.Add(new UserStat() { Username = User.Identity.Name, Value = rating });
                }
            }

            ACollection.ReplaceOne(aFilter, artist);

            return new ObjectResult("Artist Rated") { StatusCode = 200 };
        }
    }
}
