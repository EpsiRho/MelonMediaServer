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
        public string CreatePlaylist(string name, string description = "", string artworkPath = "", List<string> trackIds = null)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");
            
            
            Playlist playlist = new Playlist();
            playlist._id = new ObjectId();
            playlist.Name = name;
            playlist.Description = description;
            playlist.ArtworkPath = artworkPath;
            playlist.Tracks = new List<ShortTrack>();
            PCollection.InsertOne(playlist);
            //var str = queue._id.ToString();
            var pFilter = Builders<Playlist>.Filter.Eq("_id", playlist._id);
            if(trackIds == null)
            {
                return playlist._id.ToString();
            }
            foreach(var id in trackIds)
            {
                var trackFilter = Builders<Track>.Filter.Eq("_id", new ObjectId(id));
                var trackDoc = TCollection.Find(trackFilter).ToList();
                var arrayUpdateTracks = Builders<Playlist>.Update.Push("Tracks", new ShortTrack(trackDoc[0]));
                PCollection.UpdateOne(pFilter, arrayUpdateTracks);
            
            }
            
            return playlist._id.ToString();
        }
        [HttpPost("addToPlaylist")]
        public string AddToPlaylist(string _id, List<string> trackIds)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");


            //var str = queue._id.ToString();
            var pFilter = Builders<Playlist>.Filter.Eq("_id", ObjectId.Parse(_id));
            foreach (var id in trackIds)
            {
                var trackFilter = Builders<Track>.Filter.Eq("_id", new ObjectId(id));
                var trackDoc = TCollection.Find(trackFilter).ToList();
                var arrayUpdateTracks = Builders<Playlist>.Update.Push("Tracks", new ShortTrack(trackDoc[0]));
                var res = PCollection.UpdateOne(pFilter, arrayUpdateTracks);

            }

            return "200";
        }
        [HttpPost("removeFromPlaylist")]
        public string RemoveFromPlaylist(string _id, List<string> trackIds)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");


            //var str = queue._id.ToString();
            var pFilter = Builders<Playlist>.Filter.Eq("_id", ObjectId.Parse(_id));
            var playlist = PCollection.Find(pFilter).ToList()[0];
            foreach (var id in trackIds)
            {
                var query = from track in playlist.Tracks
                            where track._id == new ObjectId(id)
                            select track;
                playlist.Tracks.Remove(query.ToList()[0]);
            }
            PCollection.ReplaceOne(pFilter, playlist);

            return "200";
        }
        [HttpGet("getPlaylists")]
        public List<Playlist> GetPlaylists()
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");
            
            var pCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var pDoc = pCollection.Find(Builders<Playlist>.Filter.Empty).ToList();

            return pDoc;
        }
        [HttpGet("getPlaylistById")]
        public Playlist GetPlaylistById(string _id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var pCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var pFilter = Builders<Playlist>.Filter.Eq("_id", new ObjectId(_id));
            var pDoc = pCollection.Find(pFilter).ToList();

            return pDoc[0];
        }
    }
}