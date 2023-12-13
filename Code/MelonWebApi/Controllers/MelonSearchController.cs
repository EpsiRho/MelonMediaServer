using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;
using System.Data;
using MongoDB.Driver;
using Melon.LocalClasses;
using System.Diagnostics;
using Melon.Models;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/Library")]
    public class LibraryController : ControllerBase
    {
        private readonly ILogger<LibraryController> _logger;

        public LibraryController(ILogger<LibraryController> logger)
        {
            _logger = logger;
        }

        //[Authorize(Roles = "Admin, User")]
        [HttpGet("searchTracks")]
        public IEnumerable<Track> SearchTracks(int page, int count, string trackName = "", string format = "", string bitrate = "", 
            string sampleRate = "", string channels = "", string bitsPerSample = "", string year = "", string[] genres = null)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var trackFilter = Builders<Track>.Filter.Regex("TrackName", new BsonRegularExpression(trackName, "i"));
                trackFilter = trackFilter & Builders<Track>.Filter.Regex("Format", new BsonRegularExpression(format, "i"));
                trackFilter = trackFilter & Builders<Track>.Filter.Regex("Bitrate", new BsonRegularExpression(bitrate, "i"));
                trackFilter = trackFilter & Builders<Track>.Filter.Regex("SampleRate", new BsonRegularExpression(sampleRate, "i"));
                trackFilter = trackFilter & Builders<Track>.Filter.Regex("Channels", new BsonRegularExpression(channels, "i"));
                trackFilter = trackFilter & Builders<Track>.Filter.Regex("BitsPerSample", new BsonRegularExpression(bitsPerSample, "i"));
                trackFilter = trackFilter & Builders<Track>.Filter.Regex("Year", new BsonRegularExpression(year, "i"));
            if (genres != null)
            {
                foreach (var genre in genres)
                {
                    trackFilter = trackFilter & Builders<Track>.Filter.Regex("TrackGenres", new BsonRegularExpression(genre, "i"));
                }
            }
            //trackFilter = trackFilter & Builders<BsonDocument>.Filter.Eq("AlbumName", fileMetadata.Tag.Album);
            //trackFilter = trackFilter & Builders<BsonDocument>.Filter.Eq("TrackName", fileMetadata.Tag.Title);

            var trackDocs = TracksCollection.Find(trackFilter)
                                            .Skip(page * count)
                                            .Limit(count)
                                            .ToList();

            var newlist = new List<Track>();
            foreach (var doc in trackDocs.ToList())
            {
                //Track t = BsonSerializer.Deserialize<Track>(doc);
                newlist.Add(doc);
            }
            return newlist;
        }
        [HttpGet("searchAlbums")]
        public IEnumerable<Track> SearchAlbums (int page, int count, string albumName = "", string publisher = "", string releaseType = "", string[] genres = null)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TracksCollection = mongoDatabase.GetCollection<Track>("Albums");

            var albumFilter = Builders<Track>.Filter.Regex("AlbumName", new BsonRegularExpression(albumName, "i"));
            albumFilter = albumFilter & Builders<Track>.Filter.Regex("Publisher", new BsonRegularExpression(publisher, "i"));
            albumFilter = albumFilter & Builders<Track>.Filter.Regex("ReleaseType", new BsonRegularExpression(releaseType, "i"));
            if (genres != null)
            {
                foreach (var genre in genres)
                {
                    albumFilter = albumFilter & Builders<Track>.Filter.Regex("AlbumGenres", new BsonRegularExpression(genre, "i"));
                }
            }
            //trackFilter = trackFilter & Builders<BsonDocument>.Filter.Eq("AlbumName", fileMetadata.Tag.Album);
            //trackFilter = trackFilter & Builders<BsonDocument>.Filter.Eq("TrackName", fileMetadata.Tag.Title);

            var trackDocs = TracksCollection.Find(albumFilter)
                                            .Skip(page * count)
                                            .Limit(count)
                                            .ToList();

            var newlist = new List<Track>();
            foreach (var doc in trackDocs.ToList())
            {
                //Track t = BsonSerializer.Deserialize<Track>(doc);
                newlist.Add(doc);
            }
            return newlist;
        }
        [HttpGet("artists")]
        public IEnumerable<Artist> GetArtists(string ArtistName, int page, int count)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");

            var ArtistFilter = Builders<Artist>.Filter.Regex("ArtistName", new BsonRegularExpression(ArtistName, "i"));

            var ArtistDocs = ArtistCollection.Find(ArtistFilter)
                                            .Skip(page * count)
                                            .Limit(count)
                                            .ToList();

            var newlist = new List<Artist>();
            foreach (var doc in ArtistDocs.ToList())
            {
                newlist.Add(doc);
            }
            return newlist;
        }

    }
}