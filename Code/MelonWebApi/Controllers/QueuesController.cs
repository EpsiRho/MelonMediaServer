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
            queue.CurPosition = 0;

            var qFilter = Builders<PlayQueue>.Filter.Eq(x => x.QueueId, queue.QueueId);
            List<Track> tracks = new List<Track>();
            foreach(var id in ids)
            {
                var trackFilter = Builders<Track>.Filter.Eq(x=>x.TrackId, id);
                var trackDoc = TCollection.Find(trackFilter).ToList()[0];
                queue.Tracks.Add(new ShortTrack(trackDoc));
                tracks.Add(trackDoc);
            }

            switch (shuffle)
            {
                case "none":
                    break;
                case "TrackRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrack, false);
                    break;
                case "TrackFullRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrack, true);
                    break;
                case "Album":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByAlbum, false);
                    break;
                case "AlbumRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByAlbum, true);
                    break;
                case "ArtistRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByArtistRandom, true);
                    break;
                case "TrackFavorites":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrackFavorites, false);
                    break;
                case "TrackDiscovery":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrackDiscovery, false);
                    break;
            }
            
            queue.Tracks = tracks.Select(x=>new ShortTrack(x)).ToList();
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
            queue.CurPosition = 0;


            var qFilter = Builders<PlayQueue>.Filter.Eq(x=>x.QueueId, queue.QueueId);
            List<Track> tracks = new List<Track>();
            foreach (var id in ids)
            {
                var aFilter = Builders<Album>.Filter.Eq(x=>x.AlbumId, id);
                var album = ACollection.Find(aFilter).FirstOrDefault();
                if(album != null)
                {
                    var fFilter = Builders<Track>.Filter.In(a => a.TrackId, album.Tracks.Select(x => x.TrackId));
                    var fTracks = TCollection.Find(fFilter).ToList();
                    tracks.AddRange(fTracks);
                }
            }

            switch (shuffle)
            {
                case "none":
                    break;
                case "TrackRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrack, false);
                    break;
                case "TrackFullRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrack, true);
                    break;
                case "Album":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByAlbum, false);
                    break;
                case "AlbumRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByAlbum, true);
                    break;
                case "ArtistRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByArtistRandom, true);
                    break;
                case "TrackFavorites":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrackFavorites, false);
                    break;
                case "TrackDiscovery":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrackDiscovery, false);
                    break;
            }

            queue.Tracks = tracks.Select(x => new ShortTrack(x)).ToList();

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
            queue.CurPosition = 0;


            var qFilter = Builders<PlayQueue>.Filter.Eq(x=>x.QueueId, queue.QueueId);
            List<Track> tracks = new List<Track>();
            foreach (var id in ids)
            {
                var aFilter = Builders<Artist>.Filter.Eq(x=>x.ArtistId, id);
                var artist = ACollection.Find(aFilter).FirstOrDefault();
                if (artist != null)
                {
                    var fFilter = Builders<Track>.Filter.In(a => a.TrackId, artist.Tracks.Select(x => x.TrackId));
                    var fTracks = TCollection.Find(fFilter).ToList();
                    tracks.AddRange(fTracks);
                }
            }

            switch (shuffle)
            {
                case "none":
                    break;
                case "TrackRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrack, false);
                    break;
                case "TrackFullRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrack, true);
                    break;
                case "Album":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByAlbum, false);
                    break;
                case "AlbumRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByAlbum, true);
                    break;
                case "ArtistRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByArtistRandom, true);
                    break;
                case "TrackFavorites":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrackFavorites, false);
                    break;
                case "TrackDiscovery":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrackDiscovery, false);
                    break;
            }

            queue.Tracks = tracks.Select(x => new ShortTrack(x)).ToList();
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
            queue.CurPosition = 0;


            var qFilter = Builders<PlayQueue>.Filter.Eq(x=>x.QueueId, queue.QueueId);
            List<Track> tracks = new List<Track>();
            foreach (var id in ids)
            {
                var pFilter = Builders<Playlist>.Filter.Eq(x=>x.PlaylistId, id);
                var playlist = PCollection.Find(pFilter).FirstOrDefault();
                if (playlist != null)
                {
                    var fFilter = Builders<Track>.Filter.In(a => a.TrackId, playlist.Tracks.Select(x => x.TrackId));
                    var fTracks = TCollection.Find(fFilter).ToList();
                    tracks.AddRange(fTracks);
                }
            }

            switch (shuffle)
            {
                case "none":
                    break;
                case "TrackRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrack, false);
                    break;
                case "TrackFullRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrack, true);
                    break;
                case "Album":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByAlbum, false);
                    break;
                case "AlbumRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByAlbum, true);
                    break;
                case "ArtistRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByArtistRandom, true);
                    break;
                case "TrackFavorites":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrackFavorites, false);
                    break;
                case "TrackDiscovery":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrackDiscovery, false);
                    break;
            }

            queue.Tracks = tracks.Select(x => new ShortTrack(x)).ToList();
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
        public ObjectResult GetTracks(string id, int page = 0, int count = 100)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

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

            List<Track> fullTracks = TracksCollection.Find(Builders<Track>.Filter.In(x => x.TrackId, tracks.Select(x => x.TrackId))).ToList();

            var usernames = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x.Username));
            usernames.Add(User.Identity.Name);

            foreach (var track in fullTracks)
            {
                // Check for null or empty collections to avoid exceptions
                if (track.PlayCounts != null)
                {
                    track.PlayCounts = track.PlayCounts.Where(x => usernames.Contains(x.Username)).ToList();
                }

                if (track.SkipCounts != null)
                {
                    track.SkipCounts = track.SkipCounts.Where(x => usernames.Contains(x.Username)).ToList();
                }

                if (track.Ratings != null)
                {
                    track.Ratings = track.Ratings.Where(x => usernames.Contains(x.Username)).ToList();
                }
            }

            return new ObjectResult(fullTracks) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("add-tracks")]
        public ObjectResult AddToQueue(string id, [FromQuery] List<string> trackIds, string position = "end", int place = 0)
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
                if (track == null)
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
                        if(queue.CurPosition >= 0)
                        {
                            queue.CurPosition++;
                        }
                        queue.Tracks.Insert(0, new ShortTrack(track));
                        queue.OriginalTrackOrder.Insert(0, track.TrackId);
                        break;
                    case "random":
                        int randIdx = new Random().Next(0, queue.Tracks.Count());
                        if(queue.CurPosition >= randIdx)
                        {
                            queue.CurPosition++;
                        }
                        queue.Tracks.Insert(randIdx, new ShortTrack(track));
                        queue.OriginalTrackOrder.Insert(randIdx, track.TrackId);
                        break;
                    case "at":
                        if (queue.CurPosition >= place)
                        {
                            queue.CurPosition++;
                        }
                        queue.Tracks.Insert(place, new ShortTrack(track));
                        queue.OriginalTrackOrder.Insert(place, track.TrackId);
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
                var newTrack = query.FirstOrDefault();
                if (newTrack != null)
                {
                    int idx = queue.Tracks.IndexOf(newTrack);
                    if(queue.CurPosition == idx)
                    {
                        StreamManager.AlertQueueUpdate(id, "UPDATE CURRENT");
                    }
                    else if (queue.CurPosition > idx)
                    {
                        queue.CurPosition--;
                    }
                    queue.Tracks.Remove(newTrack);
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
            if(queue.CurPosition == curIdx)
            {
                StreamManager.AlertQueueUpdate(queueId, "UPDATE CURRENT");
            }
            else if(queue.CurPosition > curIdx && queue.CurPosition <= position)
            {
                queue.CurPosition--;
            }
            else if(queue.CurPosition > position && queue.CurPosition < curIdx)
            {
                queue.CurPosition++;
            }
            queue.Tracks.RemoveAt(curIdx);
            queue.Tracks.Insert(position, track);

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
        public ObjectResult UpdateQueue(string queueId, string name = "", [FromQuery] string[] editors = null, [FromQuery] string[] viewers = null, 
                                        string publicEditing = "", string publicViewing = "")
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
            var oq = queues.FirstOrDefault();

            if (oq.PublicEditing == false)
            {
                if (oq.Owner != userName && !oq.Editors.Contains(userName))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            oq.Name = name == "" ? oq.Name : name;
            try
            {
                oq.PublicEditing = publicEditing == "" ? oq.PublicEditing : Convert.ToBoolean(publicEditing);
                oq.PublicViewing = publicViewing == "" ? oq.PublicViewing : Convert.ToBoolean(publicViewing);
            }
            catch (Exception)
            {
                return new ObjectResult("Invalid Parameters") { StatusCode = 400 };
            }

            oq.Editors = editors == null ? oq.Editors : editors.ToList();
            oq.Viewers = viewers == null ? oq.Viewers : viewers.ToList();


            QCollection.ReplaceOne(qFilter, oq);

            StreamManager.AlertQueueUpdate(oq.QueueId);

            return new ObjectResult("Queue updated") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("shuffle")]
        public ObjectResult ShuffleQueue(string id, string shuffle = "None")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");

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


            List<Track> tracks = TCollection.Find(Builders<Track>.Filter.In(x => x.TrackId, queue.Tracks.Select(x => x.TrackId))).ToList();
            Track currentTrack = tracks.Where(x=>x.TrackId == queue.Tracks[queue.CurPosition].TrackId).FirstOrDefault();
            tracks.Remove(currentTrack);

            switch (shuffle)
            {
                case "None":
                    tracks.Clear();
                    queue.Tracks.Remove(queue.Tracks[queue.CurPosition]);
                    List<ShortTrack> sTracks = new List<ShortTrack>();
                    foreach (var tid in queue.OriginalTrackOrder)
                    {
                        var track = (from t in queue.Tracks
                                     where t.TrackId == tid
                                     select t).FirstOrDefault();

                        if (track != null)
                        {
                            sTracks.Add(track);
                        }
                    }
                    queue.Tracks.Clear();
                    queue.Tracks.Add(new ShortTrack(currentTrack));
                    queue.Tracks.AddRange(sTracks);
                    QCollection.ReplaceOne(qFilter, queue);
                    return new ObjectResult("Tracks Reset") { StatusCode = 200 };
                case "TrackRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrack, false);
                    break;
                case "TrackFullRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrack, true);
                    break;
                case "Album":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByAlbum, false);
                    break;
                case "AlbumRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByAlbum, true);
                    break;
                case "ArtistRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByArtistRandom, true);
                    break;
                case "TrackFavorites":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrackFavorites, false);
                    break;
                case "TrackDiscovery":
                    tracks = MelonAPI.ShuffleTracks(tracks, userName, Melon.Types.ShuffleType.ByTrackDiscovery, false);
                    break;
            }

            queue.Tracks.Clear();
            queue.Tracks.Add(new ShortTrack(currentTrack));
            queue.CurPosition = 0;
            queue.Tracks.AddRange(tracks.Select(x=> new ShortTrack(x)));
            QCollection.ReplaceOne(qFilter, queue);

            StreamManager.AlertQueueUpdate(queue.QueueId);

            return new ObjectResult("Tracks Shuffled") { StatusCode = 200 };
        }
    }
}