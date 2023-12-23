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
        public string LogPlay(string _id, string device = "", string dateTime = "")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");
            var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");

            // Get track, album, artists
            Track track = null;
            var tFilter = Builders<Track>.Filter.Eq("_id", ObjectId.Parse(_id));
            try
            {
                track = TCollection.Find(tFilter).ToList()[0];
            }
            catch(Exception ex)
            {
                return "Track Not Found";
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
                    return "Invalid DateTime";
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

            return "200";
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("top-tracks")]
        public Dictionary<string, int> TopTracks(string ltDateTime = "", string gtDateTime = "", string device = "", string user = "", int page = 0, int count = 500)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");

            var statFilter = Builders<PlayStat>.Filter.Regex(x => x.Device, new BsonRegularExpression(device,"i"));
            statFilter = Builders<PlayStat>.Filter.Regex(x => x.User, new BsonRegularExpression(user,"i"));

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

            return tracks;
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("top-albums")]
        public Dictionary<string, int> TopAlbums(string ltDateTime = "", string gtDateTime = "", string device = "", string user = "", int page = 0, int count = 500)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");

            var statFilter = Builders<PlayStat>.Filter.Regex(x => x.Device, new BsonRegularExpression(device, "i"));
            statFilter = Builders<PlayStat>.Filter.Regex(x => x.User, new BsonRegularExpression(user, "i"));

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

            return albums;
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("top-artists")]
        public Dictionary<string, int> TopArtists(string ltDateTime = "", string gtDateTime = "", string device = "", string user = "", int page = 0, int count = 500)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");

            var statFilter = Builders<PlayStat>.Filter.Regex(x => x.Device, new BsonRegularExpression(device, "i"));
            statFilter = Builders<PlayStat>.Filter.Regex(x => x.User, new BsonRegularExpression(user, "i"));

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

            return artists;
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("top-genres")]
        public Dictionary<string, int> TopGenres(string ltDateTime = "", string gtDateTime = "", string device = "", string user = "", int page = 0, int count = 500)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");

            var statFilter = Builders<PlayStat>.Filter.Regex(x => x.Device, new BsonRegularExpression(device, "i"));
            statFilter = Builders<PlayStat>.Filter.Regex(x => x.User, new BsonRegularExpression(user, "i"));

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

            return genres;
        }
    }
}
