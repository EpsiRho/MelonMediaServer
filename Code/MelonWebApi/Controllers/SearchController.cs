using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;
using System.Data;
using MongoDB.Driver;
using Melon.LocalClasses;
using Melon.Models;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/search")]
    public class SearchController : ControllerBase
    {
        private readonly ILogger<SearchController> _logger;

        public SearchController(ILogger<SearchController> logger)
        {
            _logger = logger;
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("tracks")]
        public ObjectResult SearchTracks(int page = 0, int count = 100, string trackName = "", string format = "", string bitrate = "", 
                                         string sampleRate = "", string channels = "", string bitsPerSample = "", string year = "", 
                                         long ltPlayCount = -1, long gtPlayCount = -1, long ltSkipCount = -1, long gtSkipCount = -1, int ltYear = -1, int ltMonth = -1, int ltDay = -1,
                                         int gtYear = -1, int gtMonth = -1, int gtDay = -1, long ltRating = -1, long gtRating = -1, [FromQuery] List<string> genres = null, 
                                         bool searchOr = false, string sort = "NameAsc", string albumName = "", string artistName = "")
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/search/tracks", curId, new Dictionary<string, object>()
                {
                    { "page", page },
                    { "count", count },
                    { "trackName", trackName },
                    { "format", format },
                    { "bitrate", bitrate },
                    { "sampleRate", sampleRate },
                    { "channels", channels },
                    { "bitsPerSample", bitsPerSample },
                    { "year", year },
                    { "ltPlayCount", ltPlayCount },
                    { "gtPlayCount", gtPlayCount },
                    { "ltSkipCount", ltSkipCount },
                    { "gtSkipCount", gtSkipCount },
                    { "ltYear", ltYear },
                    { "ltMonth", ltMonth },
                    { "ltDay", ltDay },
                    { "gtYear", gtYear },
                    { "gtMonth", gtMonth },
                    { "gtDay", gtDay },
                    { "ltRating", ltRating },
                    { "gtRating", gtRating },
                    { "genres", genres },
                    { "searchOr", searchOr },
                    { "sort", sort },
                });

            List<ResponseTrack> tracks = new List<ResponseTrack>();

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            List<FilterDefinition<Track>> filterList = new List<FilterDefinition<Track>>();
            if (trackName != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex(x=>x.Name, new BsonRegularExpression(Regex.Escape(trackName), "i")));
            }

            if (format != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex(x=>x.Format, new BsonRegularExpression(Regex.Escape(format), "i")));
            }

            if (bitrate != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex(x=>x.Bitrate, new BsonRegularExpression(Regex.Escape(bitrate), "i")));
            }

            if (sampleRate != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex(x=>x.SampleRate, new BsonRegularExpression(Regex.Escape(sampleRate), "i")));
            }

            if (channels != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex(x=>x.Channels, new BsonRegularExpression(Regex.Escape(channels), "i")));
            }

            if (bitsPerSample != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex(x => x.BitsPerSample, new BsonRegularExpression(Regex.Escape(bitsPerSample), "i")));
            }

            if (year != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex(x => x.Year, new BsonRegularExpression(Regex.Escape(year), "i")));
            }

            if (albumName != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex(x => x.Album.Name, new BsonRegularExpression(Regex.Escape(albumName), "i")));
            }

            if (artistName != "")
            {
                var f = Builders<DbLink>.Filter.And(Builders<DbLink>.Filter.Eq(x => x.Name, artistName), Builders<DbLink>.Filter.Regex(x => x.Name, new BsonRegularExpression(Regex.Escape(albumName), "i")));
                filterList.Add(Builders<Track>.Filter.ElemMatch(x => x.TrackArtists, f));
            }

            // Play Count
            if (gtPlayCount >= 0)
            {
                var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, curId), Builders<UserStat>.Filter.Gt(x => x.Value, gtPlayCount));
                filterList.Add(Builders<Track>.Filter.ElemMatch(x => x.PlayCounts, f));
            }
            
            if (ltPlayCount >= 0)
            {
                var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, curId), Builders<UserStat>.Filter.Lt(x => x.Value, ltPlayCount));
                filterList.Add(Builders<Track>.Filter.ElemMatch(x => x.PlayCounts, f));
            }

            // Skip Count
            if (gtSkipCount >= 0)
            {
                var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, curId), Builders<UserStat>.Filter.Gt(x => x.Value, gtSkipCount));
                filterList.Add(Builders<Track>.Filter.ElemMatch(x => x.SkipCounts, f));
            }
            
            if (ltSkipCount >= 0)
            {
                var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, curId), Builders<UserStat>.Filter.Lt(x => x.Value, ltSkipCount));
                filterList.Add(Builders<Track>.Filter.ElemMatch(x => x.SkipCounts, f));
            }

            // Ratings
            if (gtRating >= 0)
            {
                var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, curId), Builders<UserStat>.Filter.Gt(x => x.Value, gtRating));
                filterList.Add(Builders<Track>.Filter.ElemMatch(x => x.Ratings, f));
            }

            if (ltRating >= 0)
            {
                var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, curId), Builders<UserStat>.Filter.Lt(x => x.Value, ltRating));
                filterList.Add(Builders<Track>.Filter.ElemMatch(x => x.Ratings, f));
            }

            // Date
            if (ltYear >= 0 && ltMonth >= 0 && ltDay >= 0)
            {
                DateTime ltDateTime = new DateTime(ltYear, ltMonth, ltDay);
                filterList.Add(Builders<Track>.Filter.Lt(x => x.ReleaseDate, ltDateTime));
            }

            if (gtYear >= 0 && gtMonth >= 0 && gtDay >= 0)
            {
                DateTime gtDateTime = new DateTime(gtYear, gtMonth, gtDay);
                filterList.Add(Builders<Track>.Filter.Gt(x => x.ReleaseDate, gtDateTime));
            }

            if (genres != null)
            {
                foreach (var genre in genres)
                {
                    filterList.Add(Builders<Track>.Filter.Regex(x=>x.TrackGenres, new BsonRegularExpression(Regex.Escape(genre), "i")));
                }
            }

            FilterDefinition<Track> combinedFilter = null;
            foreach(var filter in filterList)
            {
                if(combinedFilter == null)
                {
                    combinedFilter = filter;
                }
                else
                {
                    if (searchOr)
                    {
                        combinedFilter = Builders<Track>.Filter.Or(combinedFilter, filter);
                    }
                    else
                    {
                        combinedFilter = Builders<Track>.Filter.And(combinedFilter, filter);
                    }
                }
            }
            if(combinedFilter == null)
            {
                combinedFilter = Builders<Track>.Filter.Empty;
            }

            SortDefinition<Track> sortDefinition = null;
            bool needsAggregate = false;
            switch (sort)
            {
                case "NameDesc":
                    sortDefinition = Builders<Track>.Sort.Descending(x=>x.Name);
                    break;
                case "NameAsc":
                    sortDefinition = Builders<Track>.Sort.Ascending(x => x.Name);
                    break;
                case "DateAddedDesc":
                    sortDefinition = Builders<Track>.Sort.Descending(x => x.DateAdded);
                    break;
                case "DateAddedAsc":
                    sortDefinition = Builders<Track>.Sort.Ascending(x => x.DateAdded);
                    break;
                case "ReleaseDateDesc":
                    sortDefinition = Builders<Track>.Sort.Descending(x => x.ReleaseDate);
                    break;
                case "ReleaseDateAsc":
                    sortDefinition = Builders<Track>.Sort.Ascending(x => x.ReleaseDate);
                    break;
                case "PlayCountDesc":
                    needsAggregate = true;
                    break;
                case "PlayCountAsc":
                    needsAggregate = true;
                    break;
            }

            var trackProjection = Builders<Track>.Projection.Exclude(x => x.Path)
                                                            .Exclude(x => x.LyricsPath);
            var projection = Builders<BsonDocument>.Projection.Include("Album")
                    .Include("Position")
                    .Include("Disc")
                    .Include("Format")
                    .Include("Bitrate")
                    .Include("SampleRate")
                    .Include("Channels")
                    .Include("BitsPerSample")
                    .Include("MusicBrainzID")
                    .Include("ISRC")
                    .Include("Year")
                    .Include("Name")
                    .Include("Duration")
                    .Include("nextTrack")
                    .Include("PlayCounts")
                    .Include("SkipCounts")
                    .Include("Ratings")
                    .Include("TrackArtCount")
                    .Include("TrackArtDefault")
                    .Include("ServerURL")
                    .Include("LastModified")
                    .Include("DateAdded")
                    .Include("ReleaseDate")
                    .Include("Chapters")
                    .Include("TrackGenres")
                    .Include("TrackArtists")
                    .Include("_id");

            List<ResponseTrack> trackDocs = new List<ResponseTrack>();
            if (needsAggregate)
            {
                combinedFilter = Builders<Track>.Filter.And(combinedFilter, Builders<Track>.Filter.ElemMatch(x => x.PlayCounts, Builders<UserStat>.Filter.Eq(x => x.UserId, curId)));
                var responseTrackProjection = new BsonDocument {
                    { "_id", 1 },
                    { "Album", 1 },
                    { "Position", 1 },
                    { "Disc", 1 },
                    { "Format", 1 },
                    { "Bitrate", 1 },
                    { "SampleRate", 1 },
                    { "Channels", 1 },
                    { "BitsPerSample", 1 },
                    { "MusicBrainzID", 1 },
                    { "ISRC", 1 },
                    { "Year", 1 },
                    { "Name", 1 },
                    { "Duration", 1 },
                    { "nextTrack", 1 },
                    { "SkipCounts", 1 },
                    { "Ratings", 1 },
                    { "TrackArtCount", 1 },
                    { "TrackArtDefault", 1 },
                    { "ServerURL", 1 },
                    { "LastModified", 1 },
                    { "DateAdded", 1 },
                    { "ReleaseDate", 1 },
                    { "Chapters", 1 },
                    { "TrackGenres", 1 },
                    { "TrackArtists", 1 },
                    { "PlayCounts", 1 }, 
                    { "SortValue", new BsonDocument("$let", new BsonDocument {
                        { "vars", new BsonDocument("filteredItems", new BsonDocument("$filter", new BsonDocument {
                            { "input", "$PlayCounts" },
                            { "as", "item" },
                            { "cond", new BsonDocument("$eq", new BsonArray { "$$item.UserId", curId }) }
                        })) },
                        { "in", new BsonDocument("$arrayElemAt", new BsonArray { "$$filteredItems.Value", 0 }) }
                    }) }
                };
                switch (sort)
                {
                    case "PlayCountDesc":
                        var pipeline = TracksCollection.Aggregate()
                                        .Match(combinedFilter)
                                        .Project(responseTrackProjection)
                                        .Sort(new BsonDocument("SortValue", -1))
                                        .Skip(page * count)
                                        .Limit(count)
                                        .Project<ResponseTrack>(projection);

                        trackDocs = pipeline.ToList();
                        break;
                    case "PlayCountAsc":
                        var pipelineAsc = TracksCollection.Aggregate()
                                        .Match(combinedFilter)
                                        .Project(responseTrackProjection)
                                        .Sort(new BsonDocument("SortValue", 1))
                                        .Skip(page * count)
                                        .Limit(count)
                                        .Project<ResponseTrack>(projection);

                        trackDocs = pipelineAsc.ToList();
                        break;
                }
            }
            else
            {
                trackDocs = TracksCollection.Find(combinedFilter, new FindOptions() { Collation = new Collation("en", strength: CollationStrength.Secondary) })
                                                .Project(trackProjection)
                                                .Sort(sortDefinition)
                                                .Skip(page * count)
                                                .Limit(count)
                                                .ToList()
                                                .Select(x => BsonSerializer.Deserialize<ResponseTrack>(x))
                                                .ToList();
            }
            tracks.AddRange(trackDocs);

            var userIds = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            userIds.Add(curId);

            foreach (var track in tracks)
            {
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
            }

            args.SendEvent("Tracks sent", 200, Program.mWebApi);
            return new ObjectResult(tracks) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("albums")]
        public ObjectResult SearchAlbums (int page = 0, int count = 100, string albumName = "", string publisher = "", string releaseType = "", string releaseStatus = "",
                                          long ltPlayCount = -1, long gtPlayCount = -1, long ltRating = -1, long gtRating = -1, int ltYear = -1, int ltMonth = -1, int ltDay = -1,
                                          int gtYear = -1, int gtMonth = -1, int gtDay = -1, [FromQuery] List<string> genres = null, bool searchOr = false, string sort = "NameAsc")
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/search/albums", curId, new Dictionary<string, object>()
                {
                    { "page", page },
                    { "count", count },
                    { "albumName", albumName },
                    { "publisher", publisher },
                    { "releaseType", releaseType },
                    { "releaseStatus", releaseStatus },
                    { "ltPlayCount", ltPlayCount },
                    { "gtPlayCount", gtPlayCount },
                    { "ltYear", ltYear },
                    { "ltMonth", ltMonth },
                    { "ltDay", ltDay },
                    { "gtYear", gtYear },
                    { "gtMonth", gtMonth },
                    { "gtDay", gtDay },
                    { "ltRating", ltRating },
                    { "gtRating", gtRating },
                    { "genres", genres },
                    { "searchOr", searchOr },
                    { "sort", sort },
                });

            List<ResponseAlbum> albums = new List<ResponseAlbum>();
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            List<FilterDefinition<Album>> filterList = new List<FilterDefinition<Album>>();

            if (albumName != "")
            {
                filterList.Add(Builders<Album>.Filter.Regex(x=>x.Name, new BsonRegularExpression(Regex.Escape(albumName), "i")));
            }

            if (publisher != "")
            {
                filterList.Add(Builders<Album>.Filter.Regex(x=>x.Publisher, new BsonRegularExpression(Regex.Escape(publisher), "i")));
            }

            if (releaseType != "")
            {
                filterList.Add(Builders<Album>.Filter.Regex(x=>x.ReleaseType, new BsonRegularExpression(Regex.Escape(releaseType), "i")));
            }

            if (releaseType != "")
            {
                filterList.Add(Builders<Album>.Filter.Regex(x=>x.ReleaseStatus, new BsonRegularExpression(Regex.Escape(releaseStatus), "i")));
            }

            // Play Count
            if (gtPlayCount >= 0)
            {
                var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, curId), Builders<UserStat>.Filter.Gt(x => x.Value, gtPlayCount));
                filterList.Add(Builders<Album>.Filter.ElemMatch(x => x.PlayCounts, f));
            }

            if (ltPlayCount >= 0)
            {
                var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, curId), Builders<UserStat>.Filter.Lt(x => x.Value, ltPlayCount));
                filterList.Add(Builders<Album>.Filter.ElemMatch(x => x.PlayCounts, f));
            }

            // Ratings
            if (gtRating >= 0)
            {
                var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, curId), Builders<UserStat>.Filter.Gt(x => x.Value, gtRating));
                filterList.Add(Builders<Album>.Filter.ElemMatch(x => x.Ratings, f));
            }

            if (ltRating >= 0)
            {
                var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, curId), Builders<UserStat>.Filter.Lt(x => x.Value, ltRating));
                filterList.Add(Builders<Album>.Filter.ElemMatch(x => x.Ratings, f));
            }

            // Date
            if (ltYear >= 0 && ltMonth >= 0 && ltDay >= 0)
            {
                DateTime ltDateTime = new DateTime(ltYear, ltMonth, ltDay);
                filterList.Add(Builders<Album>.Filter.Lt(x => x.ReleaseDate, ltDateTime));
            }

            if (gtYear >= 0 && gtMonth >= 0 && gtDay >= 0)
            {
                DateTime gtDateTime = new DateTime(gtYear, gtMonth, gtDay);
                filterList.Add(Builders<Album>.Filter.Gt(x => x.ReleaseDate, gtDateTime));
            }

            if (genres != null)
            {
                foreach (var genre in genres)
                {
                    filterList.Add(Builders<Album>.Filter.Regex(x=>x.AlbumGenres, new BsonRegularExpression(Regex.Escape(genre), "i")));
                }
            }

            FilterDefinition<Album> combinedFilter = null;
            foreach (var filter in filterList)
            {
                if (combinedFilter == null)
                {
                    combinedFilter = filter;
                }
                else
                {
                    if (searchOr)
                    {
                        combinedFilter = Builders<Album>.Filter.Or(combinedFilter, filter);
                    }
                    else
                    {
                        combinedFilter = Builders<Album>.Filter.And(combinedFilter, filter);
                    }
                }
            }
            if (combinedFilter == null)
            {
                combinedFilter = Builders<Album>.Filter.Empty;
            }

            SortDefinition<Album> sortDefinition = null;
            bool needsAggregate = false;
            switch (sort)
            {
                case "NameDesc":
                    sortDefinition = Builders<Album>.Sort.Descending(x => x.Name);
                    break;
                case "NameAsc":
                    sortDefinition = Builders<Album>.Sort.Ascending(x => x.Name);
                    break;
                case "DateAddedDesc":
                    sortDefinition = Builders<Album>.Sort.Descending(x => x.DateAdded);
                    break;
                case "DateAddedAsc":
                    sortDefinition = Builders<Album>.Sort.Ascending(x => x.DateAdded);
                    break;
                case "ReleaseDateDesc":
                    sortDefinition = Builders<Album>.Sort.Descending(x => x.ReleaseDate);
                    break;
                case "ReleaseDateAsc":
                    sortDefinition = Builders<Album>.Sort.Ascending(x => x.ReleaseDate);
                    break;
                case "PlayCountDesc":
                    needsAggregate = true;
                    break;
                case "PlayCountAsc":
                    needsAggregate = true;
                    break;
            }

            var albumProjection = Builders<Album>.Projection.Exclude(x => x.AlbumArtPaths)
                                                            .Exclude(x => x.Tracks);
            var projection = Builders<BsonDocument>.Projection
                .Include("_id")
                .Include("TotalDiscs")
                .Include("TotalTracks")
                .Include("Name")
                .Include("Bio")
                .Include("Publisher")
                .Include("ReleaseStatus")
                .Include("ReleaseType")
                .Include("PlayCounts")
                .Include("SkipCounts")
                .Include("Ratings")
                .Include("ServerURL")
                .Include("DateAdded")
                .Include("ReleaseDate")
                .Include("AlbumArtCount")
                .Include("AlbumArtDefault")
                .Include("AlbumGenres")
                .Include("AlbumArtists")
                .Include("ContributingArtists");


            List<ResponseAlbum> albumDocs = new List<ResponseAlbum>();
            if (needsAggregate)
            {
                combinedFilter = Builders<Album>.Filter.And(combinedFilter, Builders<Album>.Filter.ElemMatch(x => x.PlayCounts, Builders<UserStat>.Filter.Eq(x => x.UserId, curId)));
                var responseAlbumProjection = new BsonDocument {
                    { "_id", 1 },
                    { "TotalDiscs", 1 },
                    { "TotalTracks", 1 },
                    { "Name", 1 },
                    { "Bio", 1 },
                    { "Publisher", 1 },
                    { "ReleaseStatus", 1 },
                    { "ReleaseType", 1 },
                    { "SkipCounts", 1 },
                    { "Ratings", 1 },
                    { "ServerURL", 1 },
                    { "DateAdded", 1 },
                    { "ReleaseDate", 1 },
                    { "AlbumArtCount", 1 },
                    { "AlbumArtDefault", 1 },
                    { "AlbumGenres", 1 },
                    { "AlbumArtists", 1 },
                    { "ContributingArtists", 1 },
                    { "PlayCounts", 1 }, 
                    { "SortValue", new BsonDocument("$let", new BsonDocument {
                        { "vars", new BsonDocument("filteredItems", new BsonDocument("$filter", new BsonDocument {
                            { "input", "$PlayCounts" },
                            { "as", "item" },
                            { "cond", new BsonDocument("$eq", new BsonArray { "$$item.UserId", curId }) }
                        })) },
                        { "in", new BsonDocument("$arrayElemAt", new BsonArray { "$$filteredItems.Value", 0 }) }
                    }) } 
                };
                switch (sort)
                {
                    case "PlayCountDesc":
                        var pipeline = AlbumCollection.Aggregate()
                                        .Project(responseAlbumProjection)
                                        .Sort(new BsonDocument("SortValue", -1))
                                        .Skip(page * count)
                                        .Limit(count)
                                        .Project<ResponseAlbum>(projection);

                        albumDocs = pipeline.ToList();
                        break;
                    case "PlayCountAsc":
                        var pipelineAsc = AlbumCollection.Aggregate()
                                        .Project(responseAlbumProjection)
                                        .Sort(new BsonDocument("SortValue", 1))
                                        .Skip(page * count)
                                        .Limit(count)
                                        .Project<ResponseAlbum>(projection);

                        albumDocs = pipelineAsc.ToList();
                        break;
                }
            }
            else
            {
                albumDocs = AlbumCollection.Find(combinedFilter, new FindOptions() { Collation = new Collation("en", strength: CollationStrength.Secondary) })
                                           .Project(albumProjection)
                                           .Sort(sortDefinition)
                                           .Skip(page * count)
                                           .Limit(count)
                                           .ToList()
                                           .Select(x => BsonSerializer.Deserialize<ResponseAlbum>(x))
                                           .ToList();
            }

            albums.AddRange(albumDocs);

            // Initialize usernames as a HashSet for better performance
            var userIds = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            userIds.Add(curId);

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
            }

            args.SendEvent("Albums sent", 200, Program.mWebApi);
            return new ObjectResult(albums) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artists")]
        public ObjectResult SearchArtists(int page = 0, int count = 100, string artistName = "", long ltPlayCount = -1, long gtPlayCount = -1, long ltRating = -1, 
                                          long gtRating = -1, [FromQuery] List<string> genres = null, bool searchOr = false, string sort = "NameAsc")
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/search/artists", curId, new Dictionary<string, object>()
                {
                    { "page", page },
                    { "count", count },
                    { "artistName", artistName },
                    { "ltPlayCount", ltPlayCount },
                    { "gtPlayCount", gtPlayCount },
                    { "ltRating", ltRating },
                    { "gtRating", gtRating },
                    { "genres", genres },
                    { "searchOr", searchOr },
                    { "sort", sort },
                });

            List<ResponseArtist> artists = new List<ResponseArtist>();
            
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            List<FilterDefinition<Artist>> filterList = new List<FilterDefinition<Artist>>();
            if (artistName != "")
            {
                filterList.Add(Builders<Artist>.Filter.Regex(x=>x.Name, new BsonRegularExpression(Regex.Escape(artistName), "i")));
            }

            if (gtPlayCount >= 0)
            {
                var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, curId), Builders<UserStat>.Filter.Gt(x => x.Value, gtPlayCount));
                filterList.Add(Builders<Artist>.Filter.ElemMatch(x => x.PlayCounts, f));
            }

            if (ltPlayCount >= 0)
            {
                var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, curId), Builders<UserStat>.Filter.Lt(x => x.Value, ltPlayCount));
                filterList.Add(Builders<Artist>.Filter.ElemMatch(x => x.PlayCounts, f));
            }

            if (gtRating >= 0)
            {
                var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, curId), Builders<UserStat>.Filter.Gt(x => x.Value, gtRating));
                filterList.Add(Builders<Artist>.Filter.ElemMatch(x => x.PlayCounts, f));
            }

            if (ltRating >= 0)
            {
                var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, curId), Builders<UserStat>.Filter.Lt(x => x.Value, ltRating));
                filterList.Add(Builders<Artist>.Filter.ElemMatch(x => x.PlayCounts, f));
            }

            if (genres != null)
            {
                foreach (var genre in genres)
                {
                    filterList.Add(Builders<Artist>.Filter.Regex(x=>x.Genres, new BsonRegularExpression(Regex.Escape(genre), "i")));
                }
            }

            FilterDefinition<Artist> combinedFilter = null;
            foreach (var filter in filterList)
            {
                if (combinedFilter == null)
                {
                    combinedFilter = filter;
                }
                else
                {
                    if (searchOr)
                    {
                        combinedFilter = Builders<Artist>.Filter.Or(combinedFilter, filter);
                    }
                    else
                    {
                        combinedFilter = Builders<Artist>.Filter.And(combinedFilter, filter);
                    }
                }
            }
            if (combinedFilter == null)
            {
                combinedFilter = Builders<Artist>.Filter.Empty;
            }

            SortDefinition<Artist> sortDefinition = null;
            bool needsAggregate = false;
            switch (sort)
            {
                case "NameDesc":
                    sortDefinition = Builders<Artist>.Sort.Descending(x => x.Name);
                    break;
                case "NameAsc":
                    sortDefinition = Builders<Artist>.Sort.Ascending(x => x.Name);
                    break;
                case "DateAddedDesc":
                    sortDefinition = Builders<Artist>.Sort.Descending(x => x.DateAdded);
                    break;
                case "DateAddedAsc":
                    sortDefinition = Builders<Artist>.Sort.Ascending(x => x.DateAdded);
                    break;
                case "PlayCountDesc":
                    needsAggregate = true;
                    break;
                case "PlayCountAsc":
                    needsAggregate = true;
                    break;
            }

            var artistProjection = Builders<Artist>.Projection.Exclude(x => x.ArtistBannerPaths)
                                                              .Exclude(x => x.ArtistPfpPaths)
                                                              .Exclude(x => x.Releases)
                                                              .Exclude(x => x.SeenOn)
                                                              .Exclude(x => x.Tracks)
                                                              .Exclude(x => x.ConnectedArtists);
            var projection = Builders<BsonDocument>.Projection
                .Include("_id")
                .Include("Name")
                .Include("Bio")
                .Include("ArtistPfpArtCount")
                .Include("ArtistBannerArtCount")
                .Include("ArtistPfpDefault")
                .Include("ArtistBannerArtDefault")
                .Include("PlayCounts")
                .Include("SkipCounts")
                .Include("Ratings")
                .Include("ServerURL")
                .Include("Genres")
                .Include("DateAdded");



            List<ResponseArtist> ArtistDocs = new List<ResponseArtist>();
            if (needsAggregate)
            {
                combinedFilter = Builders<Artist>.Filter.And(combinedFilter, Builders<Artist>.Filter.ElemMatch(x => x.PlayCounts, Builders<UserStat>.Filter.Eq(x => x.UserId, curId)));
                var responseAlbumProjection = new BsonDocument {
                    { "_id", 1 },
                    { "Name", 1 },
                    { "Bio", 1 },
                    { "ArtistPfpArtCount", 1 },
                    { "ArtistBannerArtCount", 1 },
                    { "ArtistPfpDefault", 1 },
                    { "ArtistBannerArtDefault", 1 },
                    { "SkipCounts", 1 },
                    { "Ratings", 1 },
                    { "ServerURL", 1 },
                    { "Genres", 1 },
                    { "DateAdded", 1 },
                    { "PlayCounts", 1 },
                    { "SortValue", new BsonDocument("$let", new BsonDocument {
                        { "vars", new BsonDocument("filteredItems", new BsonDocument("$filter", new BsonDocument {
                            { "input", "$PlayCounts" },
                            { "as", "item" },
                            { "cond", new BsonDocument("$eq", new BsonArray { "$$item.UserId", curId }) }
                        })) },
                        { "in", new BsonDocument("$arrayElemAt", new BsonArray { "$$filteredItems.Value", 0 }) }
                    }) }
                };
                switch (sort)
                {
                    case "PlayCountDesc":
                        var pipeline = ArtistCollection.Aggregate()
                                        .Project(responseAlbumProjection)
                                        .Sort(new BsonDocument("SortValue", -1))
                                        .Skip(page * count)
                                        .Limit(count)
                                        .Project<ResponseArtist>(projection);

                        ArtistDocs = pipeline.ToList();
                        break;
                    case "PlayCountAsc":
                        var pipelineAsc = ArtistCollection.Aggregate()
                                        .Project(responseAlbumProjection)
                                        .Sort(new BsonDocument("SortValue", 1))
                                        .Skip(page * count)
                                        .Limit(count)
                                        .Project<ResponseArtist>(projection);

                        ArtistDocs = pipelineAsc.ToList();
                        break;
                }
            }
            else
            {
                ArtistDocs = ArtistCollection.Find(combinedFilter, new FindOptions() { Collation = new Collation("en", strength: CollationStrength.Secondary) })
                                             .Project(artistProjection)
                                             .Sort(sortDefinition)
                                             .Skip(page * count)
                                             .Limit(count)
                                             .ToList()
                                             .Select(x => BsonSerializer.Deserialize<ResponseArtist>(x))
                                             .ToList();
            }

            artists.AddRange(ArtistDocs);

            var userIds = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x._id));
            userIds.Add(curId);

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
            }

            args.SendEvent("Artists sent", 200, Program.mWebApi);
            return new ObjectResult(artists) { StatusCode = 200 };
        }

    }
}