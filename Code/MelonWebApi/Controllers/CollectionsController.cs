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
    [Route("api/collections")]
    public class CollectionsController : ControllerBase
    {
        private readonly ILogger<CollectionsController> _logger;

        public CollectionsController(ILogger<CollectionsController> logger)
        {
            _logger = logger;
        }

        [Authorize(Roles = "Admin,User")]
        [HttpPost("create")]
        public ObjectResult CreateCollection(string name, string description = "", [FromQuery] List<string> andFilters = null, [FromQuery] List<string> orFilters = null)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var CollectionsCollection = mongoDatabase.GetCollection<Collection>("Collections");

            if(andFilters == null)
            {
                andFilters = new List<string>() { "TrackName;Contains;Dream" };
            }
            if(orFilters == null)
            {
                orFilters = new List<string>();
            }

            Collection col = new Collection();
            col._id = ObjectId.GenerateNewId().ToString();
            col.Name = name;
            col.Description = description;
            col.AndFilters = andFilters;
            col.OrFilters = orFilters;
            col.Editors = new List<string>();
            col.Viewers = new List<string>();
            col.Owner = curId;
            col.PublicViewing = false;
            col.PublicEditing = false;
            col.ArtworkPath = "";
            col.Tracks = Collection.FindTracks(andFilters, orFilters, curId);
            col.TrackCount = col.Tracks.Count();

            if(col.Tracks == null)
            {
                return new ObjectResult("Invalid Parameters") { StatusCode = 400 };
            }

            CollectionsCollection.InsertOne(col);

            return new ObjectResult(col._id) { StatusCode = 200 };
        }
        
        [Authorize(Roles = "Admin,User")]
        [HttpPost("add-filters")]
        public ObjectResult AddFilter(string id, [FromQuery] List<string> andFilters = null, [FromQuery] List<string> orFilters = null)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ColCollection = mongoDatabase.GetCollection<Collection>("Collections");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            var cFilter = Builders<Collection>.Filter.Eq(x=>x._id, id);
            var collection = ColCollection.Find(cFilter).FirstOrDefault();
            if(collection == null)
            {
                return new ObjectResult("Collection Not Found") { StatusCode = 404 };
            }

            if(collection.PublicEditing == false)
            {
                if(collection.Owner != curId && !collection.Editors.Contains(curId))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            if(andFilters == null)
            {
                andFilters = new List<string>();
            }
            if(orFilters == null)
            {
                orFilters = new List<string>();
            }

            foreach (var filter in andFilters)
            {
                if(!collection.AndFilters.Contains(filter))
                {
                    collection.AndFilters.Add(filter);
                }
            }

            foreach (var filter in orFilters)
            {
                if(!collection.OrFilters.Contains(filter))
                {
                    collection.OrFilters.Add(filter);
                }
            }

            var tracks = Collection.FindTracks(collection.AndFilters, collection.OrFilters, collection.Owner);
            if (tracks == null)
            {
                return new ObjectResult("Invalid Parameters") { StatusCode = 400 };
            }
            collection.Tracks = tracks;
            collection.TrackCount = collection.Tracks.Count();

            ColCollection.ReplaceOne(cFilter, collection);

            return new ObjectResult("Filters added") { StatusCode = 200 };
        }

        [Authorize(Roles = "Admin,User")]
        [HttpPost("remove-filters")]
        public ObjectResult RemoveFilter(string id, [FromQuery] List<string> andFilters = null, [FromQuery] List<string> orFilters = null)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ColCollection = mongoDatabase.GetCollection<Collection>("Collections");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();

            var cFilter = Builders<Collection>.Filter.Eq(x => x._id, id);
            var collection = ColCollection.Find(cFilter).FirstOrDefault();
            if (collection == null)
            {
                return new ObjectResult("Collection Not Found") { StatusCode = 404 };
            }

            if (collection.PublicEditing == false)
            {
                if (collection.Owner != curId && !collection.Editors.Contains(curId))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            if (andFilters == null)
            {
                andFilters = new List<string>();
            }
            if (orFilters == null)
            {
                orFilters = new List<string>();
            }

            foreach (var filter in andFilters)
            {
                collection.AndFilters.Remove(filter);
            }

            foreach(var filter in orFilters)
            {
                collection.OrFilters.Remove(filter);
            }

            collection.Tracks = Collection.FindTracks(collection.AndFilters, collection.OrFilters, collection.Owner);
            if (collection.Tracks == null)
            {
                return new ObjectResult("Invalid Parameters") { StatusCode = 400 };
            }
            collection.TrackCount = collection.Tracks.Count();

            ColCollection.ReplaceOne(cFilter, collection);

            return new ObjectResult("Tracks removed") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpPost("delete")]
        public ObjectResult DeleteCollection(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var CCollection = mongoDatabase.GetCollection<Collection>("Collections");

            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();

            //var str = queue._id.ToString();
            var cFilter = Builders<Collection>.Filter.Eq(x => x._id, id);
            var collection = CCollection.Find(cFilter).FirstOrDefault();
            if (collection == null)
            {
                return new ObjectResult("Collection Not Found") { StatusCode = 404 };
            }

            if (collection.Owner != curId)
            {
                return new ObjectResult("Invalid Auth") { StatusCode = 401 };
            }

            CCollection.DeleteOne(cFilter);

            return new ObjectResult("Collection deleted") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin, User")]
        [HttpPost("update")]
        public ObjectResult updateCollection(string id, string description = "", string name = "", [FromQuery] List<string> editors = null, [FromQuery] List<string> viewers = null,
                                           string publicEditing = "", string publicViewing = "")
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
                var mongoDatabase = mongoClient.GetDatabase("Melon");
                var ColCollection = mongoDatabase.GetCollection<Collection>("Collections");

                var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();

                var cFilter = Builders<Collection>.Filter.Eq(x=>x._id, id);
                var collection = ColCollection.Find(cFilter).FirstOrDefault();
                if (collection == null)
                {
                    return new ObjectResult("Collection not found") { StatusCode = 404 };
                }

                if (collection.PublicEditing == false)
                {
                    if (collection.Owner != curId && !collection.Editors.Contains(curId))
                    {
                        return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                    }
                }

                description = description == "" ? collection.Description : description;
                name = name == "" ? collection.Name : name;
                editors = editors == null ? collection.Editors : editors;
                viewers = viewers == null ? collection.Viewers : viewers;
                bool pubEditing = false;
                bool pubViewing = false;
                try
                {
                    pubEditing = publicEditing == "" ? collection.PublicEditing : bool.Parse(publicEditing);
                    pubViewing = publicViewing == "" ? collection.PublicViewing : bool.Parse(publicViewing);
                }
                catch (Exception)
                {
                    return new ObjectResult("Invalid Parameters") { StatusCode = 400 };
                }
                

                collection._id = id;
                collection.Description = description;
                collection.Name = name;
                collection.Editors = editors;
                collection.Viewers = viewers;
                collection.PublicEditing = pubEditing;
                collection.PublicViewing = pubViewing;

                ColCollection.ReplaceOne(cFilter, collection);
            }
            catch (Exception e)
            {
                return new ObjectResult(e.Message) { StatusCode = 500 };
            }


            return new ObjectResult("Playlist updated") { StatusCode = 404 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("get")]
        public ObjectResult GetCollectionById(string id)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ColCollection = mongoDatabase.GetCollection<Collection>("Collections");

            var cFilter = Builders<Collection>.Filter.Eq(x=>x._id, id);
            var cProjection = Builders<Collection>.Projection.Exclude(x => x.Tracks)
                                                              .Exclude(x => x.ArtworkPath);
            var cDocs = ColCollection.Find(cFilter).Project(cProjection)
                                     .ToList();

            var collection = cDocs.Select(x => BsonSerializer.Deserialize<ResponseCollection>(x)).FirstOrDefault();

            if (collection != null)
            {
                if (collection.PublicEditing == false)
                {
                    if (collection.Owner != curId && !collection.Editors.Contains(curId) && !collection.Viewers.Contains(curId))
                    {
                        return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                    }
                }
                return new ObjectResult(collection) { StatusCode = 200 };
            }

            return new ObjectResult("Collection not found") { StatusCode = 404 };
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("search")]
        public ObjectResult SearchCollections(int page, int count, string name="")
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ColCollection = mongoDatabase.GetCollection<Collection>("Collections");

            List<ResponseCollection> Collections = new List<ResponseCollection>();

            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();

            var ownerFilter = Builders<Collection>.Filter.Eq(x => x.Owner, curId);
            var viewersFilter = Builders<Collection>.Filter.AnyEq(x => x.Viewers, curId);
            var publicViewingFilter = Builders<Collection>.Filter.Eq(x => x.PublicViewing, true);
            var EditorsFilter = Builders<Collection>.Filter.AnyEq(x => x.Editors, curId);

            // Combine filters with OR
            var combinedFilter = Builders<Collection>.Filter.Or(ownerFilter, viewersFilter, publicViewingFilter, EditorsFilter);
            combinedFilter = combinedFilter & Builders<Collection>.Filter.Regex(x=>x.Name, new BsonRegularExpression(name, "i"));
            var plstProjection = Builders<Collection>.Projection.Exclude(x => x.Tracks)
                                                              .Exclude(x => x.ArtworkPath);

            Collections.AddRange(ColCollection.Find(combinedFilter)
                                          .Project(plstProjection)
                                          .Skip(page * count)
                                          .Limit(count)
                                          .ToList()
                                          .Select(x => BsonSerializer.Deserialize<ResponseCollection>(x)));

            

            return new ObjectResult(Collections) { StatusCode = 200 };
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
            var ColCollection = mongoDatabase.GetCollection<Collection>("Collections");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var cFilter = Builders<Collection>.Filter.Eq(x => x._id, id);
            var collection = ColCollection.Find(cFilter).FirstOrDefault();
            if (collection == null)
            {
                return new ObjectResult("Collection not found") { StatusCode = 404 };
            }

            if (collection.PublicEditing == false)
            {
                if (collection.Owner != curId && !collection.Editors.Contains(curId) && !collection.Viewers.Contains(curId))
                {
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            var tracks = collection.Tracks.Take(new Range(page * count, (page * count) + count));

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
