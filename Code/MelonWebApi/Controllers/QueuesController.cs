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
using System.Web.Http.Filters;
using System.Security.Claims;

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
        public ObjectResult CreateQueueFromIDs(string name, [FromQuery] List<string> ids, string shuffle = "none", bool enableTrackLinks = true)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            PlayQueue queue = new PlayQueue();
            queue._id = ObjectId.GenerateNewId().ToString();
            queue.Name = name;
            queue.Owner = curId;
            queue.PublicViewing = false;
            queue.PublicEditing = false;
            queue.Editors = new List<string>();
            queue.Viewers = new List<string>();
            queue.Tracks = new List<DbLink>();
            queue.CurPosition = 0;

            var qFilter = Builders<PlayQueue>.Filter.Eq(x => x._id, queue._id);

            List<Track> fullTracks = TCollection.Find(Builders<Track>.Filter.In(x => x._id, ids)).ToList();
            if(fullTracks.Count() == 0)
            {
                return new ObjectResult("No tracks found.") { StatusCode = 404 };
            }


            List<Track> tracks = new List<Track>();
            foreach (var t in ids)
            {
                Track track = fullTracks.Where(x => x._id == t).FirstOrDefault();
                tracks.Add(track);
            }

            switch (shuffle)
            {
                case "none":
                    break;
                case "TrackRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrack, false, enableTrackLinks);
                    break;
                case "TrackFullRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrack, true, enableTrackLinks);
                    break;
                case "Album":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByAlbum, false, enableTrackLinks);
                    break;
                case "AlbumRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByAlbum, true, enableTrackLinks);
                    break;
                case "ArtistRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByArtistRandom, true, enableTrackLinks);
                    break;
                case "TrackFavorites":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrackFavorites, false, enableTrackLinks);
                    break;
                case "TrackDiscovery":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrackDiscovery, false, enableTrackLinks);
                    break;
            }
            
            queue.Tracks = tracks.Select(x=>new DbLink(x)).ToList();
            queue.TrackCount = tracks.Count;
            queue.OriginalTrackOrder = (from track in queue.Tracks
                                        select track._id).ToList();
            QCollection.InsertOne(queue);

            return new ObjectResult(queue._id) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("create-from-albums")]
        public ObjectResult CreateQueueAlbums(string name, [FromQuery] List<string> ids, string shuffle = "none", bool enableTrackLinks = true)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var ACollection = mongoDatabase.GetCollection<Album>("Albums");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            PlayQueue queue = new PlayQueue();
            queue._id = ObjectId.GenerateNewId().ToString();
            queue.Name = name;
            queue.Owner = curId;
            queue.PublicViewing = false;
            queue.PublicEditing = false;
            queue.Editors = new List<string>();
            queue.Viewers = new List<string>();
            queue.Tracks = new List<DbLink>();
            queue.CurPosition = 0;

            var qFilter = Builders<PlayQueue>.Filter.Eq(x => x._id, queue._id);
            List<Track> tracks = new List<Track>();
            foreach (var id in ids)
            {
                var aFilter = Builders<Album>.Filter.Eq(x=>x._id, id);
                var album = ACollection.Find(aFilter).FirstOrDefault();
                if(album != null)
                {
                    var fFilter = Builders<Track>.Filter.In(a => a._id, album.Tracks.Select(x => x._id));
                    var fTracks = TCollection.Find(fFilter).ToList();
                    foreach (var t in album.Tracks)
                    {
                        Track track = fTracks.Where(x => x._id == t._id).FirstOrDefault();
                        tracks.Add(track);
                    }
                }
                else
                {
                    return new ObjectResult("One or more Albums not found.") { StatusCode = 404 };
                }
            }

            switch (shuffle)
            {
                case "none":
                    break;
                case "TrackRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrack, false, enableTrackLinks);
                    break;
                case "TrackFullRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrack, true, enableTrackLinks);
                    break;
                case "Album":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByAlbum, false, enableTrackLinks);
                    break;
                case "AlbumRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByAlbum, true, enableTrackLinks);
                    break;
                case "ArtistRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByArtistRandom, true, enableTrackLinks);
                    break;
                case "TrackFavorites":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrackFavorites, false, enableTrackLinks);
                    break;
                case "TrackDiscovery":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrackDiscovery, false, enableTrackLinks);
                    break;
            }

            queue.Tracks = tracks.Select(x => new DbLink(x)).ToList();
            queue.TrackCount = tracks.Count;
            queue.OriginalTrackOrder = (from track in queue.Tracks
                                        select track._id).ToList();
            QCollection.InsertOne(queue);

            return new ObjectResult(queue._id) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("create-from-artists")]
        public ObjectResult CreateQueueArtists(string name, [FromQuery] List<string> ids, string shuffle = "none", bool enableTrackLinks = true)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var ACollection = mongoDatabase.GetCollection<Artist>("Artists");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            PlayQueue queue = new PlayQueue();
            queue._id = ObjectId.GenerateNewId().ToString();
            queue.Name = name;
            queue.Owner = curId;
            queue.PublicViewing = false;
            queue.PublicEditing = false;
            queue.Editors = new List<string>();
            queue.Viewers = new List<string>();
            queue.Tracks = new List<DbLink>();
            queue.CurPosition = 0;


            var qFilter = Builders<PlayQueue>.Filter.Eq(x => x._id, queue._id);
            List<Track> tracks = new List<Track>();
            foreach (var id in ids)
            {
                var aFilter = Builders<Artist>.Filter.Eq(x=>x._id, id);
                var artist = ACollection.Find(aFilter).FirstOrDefault();
                if (artist != null)
                {
                    var fFilter = Builders<Track>.Filter.In(a => a._id, artist.Tracks.Select(x => x._id));
                    var fTracks = TCollection.Find(fFilter).ToList();
                    foreach (var t in artist.Tracks)
                    {
                        Track track = fTracks.Where(x => x._id == t._id).FirstOrDefault();
                        tracks.Add(track);
                    }
                }
                else
                {
                    return new ObjectResult("One or more Artists not found.") { StatusCode = 404 };
                }
            }

            switch (shuffle)
            {
                case "none":
                    break;
                case "TrackRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrack, false, enableTrackLinks);
                    break;
                case "TrackFullRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrack, true, enableTrackLinks);
                    break;
                case "Album":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByAlbum, false, enableTrackLinks);
                    break;
                case "AlbumRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByAlbum, true, enableTrackLinks);
                    break;
                case "ArtistRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByArtistRandom, true, enableTrackLinks);
                    break;
                case "TrackFavorites":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrackFavorites, false, enableTrackLinks);
                    break;
                case "TrackDiscovery":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrackDiscovery, false, enableTrackLinks);
                    break;
            }

            queue.Tracks = tracks.Select(x => new DbLink(x)).ToList();
            queue.TrackCount = tracks.Count;
            queue.OriginalTrackOrder = (from track in queue.Tracks
                                        select track._id).ToList();
            QCollection.InsertOne(queue);

            return new ObjectResult(queue._id) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("create-from-playlists")]
        public ObjectResult CreateQueuePlaylists(string name, [FromQuery] List<string> ids, string shuffle = "none", bool enableTrackLinks = true)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            PlayQueue queue = new PlayQueue();
            queue._id = ObjectId.GenerateNewId().ToString();
            queue.Name = name;
            queue.Owner = curId;
            queue.PublicViewing = false;
            queue.PublicEditing = false;
            queue.Editors = new List<string>();
            queue.Viewers = new List<string>();
            queue.Tracks = new List<DbLink>();
            queue.CurPosition = 0;


            var qFilter = Builders<PlayQueue>.Filter.Eq(x => x._id, queue._id);
            List<Track> tracks = new List<Track>();
            foreach (var id in ids)
            {
                var pFilter = Builders<Playlist>.Filter.Eq(x=>x._id, id);
                var playlist = PCollection.Find(pFilter).FirstOrDefault();
                if (playlist != null)
                {
                    var fFilter = Builders<Track>.Filter.In(a => a._id, playlist.Tracks.Select(x => x._id));
                    var fTracks = TCollection.Find(fFilter).ToList();
                    foreach(var t in playlist.Tracks)
                    {
                        Track track = fTracks.Where(x => x._id == t._id).FirstOrDefault();
                        tracks.Add(track);
                    }
                }
                else
                {
                    return new ObjectResult("One or more Playlists not found.") { StatusCode = 404 };
                }
            }

            switch (shuffle)
            {
                case "none":
                    break;
                case "TrackRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrack, false, enableTrackLinks);
                    break;
                case "TrackFullRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrack, true, enableTrackLinks);
                    break;
                case "Album":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByAlbum, false, enableTrackLinks);
                    break;
                case "AlbumRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByAlbum, true, enableTrackLinks);
                    break;
                case "ArtistRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByArtistRandom, true, enableTrackLinks);
                    break;
                case "TrackFavorites":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrackFavorites, false, enableTrackLinks);
                    break;
                case "TrackDiscovery":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrackDiscovery, false, enableTrackLinks);
                    break;
            }

            queue.Tracks = tracks.Select(x => new DbLink(x)).ToList();
            queue.TrackCount = tracks.Count;
            queue.OriginalTrackOrder = (from track in queue.Tracks
                                        select track._id).ToList();
            QCollection.InsertOne(queue);

            return new ObjectResult(queue._id) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("get")]
        public ObjectResult GetQueueFromIDs(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            var qFilter = Builders<PlayQueue>.Filter.Eq(x=>x._id, id);
            var qProjection = Builders<PlayQueue>.Projection.Exclude(x => x.Tracks)
                                                            .Exclude(x => x.OriginalTrackOrder);
            var queue = QCollection.Find(qFilter).Project(qProjection).ToList()
                                   .Select(x => BsonSerializer.Deserialize<ResponseQueue>(x)).FirstOrDefault();

            if (queue != null)
            {
                if (queue.PublicEditing == false)
                {
                    if (queue.Owner != curId && !queue.Editors.Contains(curId) && !queue.Viewers.Contains(curId))
                    {
                        return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                    }
                }

                return new ObjectResult(queue) { StatusCode = 200 };
            }

            return new ObjectResult($"Queue not found") { StatusCode = 404 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("search")]
        public ObjectResult SearchQueues(int page = 0, int count = 100, string name = "")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            var ownerFilter = Builders<PlayQueue>.Filter.Eq(x => x.Owner, curId);
            var EditorsFilter = Builders<PlayQueue>.Filter.AnyEq(x => x.Editors, curId);
            var viewersFilter = Builders<PlayQueue>.Filter.AnyEq(x => x.Viewers, curId);
            var publicViewingFilter = Builders<PlayQueue>.Filter.Eq(x => x.PublicViewing, true);
            var nameFilter = Builders<PlayQueue>.Filter.Regex(x => x.Name, new BsonRegularExpression(name, "i"));

            // Combine filters with OR
            var orFilter = Builders<PlayQueue>.Filter.Or(ownerFilter, viewersFilter, publicViewingFilter, EditorsFilter);
            var andFilter = Builders<PlayQueue>.Filter.And(orFilter, nameFilter);
            var qProjection = Builders<PlayQueue>.Projection.Exclude(x => x.Tracks)
                                                            .Exclude(x => x.OriginalTrackOrder);

            var Queues = QCollection.Find(andFilter)
                                    .Project(qProjection)
                                    .Skip(page * count)
                                    .Limit(count)
                                    .ToList()
                                    .Select(x => BsonSerializer.Deserialize<ResponseQueue>(x));

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

            var qFilter = Builders<PlayQueue>.Filter.Eq(x => x._id, id);

            var Queues = QCollection.Find(qFilter).ToList();
            if (Queues.Count() == 0)
            {
                return new ObjectResult("Queue not found") { StatusCode = 404 };
            }
            var queue = Queues[0];

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            if (queue.PublicEditing == false)
            {
                if (queue.Owner != curId && !queue.Editors.Contains(curId) && !queue.Viewers.Contains(curId))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            var tracks = Queues[0].Tracks.Take(new Range(page * count, (page * count) + count));

            var trackProjection = Builders<Track>.Projection.Exclude(x => x.Path)
                                                            .Exclude(x => x.LyricsPath);

            List<ResponseTrack> fullTracks = TracksCollection.Find(Builders<Track>.Filter.In(x => x._id, tracks.Select(x => x._id)))
                                                     .Project(trackProjection).ToList().Select(x => BsonSerializer.Deserialize<ResponseTrack>(x)).ToList();

            var userIds = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            userIds.Add(curId);

            List<ResponseTrack> orderedTracks = new List<ResponseTrack>();
            foreach (var sTrack in tracks)
            {
                ResponseTrack track = fullTracks.Where(x => x._id == sTrack._id).FirstOrDefault();

                // Check for null or empty collections to avoid exceptions
                if (track.PlayCounts != null)
                {
                    track.PlayCounts = track.PlayCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                }

                if (track.SkipCounts != null)
                {
                    track.SkipCounts = track.SkipCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                }

                if (track.Ratings != null)
                {
                    track.Ratings = track.Ratings.Where(x => userIds.Contains(x.UserId)).ToList();
                }

                orderedTracks.Add(track);
            }

            return new ObjectResult(orderedTracks) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("add-tracks")]
        public ObjectResult AddToQueue(string id, [FromQuery] List<string> trackIds, string position = "end", int place = 0)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            var qFilter = Builders<PlayQueue>.Filter.Eq(x => x._id, id);
            var queues = QCollection.Find(qFilter).ToList();
            if(queues.Count() == 0)
            {
                return new ObjectResult("Queue not found") { StatusCode = 404 };
            }
            var queue = queues[0];

            if (queue.PublicEditing == false)
            {
                if (queue.Owner != curId && !queue.Editors.Contains(curId))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            foreach (var tid in trackIds)
            {
                var trackFilter = Builders<Track>.Filter.Eq(x=>x._id, tid);
                var track = TCollection.Find(trackFilter).ToList()[0];
                if (track == null)
                {
                    continue;
                }

                switch (position)
                {
                    case "end":
                        queue.Tracks.Add(new DbLink(track));
                        queue.OriginalTrackOrder.Add(track._id);
                        break;
                    case "front":
                        if(queue.CurPosition >= 0)
                        {
                            queue.CurPosition++;
                        }
                        queue.Tracks.Insert(0, new DbLink(track));
                        queue.OriginalTrackOrder.Insert(0, track._id);
                        break;
                    case "random":
                        int randIdx = new Random().Next(0, queue.Tracks.Count());
                        if(queue.CurPosition >= randIdx)
                        {
                            queue.CurPosition++;
                        }
                        queue.Tracks.Insert(randIdx, new DbLink(track));
                        queue.OriginalTrackOrder.Insert(randIdx, track._id);
                        break;
                    case "at":
                        if (queue.CurPosition >= place)
                        {
                            queue.CurPosition++;
                        }
                        queue.Tracks.Insert(place, new DbLink(track));
                        queue.OriginalTrackOrder.Insert(place, track._id);
                        break;
                }
                queue.TrackCount++;
            }
            QCollection.ReplaceOne(qFilter, queue);

            StreamManager.AlertQueueUpdate(queue._id);

            return new ObjectResult("Tracks added") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("remove-tracks")]
        public ObjectResult RemoveFromQueue(string id, [FromQuery] List<int> positions)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            var qFilter = Builders<PlayQueue>.Filter.Eq(x => x._id, id);
            var queues = QCollection.Find(qFilter).ToList();
            if (queues.Count() == 0)
            {
                return new ObjectResult("Queue not found") { StatusCode = 404 };
            }
            var queue = queues[0];
            if (queue.PublicEditing == false)
            {
                if (queue.Owner != curId && !queue.Editors.Contains(curId))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            foreach (var pos in positions)
            {
                if(queue.CurPosition == pos)
                {
                    StreamManager.AlertQueueUpdate(id, "UPDATE CURRENT");
                }
                else if (queue.CurPosition > pos)
                {
                    queue.CurPosition--;
                }
                var trackId = queue.Tracks[pos]._id;
                queue.Tracks.RemoveAt(pos);
                queue.OriginalTrackOrder.Remove(trackId);
                queue.TrackCount--;
            }
            QCollection.ReplaceOne(qFilter, queue);

            StreamManager.AlertQueueUpdate(queue._id);

            return new ObjectResult("Tracks removed") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("delete")]
        public ObjectResult DeleteQueue(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();

            //var str = queue._id.ToString();
            var qFilter = Builders<PlayQueue>.Filter.Eq(x => x._id, id);
            var queue = QCollection.Find(qFilter).FirstOrDefault();
            if (queue == null)
            {
                return new ObjectResult("Queue Not Found") { StatusCode = 404 };
            }

            if (queue.Owner != curId)
            {
                return new ObjectResult("Invalid Auth") { StatusCode = 401 };
            }

            QCollection.DeleteOne(qFilter);

            return new ObjectResult("Queue deleted") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("move-track")]
        public ObjectResult MoveTrack(string id, int fromPos, int toPos)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            var qFilter = Builders<PlayQueue>.Filter.Eq(x=>x._id, id);
            var queue = QCollection.Find(qFilter).FirstOrDefault();
            if (queue == null)
            {
                return new ObjectResult("Queue not found") { StatusCode = 404 };
            }

            if (queue.PublicEditing == false)
            {
                if (queue.Owner != curId && !queue.Editors.Contains(curId))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            var track = queue.Tracks[fromPos];
            if(queue.CurPosition == fromPos)
            {
                StreamManager.AlertQueueUpdate(id, "UPDATE CURRENT");
            }
            else if(queue.CurPosition > fromPos && queue.CurPosition <= toPos)
            {
                queue.CurPosition--;
            }
            else if(queue.CurPosition > toPos && queue.CurPosition < fromPos)
            {
                queue.CurPosition++;
            }
            queue.Tracks.RemoveAt(fromPos);
            queue.Tracks.Insert(toPos, track);

            QCollection.ReplaceOne(qFilter, queue);

            StreamManager.AlertQueueUpdate(queue._id);

            return new ObjectResult("Track moved") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("update-position")]
        public ObjectResult UpdateQueuePosition(string id, int pos, string device = "")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            var qFilter = Builders<PlayQueue>.Filter.Eq(x=>x._id, id);
            var oq = QCollection.Find(qFilter).FirstOrDefault();
            if (oq == null)
            {
                return new ObjectResult("Queue not found") { StatusCode = 404 };
            }

            if (oq.PublicEditing == false)
            {
                if (oq.Owner != curId && !oq.Editors.Contains(curId))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            oq.CurPosition = pos;

            QCollection.ReplaceOne(qFilter, oq);

            StreamManager.AlertQueueUpdate(id, skipDevice:device);

            return new ObjectResult("Queue updated") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("update")]
        public ObjectResult UpdateQueue(string id, string name = "", [FromQuery] string[] editors = null, [FromQuery] string[] viewers = null, 
                                        string publicEditing = "", string publicViewing = "")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            var qFilter = Builders<PlayQueue>.Filter.Eq(x=>x._id, id);
            var oq = QCollection.Find(qFilter).FirstOrDefault();
            if (oq == null)
            {
                return new ObjectResult("Queue not found") { StatusCode = 404 };
            }

            if (oq.PublicEditing == false)
            {
                if (oq.Owner != curId && !oq.Editors.Contains(curId))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            oq.Name = name == "" ? oq.Name : name;
            try
            {
                oq.PublicEditing = publicEditing == "" ? oq.PublicEditing : bool.Parse(publicEditing);
                oq.PublicViewing = publicViewing == "" ? oq.PublicViewing : bool.Parse(publicViewing);
            }
            catch (Exception)
            {
                return new ObjectResult("Invalid Parameters") { StatusCode = 400 };
            }

            oq.Editors = editors == null ? oq.Editors : editors.ToList();
            oq.Viewers = viewers == null ? oq.Viewers : viewers.ToList();


            QCollection.ReplaceOne(qFilter, oq);

            StreamManager.AlertQueueUpdate(oq._id);

            return new ObjectResult("Queue updated") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("shuffle")]
        public ObjectResult ShuffleQueue(string id, string shuffle = "None", bool enableTrackLinks = true)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var QCollection = mongoDatabase.GetCollection<PlayQueue>("Queues");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            var qFilter = Builders<PlayQueue>.Filter.Eq(x => x._id, id);
            var queues = QCollection.Find(qFilter).ToList();
            if(queues.Count() == 0)
            {
                return new ObjectResult("Queue not found") { StatusCode = 404 };
            }
            var queue = queues[0];

            if (queue.PublicEditing == false)
            {
                if (queue.Owner != curId && !queue.Editors.Contains(curId))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }


            List<Track> tracks = TCollection.Find(Builders<Track>.Filter.In(x => x._id, queue.Tracks.Select(x => x._id))).ToList();
            Track currentTrack = tracks.Where(x=>x._id == queue.Tracks[queue.CurPosition]._id).FirstOrDefault();
            tracks.Remove(currentTrack);

            switch (shuffle)
            {
                case "None":
                    tracks.Clear();
                    queue.Tracks.Remove(queue.Tracks[queue.CurPosition]);
                    List<DbLink> sTracks = new List<DbLink>();
                    foreach (var tid in queue.OriginalTrackOrder)
                    {
                        var track = (from t in queue.Tracks
                                     where t._id == tid
                                     select t).FirstOrDefault();

                        if (track != null)
                        {
                            sTracks.Add(track);
                        }
                    }
                    queue.Tracks.Clear();
                    queue.Tracks.Add(new DbLink(currentTrack));
                    queue.Tracks.AddRange(sTracks);
                    QCollection.ReplaceOne(qFilter, queue);
                    return new ObjectResult("Tracks Reset") { StatusCode = 200 };
                case "TrackRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrack, false, enableTrackLinks);
                    break;
                case "TrackFullRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrack, true, enableTrackLinks);
                    break;
                case "Album":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByAlbum, false, enableTrackLinks);
                    break;
                case "AlbumRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByAlbum, true, enableTrackLinks);
                    break;
                case "ArtistRandom":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByArtistRandom, true, enableTrackLinks);
                    break;
                case "TrackFavorites":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrackFavorites, false, enableTrackLinks);
                    break;
                case "TrackDiscovery":
                    tracks = MelonAPI.ShuffleTracks(tracks, curId, Melon.Types.ShuffleType.ByTrackDiscovery, false, enableTrackLinks);
                    break;
            }

            queue.Tracks.Clear();
            queue.Tracks.Add(new DbLink(currentTrack));
            queue.CurPosition = 0;
            queue.Tracks.AddRange(tracks.Select(x=> new DbLink(x)));
            QCollection.ReplaceOne(qFilter, queue);

            StreamManager.AlertQueueUpdate(queue._id);

            return new ObjectResult("Tracks Shuffled") { StatusCode = 200 };
        }
    }
}