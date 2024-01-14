using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;
using System.Data;
using MongoDB.Driver;
using Melon.LocalClasses;
using System.Diagnostics;
using Melon.Models;
using System.Security.Cryptography;

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

        [Authorize(Roles = "Admin,User")]
        [HttpPost("create")]
        public ObjectResult CreateQueueFromIDs(string name, [FromQuery] List<string> ids, string shuffle = "none")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var queueDoc = new BsonDocument();
            PlayQueue queue = new PlayQueue();
            var userName = User.Identity.Name;
            queue._id = new MelonId(ObjectId.GenerateNewId());
            queue.QueueId = queue._id.ToString();
            queue.Name = name;
            queue.Owner = userName;
            queue.PublicViewing = false;
            queue.PublicEditing = false;
            queue.Editors = new List<string>();
            queue.Viewers = new List<string>();
            queue.Tracks = new List<ShortTrack>();

            var qFilter = Builders<PlayQueue>.Filter.Eq(x => x.QueueId, queue.QueueId);
            foreach(var id in ids)
            {
                var trackFilter = Builders<Track>.Filter.Eq(x=>x.TrackId, id);
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
            queue.OriginalTrackOrder = (from track in queue.Tracks
                                        select track.TrackId).ToList();
            QCollection.InsertOne(queue);

            return new ObjectResult($"{queue.QueueId}") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("create-from-albums")]
        public ObjectResult CreateQueueAlbums(string name, [FromQuery] List<string> ids, string shuffle = "none")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var ACollection = mongoDatabase.GetCollection<Album>("Albums");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var queueDoc = new BsonDocument();
            PlayQueue queue = new PlayQueue();
            var userName = User.Identity.Name;
            queue._id = new MelonId(ObjectId.GenerateNewId());
            queue.QueueId = queue._id.ToString();
            queue.Name = name;
            queue.Owner = userName;
            queue.PublicViewing = false;
            queue.PublicEditing = false;
            queue.Editors = new List<string>();
            queue.Viewers = new List<string>();
            queue.Tracks = new List<ShortTrack>();


            var qFilter = Builders<PlayQueue>.Filter.Eq(x=>x.QueueId, queue.QueueId);
            List<ShortTrack> tracks = new List<ShortTrack>();
            foreach (var id in ids)
            {
                var aFilter = Builders<Album>.Filter.Eq(x=>x.AlbumId, id);
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
            queue.OriginalTrackOrder = (from track in queue.Tracks
                                        select track.TrackId).ToList();
            QCollection.InsertOne(queue);

            return new ObjectResult($"{queue.QueueId}") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("create-from-artists")]
        public ObjectResult CreateQueueArtists(string name, [FromQuery] List<string> ids, string shuffle = "none")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var ACollection = mongoDatabase.GetCollection<Artist>("Artists");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var queueDoc = new BsonDocument();
            PlayQueue queue = new PlayQueue();
            var userName = User.Identity.Name;
            queue._id = new MelonId(ObjectId.GenerateNewId());
            queue.QueueId = queue._id.ToString();
            queue.Name = name;
            queue.Owner = userName;
            queue.PublicViewing = false;
            queue.PublicEditing = false;
            queue.Editors = new List<string>();
            queue.Viewers = new List<string>();
            queue.Tracks = new List<ShortTrack>();


            var qFilter = Builders<PlayQueue>.Filter.Eq(x=>x.QueueId, queue.QueueId);
            List<ShortTrack> tracks = new List<ShortTrack>();
            foreach (var id in ids)
            {
                var aFilter = Builders<Artist>.Filter.Eq(x=>x.ArtistId, id);
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
            queue.OriginalTrackOrder = (from track in queue.Tracks
                                        select track.TrackId).ToList();
            QCollection.InsertOne(queue);

            return new ObjectResult($"{queue.QueueId}") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("create-from-playlists")]
        public ObjectResult CreateQueuePlaylists(string name, [FromQuery] List<string> ids, string shuffle = "none")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var queueDoc = new BsonDocument();
            PlayQueue queue = new PlayQueue();
            var userName = User.Identity.Name;
            queue._id = new MelonId(ObjectId.GenerateNewId());
            queue.QueueId = queue._id.ToString();
            queue.Name = name;
            queue.Owner = userName;
            queue.PublicViewing = false;
            queue.PublicEditing = false;
            queue.Editors = new List<string>();
            queue.Viewers = new List<string>();
            queue.Tracks = new List<ShortTrack>();


            var qFilter = Builders<PlayQueue>.Filter.Eq(x=>x.QueueId, queue.QueueId);
            List<ShortTrack> tracks = new List<ShortTrack>();
            foreach (var id in ids)
            {
                var pFilter = Builders<Playlist>.Filter.Eq(x=>x.PlaylistId, id);
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
            queue.OriginalTrackOrder = (from track in queue.Tracks
                                        select track.TrackId).ToList();
            QCollection.InsertOne(queue);

            return new ObjectResult($"{queue.QueueId}") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("get")]
        public ObjectResult GetQueueFromIDs(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var userName = User.Identity.Name;

            var qFilter = Builders<PlayQueue>.Filter.Eq(x=>x.QueueId, id);
            var qDoc = QCollection.Find(qFilter).ToList();

            if (qDoc.Count > 0)
            {
                var queue = qDoc[0];
                if (queue.PublicEditing == false)
                {
                    if (queue.Owner != userName && !queue.Editors.Contains(userName) && !queue.Viewers.Contains(userName))
                    {
                        return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                    }
                }

                return new ObjectResult(new ShortQueue(queue)) { StatusCode = 200 };
            }

            return new ObjectResult($"Queue not found") { StatusCode = 404 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("search")]
        public ObjectResult SearchQueues(int page, int count)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var user = User.Identity.Name;

            var ownerFilter = Builders<PlayQueue>.Filter.Eq(x => x.Owner, user);
            var EditorsFilter = Builders<PlayQueue>.Filter.AnyEq(x => x.Editors, user);
            var viewersFilter = Builders<PlayQueue>.Filter.AnyEq(x => x.Viewers, user);
            var publicViewingFilter = Builders<PlayQueue>.Filter.Eq(x => x.PublicViewing, true);

            // Combine filters with OR
            var combinedFilter = Builders<PlayQueue>.Filter.Or(ownerFilter, viewersFilter, publicViewingFilter, EditorsFilter);

            var Queues = QCollection.Find(combinedFilter)
                                    .Skip(page * count)
                                    .Limit(count)
                                    .ToList()
                                    .Select(x=>new ShortQueue(x));

            return new ObjectResult(Queues) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("get-tracks")]
        public ObjectResult GetTracks(int page, int count, string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var qFilter = Builders<PlayQueue>.Filter.Eq(x => x.QueueId, id);

            var Queues = QCollection.Find(qFilter).ToList();
            if (Queues.Count() == 0)
            {
                return new ObjectResult("Queue not found") { StatusCode = 404 };
            }
            var queue = Queues[0];

            var userName = User.Identity.Name;
            if (queue.PublicEditing == false)
            {
                if (queue.Owner != userName && !queue.Editors.Contains(userName) && !queue.Viewers.Contains(userName))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            var tracks = Queues[0].Tracks.Take(new Range(page * count, (page * count) + count));

            return new ObjectResult(tracks) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("add-tracks")]
        public ObjectResult AddToQueue(string id, [FromQuery] List<string> trackIds, string position = "end")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var userName = User.Identity.Name;

            var qFilter = Builders<PlayQueue>.Filter.Eq(x => x.QueueId, id);
            var queues = QCollection.Find(qFilter).ToList();
            if(queues.Count() == 0)
            {
                return new ObjectResult("Queue not found") { StatusCode = 404 };
            }
            var queue = queues[0];

            if (queue.PublicEditing == false)
            {
                if (queue.Owner != userName && !queue.Editors.Contains(userName))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            foreach (var tid in trackIds)
            {
                var trackFilter = Builders<Track>.Filter.Eq(x=>x.TrackId, tid);
                var track = TCollection.Find(trackFilter).ToList()[0];
                if (track != null)
                {
                    continue;
                }

                switch (position)
                {
                    case "end":
                        queue.Tracks.Add(new ShortTrack(track));
                        queue.OriginalTrackOrder.Add(track.TrackId);
                        break;
                    case "front":
                        queue.Tracks.Insert(0, new ShortTrack(track));
                        queue.OriginalTrackOrder.Insert(0, track.TrackId);
                        break;
                    case "random":
                        int randIdx = new Random().Next(0, queue.Tracks.Count());
                        queue.Tracks.Insert(randIdx, new ShortTrack(track));
                        queue.OriginalTrackOrder.Insert(randIdx, track.TrackId);
                        break;
                }
            }
            QCollection.ReplaceOne(qFilter, queue);

            StreamManager.AlertQueueUpdate(queue.QueueId);

            return new ObjectResult("Tracks added") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("remove-tracks")]
        public ObjectResult RemoveFromQueue(string id, [FromQuery] List<string> trackIds)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var userName = User.Identity.Name;

            var qFilter = Builders<PlayQueue>.Filter.Eq(x => x.QueueId, id);
            var queues = QCollection.Find(qFilter).ToList();
            if (queues.Count() == 0)
            {
                return new ObjectResult("Queue not found") { StatusCode = 404 };
            }
            var queue = queues[0];
            if (queue.PublicEditing == false)
            {
                if (queue.Owner != userName && !queue.Editors.Contains(userName))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            foreach (var tid in trackIds)
            {
                var query = from track in queue.Tracks
                            where track.TrackId == tid
                            select track;
                if (query.Count() != 0)
                {
                    queue.Tracks.Remove(query.ToList()[0]);
                    queue.OriginalTrackOrder.Remove(query.ToList()[0].TrackId);
                }
            }
            QCollection.ReplaceOne(qFilter, queue);

            StreamManager.AlertQueueUpdate(queue.QueueId);

            return new ObjectResult("Tracks removed") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("move-track")]
        public ObjectResult MoveTrack(string queueId, string trackId, int position)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var userName = User.Identity.Name;

            var qFilter = Builders<PlayQueue>.Filter.Eq(x=>x.QueueId, queueId);
            var queues = QCollection.Find(qFilter).ToList();
            if (queues.Count() == 0)
            {
                return new ObjectResult("Queue not found") { StatusCode = 404 };
            }
            var queue = queues[0];

            if (queue.PublicEditing == false)
            {
                if (queue.Owner != userName && !queue.Editors.Contains(userName))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            var tracks = (from t in queue.Tracks
                         where t.TrackId == trackId
                         select t).ToList();
            if(tracks.Count() == 0)
            {
                return new ObjectResult("Track not found") { StatusCode = 404 };
            }
            var track = tracks[0];

            int curIdx = queue.Tracks.IndexOf(track);
            queue.Tracks.Insert(position, track);
            queue.Tracks.RemoveAt(curIdx);

            QCollection.ReplaceOne(qFilter, queue);

            StreamManager.AlertQueueUpdate(queue.QueueId);

            return new ObjectResult("Track moved") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("update-position")]
        public ObjectResult UpdateQueuePosition(string id, int pos)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var userName = User.Identity.Name;

            var qFilter = Builders<PlayQueue>.Filter.Eq(x=>x.QueueId, id);
            var queues = QCollection.Find(qFilter).ToList();
            if (queues.Count == 0)
            {
                return new ObjectResult("Queue not found") { StatusCode = 404 };
            }
            var oq = queues[0];

            if (oq.PublicEditing == false)
            {
                if (oq.Owner != userName && !oq.Editors.Contains(userName))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            oq.CurPosition = pos;

            QCollection.ReplaceOne(qFilter, oq);

            StreamManager.AlertQueueUpdate(id);

            return new ObjectResult("Queue updated") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("update")]
        public ObjectResult UpdateQueue(ShortQueue queue)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var userName = User.Identity.Name;

            var qFilter = Builders<PlayQueue>.Filter.Eq(x=>x.QueueId, queue.QueueId);
            var queues = QCollection.Find(qFilter).ToList();
            if (queues.Count == 0)
            {
                return new ObjectResult("Queue not found") { StatusCode = 404 };
            }
            var oq = queues[0];

            if (oq.PublicEditing == false)
            {
                if (oq.Owner != userName && !oq.Editors.Contains(userName))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            oq._id = queue._id;
            oq.QueueId = queue.QueueId;
            oq.CurPosition = queue.CurPosition;
            oq.Name = queue.Name;
            oq.Owner = queue.Owner;
            oq.Editors = queue.Editors;
            oq.Viewers = queue.Viewers;
            oq.PublicEditing = queue.PublicEditing;
            oq.PublicViewing = queue.PublicViewing;


            QCollection.ReplaceOne(qFilter, oq);

            StreamManager.AlertQueueUpdate(queue.QueueId);

            return new ObjectResult("Queue updated") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("shuffle")]
        public ObjectResult ShuffleQueue(string id, string shuffle = "None")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var userName = User.Identity.Name;

            var qFilter = Builders<PlayQueue>.Filter.Eq(x => x.QueueId, id);
            var queues = QCollection.Find(qFilter).ToList();
            if(queues.Count() == 0)
            {
                return new ObjectResult("Queue not found") { StatusCode = 404 };
            }
            var queue = queues[0];

            if (queue.PublicEditing == false)
            {
                if (queue.Owner != userName && !queue.Editors.Contains(userName))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            List<ShortTrack> tracks = new List<ShortTrack>(queue.Tracks);

            switch (shuffle)
            {
                case "None":
                    tracks.Clear();
                    foreach(var tid in queue.OriginalTrackOrder)
                    {
                        var track = (from t in queue.Tracks
                                     where t.TrackId == tid
                                     select t).FirstOrDefault();

                        if (track != null)
                        {
                            tracks.Add(track);
                        }
                    }
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

            StreamManager.AlertQueueUpdate(queue.QueueId);

            return new ObjectResult("Tracks Shuffled") { StatusCode = 200 };
        }
    }
}