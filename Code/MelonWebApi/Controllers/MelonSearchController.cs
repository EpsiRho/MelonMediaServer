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
    [Route("api/[controller]")]
    public class MelonSearchController : ControllerBase
    {
        private readonly ILogger<MelonSearchController> _logger;

        public MelonSearchController(ILogger<MelonSearchController> logger)
        {
            _logger = logger;
        }

        //[Authorize(Roles = "Admin, User")]
        [HttpGet("tracks")]
        public IEnumerable<Track> GetTracks(string query, int page, int count)
        {
            var mongoClient = new MongoClient("mongodb://localhost:27017");

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var trackFilter = Builders<Track>.Filter.Regex("TrackName", new BsonRegularExpression(query, "i"));
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
        [HttpGet("albums")]
        public IEnumerable<Album> GetAlbums(string query, int page, int count)
        {
            var mongoClient = new MongoClient("mongodb://localhost:27017");

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");

            var albumFilter = Builders<Album>.Filter.Regex("AlbumName", new BsonRegularExpression(query, "i"));

            var albumDocs = AlbumsCollection.Find(albumFilter)
                                            .Skip(page * count)
                                            .Limit(count)
                                            .ToList();

            var newlist = new List<Album>();
            foreach (var doc in albumDocs.ToList())
            {
                newlist.Add(doc);
            }
            return newlist;
        }
        [HttpGet("artists")]
        public IEnumerable<Artist> GetArtists(string query, int page, int count)
        {
            var mongoClient = new MongoClient("mongodb://localhost:27017");

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");

            var ArtistFilter = Builders<Artist>.Filter.Regex("ArtistName", new BsonRegularExpression(query, "i"));

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