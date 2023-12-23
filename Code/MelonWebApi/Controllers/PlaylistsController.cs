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
        public string CreatePlaylist(string name, string description = "", string artworkPath = "", List<string> trackIds = null)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var userName = User.Identity.Name;

            Playlist playlist = new Playlist();
            playlist._id = ObjectId.GenerateNewId();
            playlist.PlaylistId = playlist._id.ToString();
            playlist.Name = name;
            playlist.Owner = userName;
            playlist.Editors = new List<string>();
            playlist.Viewers = new List<string>();
            playlist.PublicEditing = false;
            playlist.PublicViewing = false;
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

        [Authorize(Roles = "Admin,User")]
        [HttpPost("add-tracks")]
        public string AddToPlaylist(string _id, List<string> trackIds)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var userName = User.Identity.Name;

            //var str = queue._id.ToString();
            var pFilter = Builders<Playlist>.Filter.Eq("_id", ObjectId.Parse(_id));
            var playlists = PCollection.Find(pFilter).ToList();
            if(playlists.Count == 0)
            {
                return "Playlist Not Found";
            }
            var playlist = playlists[0];

            if(playlist.PublicEditing == false)
            {
                if(playlist.Owner != userName && !playlist.Editors.Contains(userName))
                {
                    return "Invalid Auth";
                }
            }

            foreach (var id in trackIds)
            {
                var trackFilter = Builders<Track>.Filter.Eq("_id", new ObjectId(id));
                var trackDoc = TCollection.Find(trackFilter).ToList();
                playlist.Tracks.Add(new ShortTrack(trackDoc[0]));
            }
            PCollection.ReplaceOne(pFilter, playlist);

            return "200";
        }

        [Authorize(Roles = "Admin,User")]
        [HttpPost("remove-tracks")]
        public string RemoveFromPlaylist(string _id, List<string> trackIds)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var userName = User.Identity.Name;

            //var str = queue._id.ToString();
            var pFilter = Builders<Playlist>.Filter.Eq("_id", ObjectId.Parse(_id));
            var playlists = PCollection.Find(pFilter).ToList();
            if (playlists.Count == 0)
            {
                return "Playlist Not Found";
            }
            var playlist = playlists[0];

            if (playlist.PublicEditing == false)
            {
                if (playlist.Owner != userName && !playlist.Editors.Contains(userName))
                {
                    return "Invalid Auth";
                }
            }

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

        [Authorize(Roles = "Admin, User")]
        [HttpPost("update")]
        public string updatePlaylist(ShortPlaylist playlist)
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
                var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

                var userName = User.Identity.Name;

                //var str = queue._id.ToString();
                var pFilter = Builders<Playlist>.Filter.Eq("_id", playlist._id);
                var playlists = PCollection.Find(pFilter).ToList();
                if (playlists.Count == 0)
                {
                    return "Playlist Not Found";
                }
                var plst = playlists[0];

                if (plst.PublicEditing == false)
                {
                    if (plst.Owner != userName && !plst.Editors.Contains(userName))
                    {
                        return "Invalid Auth";
                    }
                }

                plst._id = playlist._id;
                plst.PlaylistId = playlist.PlaylistId;
                plst.ArtworkPath = playlist.ArtworkPath;
                plst.Description = playlist.Description;
                plst.Name = playlist.Name;
                plst.Owner = playlist.Owner;
                plst.Editors = playlist.Editors;
                plst.Viewers = playlist.Viewers;
                plst.PublicEditing = playlist.PublicEditing;
                plst.PublicViewing = playlist.PublicViewing;

                PCollection.ReplaceOne(pFilter, plst);
            }
            catch (Exception)
            {
                return "500";
            }


            return "200";
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("get")]
        public ShortPlaylist GetPlaylistById(string _id)
        {
            var userName = User.Identity.Name;

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var pCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var pFilter = Builders<Playlist>.Filter.Eq("_id", new ObjectId(_id));
            var pDoc = pCollection.Find(pFilter).ToList();

            if(pDoc.Count > 0)
            {
                var plst = pDoc[0];
                if (plst.PublicEditing == false)
                {
                    if (plst.Owner != userName && !plst.Editors.Contains(userName) && !plst.Viewers.Contains(userName))
                    {
                        return null;
                    }
                }

                return new ShortPlaylist(plst);
            }

            return null;
        }

        [Authorize(Roles = "Admin,User")]
        [HttpPost("move-track")]
        public string MoveTrack(string queueId, string trackId, int position)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var userName = User.Identity.Name;

            var pFilter = Builders<Playlist>.Filter.Eq("_id", ObjectId.Parse(queueId));
            var playlists = PCollection.Find(pFilter).ToList();
            if (playlists.Count() == 0)
            {
                return "Queue Not Found";
            }
            var playlist = playlists[0];

            if (playlist.PublicEditing == false)
            {
                if (playlist.Owner != userName && !playlist.Editors.Contains(userName))
                {
                    return null;
                }
            }

            var tracks = (from t in playlist.Tracks
                          where t.TrackId == trackId
                          select t).ToList();
            if (tracks.Count() == 0)
            {
                return "Track Not Found";
            }
            var track = tracks[0];

            int curIdx = playlist.Tracks.IndexOf(track);
            playlist.Tracks.Insert(position, track);
            playlist.Tracks.RemoveAt(curIdx);

            PCollection.ReplaceOne(pFilter, playlist);

            return "200";
        }

        [Authorize(Roles = "Admin,User")]
        [HttpGet("search")]
        public IEnumerable<ShortPlaylist> SearchPlaylists(int page, int count, string name = "")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var PCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var userName = User.Identity.Name;
            var pFilter = Builders<Playlist>.Filter.Regex(x => x.Name, new BsonRegularExpression(name, "i"));
            pFilter = pFilter & Builders<Playlist>.Filter.AnyEq(x => x.Editors, userName);
            pFilter = pFilter & Builders<Playlist>.Filter.AnyEq(x => x.Viewers, userName);
            pFilter = pFilter & Builders<Playlist>.Filter.Eq(x => x.PublicViewing, true);

            var playlists = PCollection.Find(pFilter)
                                    .Skip(page * count)
                                    .Limit(count)
                                    .ToList()
                                    .Select(x => new ShortPlaylist(x));

            return playlists;
        }
    }
}