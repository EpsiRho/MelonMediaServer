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
            var mongoClient = new MongoClient("mongodb://localhost:27017");

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("PlayQueues");




            var queueDoc = new BsonDocument();
            PlayQueue queue = new PlayQueue();
            queue._id = new ObjectId();
            queue.Name = name;
            queue.Tracks = new List<Track>();
            QCollection.InsertOne(queue);
            //var str = queue._id.ToString();
            var qFilter = Builders<PlayQueue>.Filter.Eq("_id", queue._id);
            foreach(var id in _ids)
            {
                var trackFilter = Builders<Track>.Filter.Eq("_id", new ObjectId(id));
                var trackDoc = TCollection.Find(trackFilter).ToList();
                var arrayUpdateTracks = Builders<PlayQueue>.Update.Push("Tracks", trackDoc[0]);
                QCollection.UpdateOne(qFilter, arrayUpdateTracks);

            }

            return $"{queue._id}";
        }
        [HttpGet("getQueue")]
        public PlayQueue CreateQueueFromIDs(string _id)
        {
            var mongoClient = new MongoClient("mongodb://localhost:27017");

            var mongoDatabase = mongoClient.GetDatabase("Melon");
            
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("PlayQueues");

            var qFilter = Builders<PlayQueue>.Filter.Eq("_id", new ObjectId(_id));
            var qDoc = QCollection.Find(qFilter).ToList();

            return qDoc[0];
        }
    }
}