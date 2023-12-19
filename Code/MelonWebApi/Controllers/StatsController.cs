using Melon.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using SharpCompress.Common;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing;
using Melon.LocalClasses;

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

        [HttpPost("log-play")]
        public string LogPlay(string _id, string device = "", string user = "", string dateTime = "")
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
            stat.TrackId = track.TrackId; 
            stat.AlbumId = album.AlbumId;
            stat.ArtistIds =
            [
                .. from a in artists
                   select a.ArtistId,
            ];
            stat.Device = device;
            stat.User = user;
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

            StatsCollection.InsertOne(stat);

            return "200";
        }
    }
}
