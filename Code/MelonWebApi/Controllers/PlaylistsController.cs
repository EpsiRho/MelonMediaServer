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
    public class PlaylistsController : ControllerBase
    {
        private readonly ILogger<PlaylistsController> _logger;

        public PlaylistsController(ILogger<PlaylistsController> logger)
        {
            _logger = logger;
        }

        [HttpPost("createPlaylist")]
        public string CreatePlaylist(List<string> _ids, string name)
        {
            //var mongoClient = new MongoClient("mongodb://localhost:27017");
            //
            //var mongoDatabase = mongoClient.GetDatabase("Melon");
            //
            //var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            //var QCollection = mongoDatabase.GetCollection<PlayQueue>("PlayQueues");
            //
            //
            //
            //
            //var queueDoc = new BsonDocument();
            //PlayQueue queue = new PlayQueue();
            //queue._id = new ObjectId();
            //queue.Name = name;
            //queue.Tracks = new List<Track>();
            //QCollection.InsertOne(queue);
            ////var str = queue._id.ToString();
            //var qFilter = Builders<PlayQueue>.Filter.Eq("_id", queue._id);
            //foreach(var id in _ids)
            //{
            //    var trackFilter = Builders<Track>.Filter.Eq("_id", new ObjectId(id));
            //    var trackDoc = TCollection.Find(trackFilter).ToList();
            //    var arrayUpdateTracks = Builders<PlayQueue>.Update.Push("Tracks", trackDoc[0]);
            //    QCollection.UpdateOne(qFilter, arrayUpdateTracks);
            //
            //}
            //
            //return $"{queue._id}";
            return null;
        }
        [HttpGet("getPlaylists")]
        public List<Playlist> GetPlaylists()
        {
            var mongoClient = new MongoClient("mongodb://localhost:27017");

            var mongoDatabase = mongoClient.GetDatabase("Melon");
            
            var pCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var pDoc = pCollection.Find(Builders<Playlist>.Filter.Empty).ToList();

            return pDoc;
        }
        [HttpGet("getPlaylistById")]
        public Playlist GetPlaylistById(string _id)
        {
            var mongoClient = new MongoClient("mongodb://localhost:27017");

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var pCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var pFilter = Builders<Playlist>.Filter.Eq("_id", new ObjectId(_id));
            var pDoc = pCollection.Find(pFilter).ToList();

            return pDoc[0];
        }
    }
}