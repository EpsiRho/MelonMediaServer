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

        /// <summary>
        /// Create a new Collection.
        /// </summary>
        /// <param name="name">The name of the collection.</param>
        /// <param name="description">The description of the collection.</param>
        /// <param name="andFilters">Filters where each must match for a track to be included in the collection.</param>
        /// <param name="orFilters">Filters where at least one must match for a track to be included in the collection.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin, User
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">On successful creation of the collection.</response>
        /// <response code="400">Invalid filters.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        [Authorize(Roles = "Admin,User")]
        [HttpPost("create")]
        public ObjectResult CreateCollection(string name, string description = "", [FromQuery] List<string> andFilters = null, [FromQuery] List<string> orFilters = null)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/collections/create", curId, new Dictionary<string, object>()
            {
                { "name", name },
                { "description", description },
                { "andFilters", andFilters },
                { "orFilters", orFilters }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var CollectionsCollection = mongoDatabase.GetCollection<Collection>("Collections");

            if(andFilters == null)
            {
                andFilters = new List<string>() { "Name;Contains;Dream" };
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
            col.Tracks = MelonAPI.FindTracks(andFilters, orFilters, curId, -1, -1).Select(x => new DbLink(x)).ToList();
            col.TrackCount = col.Tracks.Count();

            if(col.Tracks == null)
            {
                args.SendEvent("Invalid Parameters", 400, Program.mWebApi);
                return new ObjectResult("Invalid Parameters") { StatusCode = 400 };
            }

            CollectionsCollection.InsertOne(col);

            args.Args.Add("CollectionId", col._id);
            args.SendEvent("Created a collection", 200, Program.mWebApi);
            return new ObjectResult(col._id) { StatusCode = 200 };
        }

        /// <summary>
        /// Add filters to a collection.
        /// </summary>
        /// <param name="id">The unique identifier of the collection.</param>
        /// <param name="andFilters">Filters where each must match for a track to be included in the collection.</param>
        /// <param name="orFilters">Filters where at least one must match for a track to be included in the collection.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin, User
        /// - **User Specific**: Collections can only be edited by the owner of the collection and any editors set, unless the collection's 'PublicEditing' parameter is true.
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">On successfully adding the filters and updating the collection.</response>
        /// <response code="400">Invalid filters.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If Collection cannot be found.</response>
        [Authorize(Roles = "Admin,User")]
        [HttpPost("add-filters")]
        public ObjectResult AddFilter(string id, [FromQuery] List<string> andFilters = null, [FromQuery] List<string> orFilters = null)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/collections/add-filters", curId, new Dictionary<string, object>()
            {
                { "id", id },
                { "andFilters", andFilters },
                { "orFilters", orFilters }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ColCollection = mongoDatabase.GetCollection<Collection>("Collections");

            var cFilter = Builders<Collection>.Filter.Eq(x=>x._id, id);
            var collection = ColCollection.Find(cFilter).FirstOrDefault();
            if(collection == null)
            {
                args.SendEvent("Collection Not Found", 404, Program.mWebApi);
                return new ObjectResult("Collection Not Found") { StatusCode = 404 };
            }

            if(collection.PublicEditing == false)
            {
                if(collection.Owner != curId && !collection.Editors.Contains(curId))
                {
                    args.SendEvent("Invalid Auth", 401, Program.mWebApi);
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

            var tracks = MelonAPI.FindTracks(collection.AndFilters, collection.OrFilters, collection.Owner, -1, -1);
            if (tracks == null)
            {
                args.SendEvent("Invalid Parameters", 400, Program.mWebApi);
                return new ObjectResult("Invalid Parameters") { StatusCode = 400 };
            }
            collection.Tracks = tracks.Select(x => new DbLink(x)).ToList();
            collection.TrackCount = collection.Tracks.Count();

            ColCollection.ReplaceOne(cFilter, collection);

            args.SendEvent("Filters added", 200, Program.mWebApi);
            return new ObjectResult("Filters added") { StatusCode = 200 };
        }

        /// <summary>
        /// Remove filters from a collection.
        /// </summary>
        /// <param name="id">The unique identifier of the collection.</param>
        /// <param name="andFilters">Filters to be removed from the andFilters list.</param>
        /// <param name="orFilters">Filters to be removed from the orFilters list.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin, User
        /// - **User Specific**: Collections are can only be edited by the owner of the collection and any editors set, unless the collection's 'PublicEditing' parameter is true.
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">On successfully removing the filters and updating the collection.</response>
        /// <response code="400">Invalid filters.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If Collection cannot be found.</response>
        [Authorize(Roles = "Admin,User")]
        [HttpPost("remove-filters")]
        public ObjectResult RemoveFilter(string id, [FromQuery] List<string> andFilters = null, [FromQuery] List<string> orFilters = null)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/collections/remove-filters", curId, new Dictionary<string, object>()
            {
                { "id", id },
                { "andFilters", andFilters },
                { "orFilters", orFilters }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ColCollection = mongoDatabase.GetCollection<Collection>("Collections");

            var cFilter = Builders<Collection>.Filter.Eq(x => x._id, id);
            var collection = ColCollection.Find(cFilter).FirstOrDefault();
            if (collection == null)
            {
                args.SendEvent("Collection Not Found", 404, Program.mWebApi);
                return new ObjectResult("Collection Not Found") { StatusCode = 404 };
            }

            if (collection.PublicEditing == false)
            {
                if (collection.Owner != curId && !collection.Editors.Contains(curId))
                {
                    args.SendEvent("Invalid Auth", 401, Program.mWebApi);
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

            collection.Tracks = MelonAPI.FindTracks(collection.AndFilters, collection.OrFilters, collection.Owner, -1, -1).Select(x => new DbLink(x)).ToList();
            if (collection.Tracks == null)
            {
                args.SendEvent("Invalid Parameters", 400, Program.mWebApi);
                return new ObjectResult("Invalid Parameters") { StatusCode = 400 };
            }
            collection.TrackCount = collection.Tracks.Count();

            ColCollection.ReplaceOne(cFilter, collection);

            args.SendEvent("Tracks removed", 200, Program.mWebApi);
            return new ObjectResult("Tracks removed") { StatusCode = 200 };
        }

        /// <summary>
        /// Delete a collection.
        /// </summary>
        /// <param name="id">The unique identifier of the collection.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin, User
        /// - **User Specific**: Collections can only be deleted by the owner of the collection.
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">On successfully deleting the collection.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If Collection cannot be found.</response>
        [Authorize(Roles = "Admin,User")]
        [HttpPost("delete")]
        public ObjectResult DeleteCollection(string id)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/collections/delete", curId, new Dictionary<string, object>()
            {
                { "id", id },
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var CCollection = mongoDatabase.GetCollection<Collection>("Collections");

            //var str = queue._id.ToString();
            var cFilter = Builders<Collection>.Filter.Eq(x => x._id, id);
            var collection = CCollection.Find(cFilter).FirstOrDefault();
            if (collection == null)
            {
                args.SendEvent("Collection Not Found", 404, Program.mWebApi);
                return new ObjectResult("Collection Not Found") { StatusCode = 404 };
            }

            if (collection.Owner != curId)
            {
                args.SendEvent("Invalid Auth", 401, Program.mWebApi);
                return new ObjectResult("Invalid Auth") { StatusCode = 401 };
            }

            var filePath = $"{StateManager.melonPath}/CollectionArts/{collection.ArtworkPath}";

            try
            {
                System.IO.File.Delete(filePath);
            }
            catch (Exception)
            {

            }

            CCollection.DeleteOne(cFilter);

            args.SendEvent("Collection deleted", 200, Program.mWebApi);
            return new ObjectResult("Collection deleted") { StatusCode = 200 };
        }

        /// <summary>
        /// Update info about a collection.
        /// </summary>
        /// <param name="id">The unique identifier of the collection.</param>
        /// <param name="name">The name of the collection.</param>
        /// <param name="description">The description of the collection.</param>
        /// <param name="editors">A list of ids of users that are allowed to edit this playlist.</param>
        /// <param name="viewers">A list of ids of users that are allowed to view this playlist.</param>
        /// <param name="publicEditing">If the playlist allows any user to edit it.</param>
        /// <param name="publicViewing">If the playlist allows any user to view it.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin, User
        /// - **User Specific**: Collections are can only be edited by the owner of the collection and any editors set, unless the collection's 'PublicEditing' parameter is true.
        /// </remarks>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">On successfully updating the collection info.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If Collection cannot be found.</response>
        [Authorize(Roles = "Admin, User")]
        [HttpPost("update")]
        public ObjectResult updateCollection(string id, string description = "", string name = "", [FromQuery] List<string> editors = null, [FromQuery] List<string> viewers = null,
                                           string publicEditing = "", string publicViewing = "")
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/collections/update", curId, new Dictionary<string, object>()
            {
                { "id", id },
                { "description", description },
                { "editors", editors },
                { "viewers", viewers },
                { "publicEditing", publicEditing },
                { "publicViewing", publicViewing }
            });

            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
                var mongoDatabase = mongoClient.GetDatabase("Melon");
                var ColCollection = mongoDatabase.GetCollection<Collection>("Collections");

                var cFilter = Builders<Collection>.Filter.Eq(x=>x._id, id);
                var collection = ColCollection.Find(cFilter).FirstOrDefault();
                if (collection == null)
                {
                    args.SendEvent("Collection not found", 404, Program.mWebApi);
                    return new ObjectResult("Collection not found") { StatusCode = 404 };
                }

                if (collection.PublicEditing == false)
                {
                    if (collection.Owner != curId && !collection.Editors.Contains(curId))
                    {
                        args.SendEvent("Invalid Auth", 401, Program.mWebApi);
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
                {args.SendEvent("Invalid Parameters", 400, Program.mWebApi);
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

            args.SendEvent("Collection updated", 200, Program.mWebApi);
            return new ObjectResult("Collection updated") { StatusCode = 404 };
        }

        /// <summary>
        /// Get info about a collection.
        /// </summary>
        /// <param name="id">The unique identifier of the collection.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin, User, Pass
        /// - **User Specific**: Collections can only be viewed by the owner of the collection and any viewers set, unless the collection's 'PublicViewing' parameter is true.
        /// </remarks>
        /// <returns>The collection info.</returns>
        /// <response code="200">On successfully getting the collection info.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If Collection cannot be found.</response>
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("get")]
        public ObjectResult GetCollectionById(string id)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/collections/get", curId, new Dictionary<string, object>()
            {
                { "id", id },
            });

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
                if (collection.PublicViewing == false)
                {
                    if (collection.Owner != curId && !collection.Viewers.Contains(curId))
                    {
                        args.SendEvent("Invalid Auth", 401, Program.mWebApi);
                        return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                    }
                }
                args.SendEvent("Collection Info Sent", 200, Program.mWebApi);
                return new ObjectResult(collection) { StatusCode = 200 };
            }

            args.SendEvent("Collection not found", 404, Program.mWebApi);
            return new ObjectResult("Collection not found") { StatusCode = 404 };
        }

        /// <summary>
        /// Search for collections.
        /// </summary>
        /// <param name="page">The page of results to get, based on count.</param>
        /// <param name="count">The number of results to get.</param>
        /// <param name="name">The name of the collection.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin, User, Pass
        /// - **User Specific**: Collections can only be viewed by the owner of the collection and any viewers set, unless the collection's 'PublicViewing' parameter is true.
        /// </remarks>
        /// <returns>A list of collections.</returns>
        /// <response code="200">On successfully getting the collection info.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If Collection cannot be found.</response>
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("search")]
        public ObjectResult SearchCollections(int page, int count, string name="")
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/collections/search", curId, new Dictionary<string, object>()
            {
                { "page", page },
                { "count", count },
                { "name", name }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ColCollection = mongoDatabase.GetCollection<Collection>("Collections");

            List<ResponseCollection> Collections = new List<ResponseCollection>();

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


            args.SendEvent("List of Collections sent", 200, Program.mWebApi);
            return new ObjectResult(Collections) { StatusCode = 200 };
        }

        /// <summary>
        /// Get the tracks from a collection.
        /// </summary>
        /// <param name="id">The unique identifier of the collection.</param>
        /// <param name="page">The page of results to get, based on count.</param>
        /// <param name="count">The number of results to get.</param>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin, User, Pass
        /// - **User Specific**: Collections can only be viewed by the owner of the collection and any viewers set, unless the collection's 'PublicViewing' parameter is true.
        /// </remarks>
        /// <returns>A list of Tracks.</returns>
        /// <response code="200">On successfully getting the collection tracks.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="404">If Collection cannot be found.</response>
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("get-tracks")]
        public ObjectResult GetTracks(string id, int page = 0, int count = 100)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/collections/get-tracks", curId, new Dictionary<string, object>()
            {
                { "id", id },
                { "page", page },
                { "count", count },
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ColCollection = mongoDatabase.GetCollection<Collection>("Collections");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var cFilter = Builders<Collection>.Filter.Eq(x => x._id, id);
            var collection = ColCollection.Find(cFilter).FirstOrDefault();
            if (collection == null)
            {
                args.SendEvent("Collection not found", 404, Program.mWebApi);
                return new ObjectResult("Collection not found") { StatusCode = 404 };
            }

            if (collection.PublicEditing == false)
            {
                if (collection.Owner != curId && !collection.Editors.Contains(curId) && !collection.Viewers.Contains(curId))
                {
                    args.SendEvent("Invalid Auth", 401, Program.mWebApi);
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

            args.SendEvent("Collection Tracks Sent", 200, Program.mWebApi);
            return new ObjectResult(orderedTracks) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("get-albums")]
        public ObjectResult GetAlbums(string id, int page = 0, int count = 100)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/palylists/get-tracks", curId, new Dictionary<string, object>()
                {
                        { "id", id },
                        { "page", page },
                        { "count", count }
                });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var CCollection = mongoDatabase.GetCollection<Collection>("Collections");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");

            var cFilter = Builders<Collection>.Filter.Eq(x => x._id, id);


            var collection = CCollection.Find(cFilter).FirstOrDefault();
            if (collection == null)
            {
                args.SendEvent("Collection not found", 404, Program.mWebApi);
                return new ObjectResult("Collection not found") { StatusCode = 404 };
            }

            if (collection.PublicEditing == false)
            {
                if (collection.Owner != curId && !collection.Editors.Contains(curId) && !collection.Viewers.Contains(curId))
                {
                    args.SendEvent("Invalid Auth", 401, Program.mWebApi);
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            var tracks = collection.Tracks;

            var trackProjection = Builders<Track>.Projection.Include(x => x.Album._id);
            List<string> albumIds = TracksCollection.Find(Builders<Track>.Filter.In(x => x._id, tracks.Select(x => x._id)))
                                                    .Project(trackProjection).ToList().Select(x => (string)x["Album"]["_id"]).Distinct().ToList();

            var albumProjection = Builders<Album>.Projection.Exclude(x => x.AlbumArtPaths)
                                                            .Exclude(x => x.Tracks);
            List<ResponseAlbum> albums = AlbumsCollection.Find(Builders<Album>.Filter.In(x => x._id, albumIds))
                                                         .Project(albumProjection).ToList().Select(x => BsonSerializer.Deserialize<ResponseAlbum>(x))
                                                         .Take(new Range(page * count, (page * count) + count)).ToList();

            var userIds = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            userIds.Add(curId);

            List<ResponseAlbum> orderedAlbums = new List<ResponseAlbum>();
            foreach (var album in albums)
            {

                // Check for null or empty collections to avoid exceptions
                if (album.PlayCounts != null)
                {
                    album.PlayCounts = album.PlayCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                }

                if (album.SkipCounts != null)
                {
                    album.SkipCounts = album.SkipCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                }

                if (album.Ratings != null)
                {
                    album.Ratings = album.Ratings.Where(x => userIds.Contains(x.UserId)).ToList();
                }

                orderedAlbums.Add(album);
            }

            args.SendEvent("Tracks Sent", 200, Program.mWebApi);
            return new ObjectResult(orderedAlbums) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("get-artists")]
        public ObjectResult GetArtists(string id, int page = 0, int count = 100)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/palylists/get-tracks", curId, new Dictionary<string, object>()
                {
                        { "id", id },
                        { "page", page },
                        { "count", count }
                });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var CCollection = mongoDatabase.GetCollection<Collection>("Collections");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");

            var cFilter = Builders<Collection>.Filter.Eq(x => x._id, id);


            var collection = CCollection.Find(cFilter).FirstOrDefault();
            if (collection == null)
            {
                args.SendEvent("Collection not found", 404, Program.mWebApi);
                return new ObjectResult("Collection not found") { StatusCode = 404 };
            }

            if (collection.PublicEditing == false)
            {
                if (collection.Owner != curId && !collection.Editors.Contains(curId) && !collection.Viewers.Contains(curId))
                {
                    args.SendEvent("Invalid Auth", 401, Program.mWebApi);
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            var tracks = collection.Tracks;

            var trackProjection = Builders<Track>.Projection.Include(x => x.TrackArtists);
            List<string> artistIds = TracksCollection.Find(Builders<Track>.Filter.In(x => x._id, tracks.Select(x => x._id)))
                                                     .Project(trackProjection).ToList().SelectMany(x => BsonSerializer.Deserialize<Track>(x).TrackArtists)
                                                     .Select(x => x._id).Distinct().ToList();

            var artistProjection = Builders<Artist>.Projection.Exclude(x => x.ArtistBannerPaths)
                                                              .Exclude(x => x.ArtistPfpPaths)
                                                              .Exclude(x => x.Releases)
                                                              .Exclude(x => x.SeenOn)
                                                              .Exclude(x => x.Tracks)
                                                              .Exclude(x => x.ConnectedArtists);
            List<ResponseArtist> artists = ArtistsCollection.Find(Builders<Artist>.Filter.In(x => x._id, artistIds))
                                                           .Project(artistProjection).ToList().Select(x => BsonSerializer.Deserialize<ResponseArtist>(x))
                                                           .Take(new Range(page * count, (page * count) + count)).ToList();

            var userIds = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            userIds.Add(curId);

            List<ResponseArtist> orderedArtists = new List<ResponseArtist>();
            foreach (var artist in artists)
            {

                // Check for null or empty collections to avoid exceptions
                if (artist.PlayCounts != null)
                {
                    artist.PlayCounts = artist.PlayCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                }

                if (artist.SkipCounts != null)
                {
                    artist.SkipCounts = artist.SkipCounts.Where(x => userIds.Contains(x.UserId)).ToList();
                }

                if (artist.Ratings != null)
                {
                    artist.Ratings = artist.Ratings.Where(x => userIds.Contains(x.UserId)).ToList();
                }

                orderedArtists.Add(artist);
            }

            args.SendEvent("Tracks Sent", 200, Program.mWebApi);
            return new ObjectResult(orderedArtists) { StatusCode = 200 };
        }
    }
}
