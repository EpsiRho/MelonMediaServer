using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;
using System.Data;
using MongoDB.Driver;
using Melon.LocalClasses;
using System.Diagnostics;
using Melon.Models;
using System.Security.Claims;
using ATL.Playlist;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/playlists")]
    public class PlaylistsController : ControllerBase
    {
        private readonly ILogger<PlaylistsController> _logger;

        public PlaylistsController(ILogger<PlaylistsController> logger)
        {
            _logger = logger;
        }

        [Authorize(Roles = "Admin,User")]
        [HttpPost("create")]
        public ObjectResult CreatePlaylist(string name, string description = "", [FromQuery] List<string> trackIds = null)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            Playlist playlist = new Playlist();
            playlist._id = ObjectId.GenerateNewId().ToString();
            playlist.Name = name;
            playlist.TrackCount = 0;
            playlist.Owner = curId;
            playlist.Editors = new List<string>();
            playlist.Viewers = new List<string>();
            playlist.PublicEditing = false;
            playlist.PublicViewing = false;
            playlist.Description = description;
            playlist.ArtworkPath = "";
            playlist.Tracks = new List<DbLink>();
            //var str = queue._id.ToString();
            var pFilter = Builders<Playlist>.Filter.Eq(x=>x._id, playlist._id);
            if(trackIds == null)
            {
                PCollection.InsertOne(playlist);
                return new ObjectResult(playlist._id.ToString()) { StatusCode = 200 };
            }
            foreach(var id in trackIds)
            {
                var trackFilter = Builders<Track>.Filter.Eq(x=>x._id, id);
                var trackDoc = TCollection.Find(trackFilter).ToList();
                if(trackDoc.Count != 0)
                {
                    playlist.TrackCount++;
                    playlist.Tracks.Add(new DbLink(trackDoc[0]));
                }
            
            }
            PCollection.InsertOne(playlist);


            return new ObjectResult(playlist._id) { StatusCode = 200 };
        }

        [Authorize(Roles = "Admin,User")]
        [HttpPost("add-tracks")]
        public ObjectResult AddToPlaylist(string id, [FromQuery] List<string> trackIds)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();

            //var str = queue._id.ToString();
            var pFilter = Builders<Playlist>.Filter.Eq(x=>x._id, id);
            var playlists = PCollection.Find(pFilter).ToList();
            if(playlists.Count == 0)
            {
                return new ObjectResult("Playlist Not Found") { StatusCode = 404 };
            }
            var playlist = playlists[0];

            if(playlist.PublicEditing == false)
            {
                if(playlist.Owner != curId && !playlist.Editors.Contains(curId))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            foreach (var tid in trackIds)
            {
                var trackFilter = Builders<Track>.Filter.Eq(x=>x._id, tid);
                var trackDoc = TCollection.Find(trackFilter).ToList();
                if (trackDoc.Count != 0)
                {
                    playlist.Tracks.Add(new DbLink(trackDoc[0]));
                    playlist.TrackCount++;
                }
            }
            PCollection.ReplaceOne(pFilter, playlist);

            return new ObjectResult("Tracks added") { StatusCode = 200 };
        }

        [Authorize(Roles = "Admin,User")]
        [HttpPost("remove-tracks")]
        public ObjectResult RemoveFromPlaylist(string id, [FromQuery] List<int> positions)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();

            //var str = queue._id.ToString();
            var pFilter = Builders<Playlist>.Filter.Eq(x=>x._id, id);
            var playlist = PCollection.Find(pFilter).FirstOrDefault();
            if (playlist == null)
            {
                return new ObjectResult("Playlist Not Found") { StatusCode = 404 };
            }

            if (playlist.PublicEditing == false)
            {
                if (playlist.Owner != curId && !playlist.Editors.Contains(curId))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            foreach (var pos in positions)
            {
                playlist.Tracks.RemoveAt(pos);
                playlist.TrackCount--;
            }
            PCollection.ReplaceOne(pFilter, playlist);

            return new ObjectResult("Tracks removed") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("delete")]
        public ObjectResult DeletePlaylist(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();

            //var str = queue._id.ToString();
            var pFilter = Builders<Playlist>.Filter.Eq(x=>x._id, id);
            var playlist = PCollection.Find(pFilter).FirstOrDefault();
            if (playlist == null)
            {
                return new ObjectResult("Playlist Not Found") { StatusCode = 404 };
            }

            if (playlist.Owner != curId)
            {
                return new ObjectResult("Invalid Auth") { StatusCode = 401 };
            }

            PCollection.DeleteOne(pFilter);

            return new ObjectResult("Playlist deleted") { StatusCode = 200 };
        }

        [Authorize(Roles = "Admin, User")]
        [HttpPost("update")]
        public ObjectResult updatePlaylist(string id, string description = "", string name = "", [FromQuery] List<string> editors = null, [FromQuery] List<string> viewers = null,
                                           string publicEditing = "", string publicViewing = "")
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
                var mongoDatabase = mongoClient.GetDatabase("Melon");
                var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

                var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

                var pFilter = Builders<Playlist>.Filter.Eq(x=>x._id, id);
                var playlists = PCollection.Find(pFilter).ToList();
                if (playlists.Count == 0)
                {
                    return new ObjectResult("Playlist not found") { StatusCode = 404 };
                }
                var plst = playlists[0];

                if (plst.PublicEditing == false)
                {
                    if (plst.Owner != curId && !plst.Editors.Contains(curId))
                    {
                        return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                    }
                }

                description = description == "" ? plst.Description : description;
                name = name == "" ? plst.Name : name;
                editors = editors == null ? plst.Editors : editors;
                viewers = viewers == null ? plst.Viewers : viewers;
                bool pubEditing = false;
                bool pubViewing = false;
                try
                {
                    pubEditing = publicEditing == "" ? plst.PublicEditing : bool.Parse(publicEditing);
                    pubViewing = publicViewing == "" ? plst.PublicViewing : bool.Parse(publicViewing);
                }
                catch (Exception)
                {
                    return new ObjectResult("Invalid Parameters") { StatusCode = 400 };
                }
                

                plst._id = id;
                plst.Description = description;
                plst.Name = name;
                plst.Editors = editors;
                plst.Viewers = viewers;
                plst.PublicEditing = pubEditing;
                plst.PublicViewing = pubViewing;

                PCollection.ReplaceOne(pFilter, plst);
            }
            catch (Exception e)
            {
                return new ObjectResult(e.Message) { StatusCode = 500 };
            }


            return new ObjectResult("Playlist updated") { StatusCode = 404 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("move-track")]
        public ObjectResult MoveTrack(string id, int fromPos, int toPos)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            var pFilter = Builders<Playlist>.Filter.Eq(x => x._id, id);
            var playlist = PCollection.Find(pFilter).FirstOrDefault();
            if (playlist == null)
            {
                return new ObjectResult("Playlist not found") { StatusCode = 404 };
            }

            if (playlist.PublicEditing == false)
            {
                if (playlist.Owner != curId && !playlist.Editors.Contains(curId))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            var track = playlist.Tracks[fromPos];
            playlist.Tracks.RemoveAt(fromPos);
            playlist.Tracks.Insert(toPos, track);

            PCollection.ReplaceOne(pFilter, playlist);

            StreamManager.AlertQueueUpdate(playlist._id);

            return new ObjectResult("Track moved") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("get")]
        public ObjectResult GetPlaylistById(string id)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var pCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var pFilter = Builders<Playlist>.Filter.Eq(x=>x._id, id);
            var plstProjection = Builders<Playlist>.Projection.Exclude(x => x.Tracks)
                                                              .Exclude(x => x.ArtworkPath);
            var pDocs = pCollection.Find(pFilter).Project(plstProjection)
                                            .ToList();

            var plst = pDocs.Select(x => BsonSerializer.Deserialize<ResponsePlaylist>(x)).FirstOrDefault();

            if (plst != null)
            {
                if (plst.PublicEditing == false)
                {
                    if (plst.Owner != curId && !plst.Editors.Contains(curId) && !plst.Viewers.Contains(curId))
                    {
                        return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                    }
                }
                return new ObjectResult(plst) { StatusCode = 200 };
            }

            return new ObjectResult("Playlist not found") { StatusCode = 404 };
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("search")]
        public ObjectResult SearchPlaylists(int page, int count, string name="")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            List<ResponsePlaylist> playlists = new List<ResponsePlaylist>();

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            var ownerFilter = Builders<Playlist>.Filter.Eq(x => x.Owner, curId);
            var viewersFilter = Builders<Playlist>.Filter.AnyEq(x => x.Viewers, curId);
            var publicViewingFilter = Builders<Playlist>.Filter.Eq(x => x.PublicViewing, true);
            var EditorsFilter = Builders<Playlist>.Filter.AnyEq(x => x.Editors, curId);

            // Combine filters with OR
            var combinedFilter = Builders<Playlist>.Filter.Or(ownerFilter, viewersFilter, publicViewingFilter, EditorsFilter);
            combinedFilter = combinedFilter & Builders<Playlist>.Filter.Regex(x=>x.Name, new BsonRegularExpression(name, "i"));
            var plstProjection = Builders<Playlist>.Projection.Exclude(x => x.Tracks)
                                                              .Exclude(x => x.ArtworkPath);

            playlists.AddRange(PCollection.Find(combinedFilter)
                                          .Project(plstProjection)
                                          .Skip(page * count)
                                          .Limit(count)
                                          .ToList()
                                          .Select(x => BsonSerializer.Deserialize<ResponsePlaylist>(x)));

            

            return new ObjectResult(playlists) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("get-tracks")]
        public ObjectResult GetTracks(string id, int page = 0, int count = 100)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var pFilter = Builders<Playlist>.Filter.Eq(x => x._id, id);


            var Playlists = PCollection.Find(pFilter).ToList();
            if (Playlists.Count() == 0)
            {
                return new ObjectResult("Playlist not found") { StatusCode = 404 };
            }
            var playlist = Playlists[0];

            if (playlist.PublicEditing == false)
            {
                if (playlist.Owner != curId && !playlist.Editors.Contains(curId) && !playlist.Viewers.Contains(curId))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            var tracks = playlist.Tracks.Take(new Range(page * count, (page * count) + count));

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
    }
}
