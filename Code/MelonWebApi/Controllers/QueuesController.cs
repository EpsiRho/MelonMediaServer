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
    [Route("api/queues")]
    public class QueuesController : ControllerBase
    {
        private readonly ILogger<QueuesController> _logger;

        public QueuesController(ILogger<QueuesController> logger)
        {
            _logger = logger;
        }

        [HttpPost("create")]
        public string CreateQueueFromIDs(string name, List<string> _ids, string shuffle = "none")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var queueDoc = new BsonDocument();
            PlayQueue queue = new PlayQueue();
            queue._id = ObjectId.GenerateNewId();
            queue.QueueId = queue._id.ToString();
            queue.Name = name;
            queue.Tracks = new List<ShortTrack>();

            var qFilter = Builders<PlayQueue>.Filter.Eq("_id", queue._id);
            foreach(var id in _ids)
            {
                var trackFilter = Builders<Track>.Filter.Eq("_id", new ObjectId(id));
                var trackDoc = TCollection.Find(trackFilter).ToList()[0];
                queue.Tracks.Add(new ShortTrack(trackDoc));
            }

            var tracks = queue.Tracks;
            switch (shuffle)
            {
                case "none":
                    break;
                case "TrackRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrack, false);
                    break;
                case "TrackFullRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrack, true);
                    break;
                case "Album":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByAlbum, false);
                    break;
                case "AlbumRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByAlbum, true);
                    break;
                case "ArtistRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByArtistRandom, true);
                    break;
                case "TrackFavorites":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrackFavorites, false);
                    break;
                case "TrackDiscovery":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrackDiscovery, false);
                    break;
            }
            queue.Tracks = tracks;
            QCollection.InsertOne(queue);

            return $"{queue._id}";
        }
        [HttpPost("create-from-albums")]
        public string CreateQueueAlbums(string name, List<string> _ids, string shuffle = "none")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var ACollection = mongoDatabase.GetCollection<Album>("Albums");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var queueDoc = new BsonDocument();
            PlayQueue queue = new PlayQueue();
            queue._id = ObjectId.GenerateNewId();
            queue.QueueId = queue._id.ToString();
            queue.Name = name;
            queue.Tracks = new List<ShortTrack>();


            var qFilter = Builders<PlayQueue>.Filter.Eq("_id", queue._id);
            List<ShortTrack> tracks = new List<ShortTrack>();
            foreach (var id in _ids)
            {
                var aFilter = Builders<Album>.Filter.Eq("_id", ObjectId.Parse(id));
                var album = ACollection.Find(aFilter).ToList();
                if(album.Count() != 0)
                {
                    tracks.AddRange(album[0].Tracks);
                }
            }

            switch (shuffle)
            {
                case "none":
                    break;
                case "TrackRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrack, false);
                    break;
                case "TrackFullRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrack, true);
                    break;
                case "Album":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByAlbum, false);
                    break;
                case "AlbumRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByAlbum, true);
                    break;
                case "ArtistRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByArtistRandom, true);
                    break;
                case "TrackFavorites":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrackFavorites, false);
                    break;
                case "TrackDiscovery":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrackDiscovery, false);
                    break;
            }

            foreach (var track in tracks)
            {
                queue.Tracks.Add(track);
            }
            QCollection.InsertOne(queue);

            return $"{queue._id}";
        }
        [HttpPost("create-from-artists")]
        public string CreateQueueArtists(string name, List<string> _ids, string shuffle = "none")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var ACollection = mongoDatabase.GetCollection<Artist>("Artists");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var queueDoc = new BsonDocument();
            PlayQueue queue = new PlayQueue();
            queue._id = ObjectId.GenerateNewId();
            queue.QueueId = queue._id.ToString();
            queue.Name = name;
            queue.Tracks = new List<ShortTrack>();


            var qFilter = Builders<PlayQueue>.Filter.Eq("_id", queue._id);
            List<ShortTrack> tracks = new List<ShortTrack>();
            foreach (var id in _ids)
            {
                var aFilter = Builders<Artist>.Filter.Eq("_id", ObjectId.Parse(id));
                var artist = ACollection.Find(aFilter).ToList();
                if (artist.Count() != 0)
                {
                    tracks.AddRange(artist[0].Tracks);
                }
            }

            switch (shuffle)
            {
                case "none":
                    break;
                case "TrackRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrack, false);
                    break;
                case "TrackFullRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrack, true);
                    break;
                case "Album":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByAlbum, false);
                    break;
                case "AlbumRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByAlbum, true);
                    break;
                case "ArtistRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByArtistRandom, true);
                    break;
                case "TrackFavorites":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrackFavorites, false);
                    break;
                case "TrackDiscovery":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrackDiscovery, false);
                    break;
            }

            foreach (var track in tracks)
            {
                queue.Tracks.Add(track);
            }
            QCollection.InsertOne(queue);

            return $"{queue._id}";
        }
        [HttpPost("create-from-playlists")]
        public string CreateQueuePlaylists(string name, List<string> _ids, string shuffle = "none")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var queueDoc = new BsonDocument();
            PlayQueue queue = new PlayQueue();
            queue._id = ObjectId.GenerateNewId();
            queue.QueueId = queue._id.ToString();
            queue.Name = name;
            queue.Tracks = new List<ShortTrack>();


            var qFilter = Builders<PlayQueue>.Filter.Eq("_id", queue._id);
            List<ShortTrack> tracks = new List<ShortTrack>();
            foreach (var id in _ids)
            {
                var pFilter = Builders<Playlist>.Filter.Eq("_id", ObjectId.Parse(id));
                var playlist = PCollection.Find(pFilter).ToList();
                if (playlist.Count() != 0)
                {
                    tracks.AddRange(playlist[0].Tracks);
                }
            }

            switch (shuffle)
            {
                case "none":
                    break;
                case "TrackRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrack, false);
                    break;
                case "TrackFullRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrack, true);
                    break;
                case "Album":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByAlbum, false);
                    break;
                case "AlbumRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByAlbum, true);
                    break;
                case "ArtistRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByArtistRandom, true);
                    break;
                case "TrackFavorites":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrackFavorites, false);
                    break;
                case "TrackDiscovery":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrackDiscovery, false);
                    break;
            }

            foreach (var track in tracks)
            {
                queue.Tracks.Add(track);
            }
            QCollection.InsertOne(queue);

            return $"{queue._id}";
        }
        [HttpGet("get")]
        public PlayQueue GetQueueFromIDs(string _id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var qFilter = Builders<PlayQueue>.Filter.Eq("_id", new ObjectId(_id));
            var qDoc = QCollection.Find(qFilter).ToList();

            if(qDoc.Count != 0)
            {
                return qDoc[0];
            }
            else
            {
                return null;
            }
        }
        [HttpGet("search")]
        public IEnumerable<PlayQueue> SearchQueues(int page, int count, string name)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var qFilter = Builders<PlayQueue>.Filter.Regex(x => x.Name, new BsonRegularExpression(name, "i"));

            var Queues = QCollection.Find(qFilter)
                                    .Skip(page * count)
                                    .Limit(count)
                                    .ToList();

            return Queues;
        }
        [HttpGet("get-tracks")]
        public IEnumerable<ShortTrack> GetTracks(int page, int count, string _id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var qFilter = Builders<PlayQueue>.Filter.Eq(x => x._id, ObjectId.Parse(_id));

            var Queues = QCollection.Find(qFilter).ToList();
            if (Queues.Count() == 0)
            {
                return null;
            }
            var tracks = Queues[0].Tracks.Take(new Range(page * count, (page * count) + count));

            return tracks;
        }
        [HttpPost("add-tracks")]
        public string AddToQueue(string _id, List<string> trackIds, string position = "end")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var qFilter = Builders<PlayQueue>.Filter.Eq("_id", ObjectId.Parse(_id));
            var queues = QCollection.Find(qFilter).ToList();
            if(queues.Count() == 0)
            {
                return "Queue Not Found";
            }
            var queue = queues[0];

            foreach (var id in trackIds)
            {
                var trackFilter = Builders<Track>.Filter.Eq("_id", new ObjectId(id));
                var track = TCollection.Find(trackFilter).ToList()[0];
                if (track != null)
                {
                    continue;
                }

                switch (position)
                {
                    case "end":
                        queue.Tracks.Add(new ShortTrack(track));
                        break;
                    case "front":
                        queue.Tracks.Insert(0, new ShortTrack(track));
                        break;
                    case "random":
                        int randIdx = new Random().Next(0, queue.Tracks.Count());
                        queue.Tracks.Insert(randIdx, new ShortTrack(track));
                        break;
                }
            }
            QCollection.ReplaceOne(qFilter, queue);

            return "200";
        }
        [HttpPost("remove-tracks")]
        public string RemoveFromQueue(string _id, List<string> trackIds)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var qFilter = Builders<PlayQueue>.Filter.Eq("_id", ObjectId.Parse(_id));
            var queues = QCollection.Find(qFilter).ToList();
            if (queues.Count() == 0)
            {
                return "Queue Not Found";
            }
            var queue = queues[0];

            foreach (var id in trackIds)
            {
                var query = from track in queue.Tracks
                            where track._id == new ObjectId(id)
                            select track;
                if (query.Count() != 0)
                {
                    queue.Tracks.Remove(query.ToList()[0]);
                }
            }
            QCollection.ReplaceOne(qFilter, queue);

            return "200";
        }
        [HttpPost("move-track")]
        public string MoveTrack(string queueId, string trackId, int position)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var qFilter = Builders<PlayQueue>.Filter.Eq("_id", ObjectId.Parse(queueId));
            var queues = QCollection.Find(qFilter).ToList();
            if (queues.Count() == 0)
            {
                return "Queue Not Found";
            }
            var queue = queues[0];

            var tracks = (from t in queue.Tracks
                         where t.TrackId == trackId
                         select t).ToList();
            if(tracks.Count() == 0)
            {
                return "Track Not Found";
            }
            var track = tracks[0];

            int curIdx = queue.Tracks.IndexOf(track);
            queue.Tracks.RemoveAt(curIdx);
            queue.Tracks.Insert(position, track);

            QCollection.ReplaceOne(qFilter, queue);

            return "200";
        }
        [HttpPost("update-name")]
        public string UpdateQueueName(string queueId, string name = "", int pos = -1)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var qFilter = Builders<PlayQueue>.Filter.Eq("_id", ObjectId.Parse(queueId));
            var queues = QCollection.Find(qFilter).ToList();
            if (queues.Count == 0)
            {
                return "Queue Not Found";
            }
            var queue = queues[0];

            if (name != "")
            {
                queue.Name = name;
            }

            if(pos != -1)
            {
                queue.CurPosition = pos;
            }


            QCollection.ReplaceOne(qFilter, queue);

            return "200";
        }
        [HttpPost("shuffle")]
        public string ShuffleQueue(string _id, string shuffle = "none")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var qFilter = Builders<PlayQueue>.Filter.Eq("_id", ObjectId.Parse(_id));
            var queues = QCollection.Find(qFilter).ToList();
            if(queues.Count() == 0)
            {
                return "Not Found";
            }
            var queue = queues[0];

            List<ShortTrack> tracks = queue.Tracks;

            switch (shuffle)
            {
                case "none":
                    break;
                case "TrackRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrack, false);
                    break;
                case "TrackFullRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrack, true);
                    break;
                case "Album":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByAlbum, false);
                    break;
                case "AlbumRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByAlbum, true);
                    break;
                case "ArtistRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByArtistRandom, true);
                    break;
                case "TrackFavorites":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrackFavorites, false);
                    break;
                case "TrackDiscovery":
                    tracks = MelonAPI.ShuffleTracks(tracks, Melon.Types.ShuffleType.ByTrackDiscovery, false);
                    break;
            }

            queue.Tracks.Clear();
            foreach (var track in tracks)
            {
                queue.Tracks.Add(track);
            }
            QCollection.ReplaceOne(qFilter, queue);

            return $"200";
        }
    }
}