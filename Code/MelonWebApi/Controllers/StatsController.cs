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
            var tFilter = Builders<Track>.Filter.Eq("_id", ObjectId.Parse(id));
            try
            {
                track = TCollection.Find(tFilter).ToList()[0];
            }
            catch(Exception ex)
            {
                return new ObjectResult("Track not found") { StatusCode = 404 };
            }

            var albumFilter = Builders<Album>.Filter.Eq("_id", track.Album._id);
            var album = AlbumCollection.Find(albumFilter).ToList()[0];

            List<Artist> artists = new List<Artist>();
            foreach (var a in track.TrackArtists)
            {
                var artistFilter = Builders<Artist>.Filter.Eq("_id", a._id);
                var artist = ArtistCollection.Find(artistFilter).ToList()[0];
                artist.PlayCount++;
                artists.Add(artist);
                ArtistCollection.ReplaceOne(artistFilter, artist);
            }

            // Update track, album, artist
            track.PlayCount++;
            album.PlayCount++;
            TCollection.ReplaceOne(tFilter, track);
            AlbumCollection.ReplaceOne(albumFilter, album);

            // Add Play Stat
            PlayStat stat = new PlayStat();
            stat._id = ObjectId.GenerateNewId();
            stat.StatId = stat._id.ToString();
            stat.TrackId = track.TrackId; 
            stat.AlbumId = album.AlbumId;
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

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("top-tracks")]
        public ObjectResult TopTracks(string user, string ltDateTime = "", string gtDateTime = "", string device = "", int page = 0, int count = 100)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var uFilter = Builders<User>.Filter.Regex(x => x.Username, new BsonRegularExpression(user,"i"));
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
                statFilter = Builders<PlayStat>.Filter.Regex(x => x.User, new BsonRegularExpression(user,"i"));
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Regex(x => x.User, new BsonRegularExpression(user, "i"));
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

            var uFilter = Builders<User>.Filter.Regex(x => x.Username, new BsonRegularExpression(user, "i"));
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
                statFilter = Builders<PlayStat>.Filter.Regex(x => x.User, new BsonRegularExpression(user, "i"));
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Regex(x => x.User, new BsonRegularExpression(user, "i"));
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

            var uFilter = Builders<User>.Filter.Regex(x => x.Username, new BsonRegularExpression(user, "i"));
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
                statFilter = Builders<PlayStat>.Filter.Regex(x => x.User, new BsonRegularExpression(user, "i"));
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Regex(x => x.User, new BsonRegularExpression(user, "i"));
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

            var uFilter = Builders<User>.Filter.Regex(x => x.Username, new BsonRegularExpression(user, "i"));
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
                statFilter = Builders<PlayStat>.Filter.Regex(x => x.User, new BsonRegularExpression(user, "i"));
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Regex(x => x.User, new BsonRegularExpression(user, "i"));
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

            var uFilter = Builders<User>.Filter.Regex(x => x.Username, new BsonRegularExpression(user, "i"));
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
                statFilter = Builders<PlayStat>.Filter.Regex(x => x.User, new BsonRegularExpression(user, "i"));
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Regex(x => x.User, new BsonRegularExpression(user, "i"));
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

            var uFilter = Builders<User>.Filter.Regex(x => x.Username, new BsonRegularExpression(user, "i"));
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
                statFilter = Builders<PlayStat>.Filter.Regex(x => x.User, new BsonRegularExpression(user, "i"));
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Regex(x => x.User, new BsonRegularExpression(user, "i"));
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

            var uFilter = Builders<User>.Filter.Regex(x => x.Username, new BsonRegularExpression(user, "i"));
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
                statFilter = Builders<PlayStat>.Filter.Regex(x => x.User, new BsonRegularExpression(user, "i"));
            }
            else
            {
                statFilter = Builders<PlayStat>.Filter.Regex(x => x.User, new BsonRegularExpression(user, "i"));
            }

            var recentStats = StatsCollection.Find(statFilter)
                                             .Sort(Builders<PlayStat>.Sort.Descending(x => x.LogDate))
                                             .Skip(page * count)
                                             .Limit(count)
                                             .ToList();

            var albums = recentStats.Select(stat => new { stat.ArtistIds, stat.LogDate });


            return new ObjectResult(albums) { StatusCode = 200 };
        }
    }
}
