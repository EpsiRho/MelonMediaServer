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
using System.Security.Claims;

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
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x.Username, User.Identity.Name);
            var user = UserCollection.Find(userFilter).FirstOrDefault();

            // Get track, album, artists
            Track track = null;
            var tFilter = Builders<Track>.Filter.Eq(x=>x._id, id);
            track = TCollection.Find(tFilter).FirstOrDefault();

            if(track == null)
            {
                return new ObjectResult("Track Not Found") { StatusCode = 404 };
            }

            var albumFilter = Builders<Album>.Filter.Eq(x=>x._id, track.Album._id);
            var album = AlbumCollection.Find(albumFilter).ToList()[0];

            // Update artists
            List<Artist> artists = new List<Artist>();
            foreach (var a in track.TrackArtists)
            {
                var artistFilter = Builders<Artist>.Filter.Eq(x=>x._id, a._id);
                var artist = ArtistCollection.Find(artistFilter).ToList()[0];
                if (artist.PlayCounts == null)
                {
                    artist.PlayCounts = new List<UserStat>() { new UserStat() { UserId = user._id, Value = 1 } };
                }
                else 
                { 
                    var curPC = artist.PlayCounts.Where(x => x.UserId == user._id).FirstOrDefault();
                    if (curPC != null)
                    {
                        artist.PlayCounts[artist.PlayCounts.IndexOf(curPC)].Value++;
                    }
                    else
                    {
                        artist.PlayCounts.Add(new UserStat() { UserId = user._id, Value = 1 });
                    }
                }
                artists.Add(artist);
                ArtistCollection.ReplaceOne(artistFilter, artist);
            }

            // Update track
            if (track.PlayCounts == null)
            {
                track.PlayCounts = new List<UserStat>() { new UserStat() { UserId = user._id, Value = 1 } };
            }
            else
            {
                var curTC = track.PlayCounts.Where(x => x.UserId == user._id).FirstOrDefault();
                if (curTC != null)
                {
                    track.PlayCounts[track.PlayCounts.IndexOf(curTC)].Value++;
                }
                else
                {
                    track.PlayCounts.Add(new UserStat() { UserId = user._id, Value = 1 });
                }
            }
            TCollection.ReplaceOne(tFilter, track);

            // Update album
            if (album.PlayCounts == null)
            {
                album.PlayCounts = new List<UserStat>() { new UserStat() { UserId = user._id, Value = 1 } };
            }
            else
            {
                var curAC = album.PlayCounts.Where(x => x.UserId == user._id).FirstOrDefault();
                if (curAC != null)
                {
                    album.PlayCounts[album.PlayCounts.IndexOf(curAC)].Value++;
                }
                else
                {
                    album.PlayCounts.Add(new UserStat() { UserId = user._id, Value = 1 });
                }
            }
            AlbumCollection.ReplaceOne(albumFilter, album);

            // Add Play Stat
            PlayStat stat = new PlayStat();
            stat._id = ObjectId.GenerateNewId().ToString();
            stat.TrackId = track._id; 
            stat.AlbumId = album._id;
            stat.Duration = track.Duration;
            stat.Type = "Play";
            stat.ArtistIds =
            [
                .. from a in artists
                   select a._id,
            ];
            stat.Device = device;
            stat.UserId = user._id;
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
            stat.Genres.AddRange(track.TrackGenres);

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
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x.Username, User.Identity.Name);
            var user = UserCollection.Find(userFilter).FirstOrDefault();

            // Get track, album, artists
            Track track = null;
            var tFilter = Builders<Track>.Filter.Eq(x=>x._id, id);
            track = TCollection.Find(tFilter).FirstOrDefault();

            if (track == null)
            {
                return new ObjectResult("Track Not Found") { StatusCode = 404 };
            }

            var albumFilter = Builders<Album>.Filter.Eq(x => x._id, track.Album._id);
            var album = AlbumCollection.Find(albumFilter).ToList()[0];

            // Update artists
            List<Artist> artists = new List<Artist>();
            foreach (var a in track.TrackArtists)
            {
                var artistFilter = Builders<Artist>.Filter.Eq(x => x._id, a._id);
                var artist = ArtistCollection.Find(artistFilter).ToList()[0];
                if (artist.SkipCounts == null)
                {
                    artist.SkipCounts = new List<UserStat>() { new UserStat() { UserId = user._id, Value = 1 } };
                }
                else
                {
                    var curPC = artist.SkipCounts.Where(x => x.UserId == user._id).FirstOrDefault();
                    if (curPC != null)
                    {
                        artist.SkipCounts[artist.SkipCounts.IndexOf(curPC)].Value++;
                    }
                    else
                    {
                        artist.SkipCounts.Add(new UserStat() { UserId = user._id, Value = 1 });
                    }
                }
                artists.Add(artist);
                ArtistCollection.ReplaceOne(artistFilter, artist);
            }

            // Update track
            if (track.SkipCounts == null)
            {
                track.SkipCounts = new List<UserStat>() { new UserStat() { UserId = user._id, Value = 1 } };
            }
            else
            {
                var curTC = track.SkipCounts.Where(x => x.UserId == user._id).FirstOrDefault();
                if (curTC != null)
                {
                    track.SkipCounts[track.SkipCounts.IndexOf(curTC)].Value++;
                }
                else
                {
                    track.SkipCounts.Add(new UserStat() { UserId = user._id, Value = 1 });
                }
            }

            // Update album
            if (album.SkipCounts == null)
            {
                album.SkipCounts = new List<UserStat>() { new UserStat() { UserId = user._id, Value = 1 } };
            }
            else
            {
                var curAC = album.SkipCounts.Where(x => x.UserId == user._id).FirstOrDefault();
                if (curAC != null)
                {
                    album.SkipCounts[album.SkipCounts.IndexOf(curAC)].Value++;
                }
                else
                {
                    album.SkipCounts.Add(new UserStat() { UserId = user._id, Value = 1 });
                }
            }
            TCollection.ReplaceOne(tFilter, track);
            AlbumCollection.ReplaceOne(albumFilter, album);

            // Add Play Stat
            PlayStat stat = new PlayStat();
            stat._id = ObjectId.GenerateNewId().ToString();
            stat.TrackId = track._id;
            stat.AlbumId = album._id;
            stat.Duration = track.Duration;
            stat.Type = "Skip";
            stat.ArtistIds =
            [
                .. from a in artists
                   select a._id,
            ];
            stat.Device = device;
            stat.UserId = user._id;
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
        public ObjectResult ListeningTime(string userId = "", string ltDateTime = "", string gtDateTime = "", int page = 0, int count = 100)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            userId = userId == "" ? curId : userId;

            var uFilter = Builders<User>.Filter.Eq(x => x._id, userId);
            var user = UsersCollection.Find(uFilter).FirstOrDefault();

            if (user == null)
            {
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            var statFilter = Builders<PlayStat>.Filter.Empty;
            if(userId != curId)
            {
                if (!user.PublicStats)
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.UserId, user._id);
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.UserId, user._id);
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

            statFilter = statFilter & Builders<PlayStat>.Filter.Eq(x => x.Type, "Play");
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
        public ObjectResult TopTracks(string userId = "", string ltDateTime = "", string gtDateTime = "", string device = "", int page = 0, int count = 100)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            userId = userId == "" ? curId : userId;

            var uFilter = Builders<User>.Filter.Eq(x => x._id, userId);
            var user = UsersCollection.Find(uFilter).FirstOrDefault();

            if (user == null)
            {
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            var statFilter = Builders<PlayStat>.Filter.Regex(x => x.Device, new BsonRegularExpression(device,"i"));
            if(userId != curId)
            {
                if (!user.PublicStats)
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);
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
        public ObjectResult TopAlbums(string userId = "", string ltDateTime = "", string gtDateTime = "", string device = "", int page = 0, int count = 100)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            userId = userId == "" ? curId : userId;

            var uFilter = Builders<User>.Filter.Eq(x => x._id, userId);
            var user = UsersCollection.Find(uFilter).FirstOrDefault();

            var statFilter = Builders<PlayStat>.Filter.Regex(x => x.Device, new BsonRegularExpression(device, "i"));
            if (user == null)
            {
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            if (userId != curId)
            {
                if (!user.PublicStats)
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);
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
        public ObjectResult TopArtists(string userId = "", string ltDateTime = "", string gtDateTime = "", string device = "", int page = 0, int count = 100)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            userId = userId == "" ? curId : userId;

            var uFilter = Builders<User>.Filter.Eq(x => x._id, userId);
            var user = UsersCollection.Find(uFilter).FirstOrDefault();

            var statFilter = Builders<PlayStat>.Filter.Regex(x => x.Device, new BsonRegularExpression(device, "i"));
            if (user == null)
            {
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            if (userId != curId)
            {
                if (!user.PublicStats)
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);
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
        public ObjectResult TopGenres(string userId = "", string ltDateTime = "", string gtDateTime = "", string device = "", int page = 0, int count = 100)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            userId = userId == "" ? curId : userId;

            var uFilter = Builders<User>.Filter.Eq(x => x._id, userId);
            var user = UsersCollection.Find(uFilter).FirstOrDefault();

            var statFilter = Builders<PlayStat>.Filter.Regex(x => x.Device, new BsonRegularExpression(device, "i"));
            if (user == null)
            {
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            if (userId != curId)
            {
                if (!user.PublicStats)
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);
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
        public ObjectResult RecentTracks(string userId = "", int page = 0, int count = 25)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            userId = userId == "" ? curId : userId;

            var uFilter = Builders<User>.Filter.Eq(x => x._id, userId);
            var user = UsersCollection.Find(uFilter).FirstOrDefault();

            var statFilter = Builders<PlayStat>.Filter.Empty;
            if (user == null)
            {
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            if (userId != curId)
            {
                if (!user.PublicStats)
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);
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
        public ObjectResult RecentAlbums(string userId = "", int page = 0, int count = 25)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            userId = userId == "" ? curId : userId;

            var uFilter = Builders<User>.Filter.Eq(x => x._id, userId);
            var user = UsersCollection.Find(uFilter).FirstOrDefault();

            var statFilter = Builders<PlayStat>.Filter.Empty;
            if (user == null)
            {
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            if (userId != curId)
            {
                if (!user.PublicStats)
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);
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
        public ObjectResult RecentArtists(string userId = "", int page = 0, int count = 25)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            userId = userId == "" ? curId : userId;

            var uFilter = Builders<User>.Filter.Eq(x => x._id, userId);
            var user = UsersCollection.Find(uFilter).FirstOrDefault();

            var statFilter = Builders<PlayStat>.Filter.Empty;
            if (user == null)
            {
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            if (userId != curId)
            {
                if (!user.PublicStats)
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);
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
            var tFilter = Builders<Track>.Filter.Eq(x=>x._id, id);
            track = TCollection.Find(tFilter).FirstOrDefault();

            if (track == null)
            {
                return new ObjectResult("Track Not Found") { StatusCode = 404 };
            }

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            // Update track
            if (track.Ratings == null)
            {
                track.Ratings = new List<UserStat>() { new UserStat() { UserId = curId, Value = rating } };
            }
            else
            {
                var curRating = track.Ratings.Where(x => x.UserId == curId).FirstOrDefault();
                if (curRating != null)
                {
                    track.Ratings[track.Ratings.IndexOf(curRating)].Value = rating;
                }
                else
                {
                    track.Ratings.Add(new UserStat() { UserId = curId, Value = rating });
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
            var aFilter = Builders<Album>.Filter.Eq(x => x._id, id);
            album = ACollection.Find(aFilter).FirstOrDefault();

            if (album == null)
            {
                return new ObjectResult("Album Not Found") { StatusCode = 404 };
            }

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            // Update track
            if (album.Ratings == null)
            {
                album.Ratings = new List<UserStat>() { new UserStat() { UserId = curId, Value = rating } };
            }
            else
            {
                var curRating = album.Ratings.Where(x => x.UserId == curId).FirstOrDefault();
                if (curRating != null)
                {
                    album.Ratings[album.Ratings.IndexOf(curRating)].Value = rating;
                }
                else
                {
                    album.Ratings.Add(new UserStat() { UserId = curId, Value = rating });
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
            var aFilter = Builders<Artist>.Filter.Eq(x => x._id, id);
            artist = ACollection.Find(aFilter).FirstOrDefault();

            if (artist == null)
            {
                return new ObjectResult("Artist Not Found") { StatusCode = 404 };
            }

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            // Update track
            if (artist.Ratings == null)
            {
                artist.Ratings = new List<UserStat>() { new UserStat() { UserId = curId, Value = rating } };
            }
            else
            {
                var curRating = artist.Ratings.Where(x => x.UserId == curId).FirstOrDefault();
                if (curRating != null)
                {
                    artist.Ratings[artist.Ratings.IndexOf(curRating)].Value = rating;
                }
                else
                {
                    artist.Ratings.Add(new UserStat() { UserId = curId, Value = rating });
                }
            }

            ACollection.ReplaceOne(aFilter, artist);

            return new ObjectResult("Artist Rated") { StatusCode = 200 };
        }
    }
}
