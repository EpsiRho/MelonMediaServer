using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;
using System.Data;
using MongoDB.Driver;
using Melon.LocalClasses;
using System.Diagnostics;
using Melon.Models;
using RestSharp;
using ATL.Logging;
using System.Text.Json;
using Azure.Core;
using Newtonsoft.Json;
using System;
using Humanizer.Localisation;
using Humanizer.Bytes;
using System.Security.Policy;
using static Azure.Core.HttpHeader;
using System.Security.Claims;

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
                                         int gtYear = -1, int gtMonth = -1, int gtDay = -1, long ltRating = -1, long gtRating = -1, [FromQuery] string[] genres = null, 
                                         bool searchOr = false, string sort = "NameAsc")
        {
            List<ResponseTrack> tracks = new List<ResponseTrack>();

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            List<FilterDefinition<Track>> filterList = new List<FilterDefinition<Track>>();
            if (trackName != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex(x=>x.TrackName, new BsonRegularExpression(trackName, "i")));
            }

            if (format != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex(x=>x.Format, new BsonRegularExpression(format, "i")));
            }

            if (bitrate != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex(x=>x.Bitrate, new BsonRegularExpression(bitrate, "i")));
            }

            if (sampleRate != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex(x=>x.SampleRate, new BsonRegularExpression(sampleRate, "i")));
            }

            if (channels != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex(x=>x.Channels, new BsonRegularExpression(channels, "i")));
            }

            if (bitsPerSample != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex(x => x.BitsPerSample, new BsonRegularExpression(bitsPerSample, "i")));
            }

            if (year != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex(x => x.Year, new BsonRegularExpression(year, "i")));
            }

            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();

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
                    filterList.Add(Builders<Track>.Filter.Regex(x=>x.TrackGenres, new BsonRegularExpression(genre, "i")));
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

            SortDefinition<Track> sortDefinition = null;
            switch (sort)
            {
                case "NameDesc":
                    sortDefinition = Builders<Track>.Sort.Descending(x=>x.TrackName);
                    break;
                case "NameAsc":
                    sortDefinition = Builders<Track>.Sort.Ascending(x => x.TrackName);
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
            }

            var trackProjection = Builders<Track>.Projection.Exclude(x => x.Path)
                                                            .Exclude(x => x.LyricsPath);
            var trackDocs = TracksCollection.Find(combinedFilter)
                                            .Project(trackProjection)
                                            .Sort(sortDefinition)
                                            .Skip(page * count)
                                            .Limit(count)
                                            .ToList()
                                            .Select(x=>BsonSerializer.Deserialize<ResponseTrack>(x));
            tracks.AddRange(trackDocs);

            switch (sort)
            {
                case "NameDesc":
                    tracks = tracks.OrderByDescending(x => x.TrackName).ToList();
                    break;
                case "NameAsc":
                    tracks = tracks.OrderBy(x => x.TrackName).ToList();
                    break;
                case "DateAddedDesc":
                    tracks = tracks.OrderByDescending(x => x.DateAdded).ToList();
                    break;
                case "DateAddedAsc":
                    tracks = tracks.OrderBy(x => x.DateAdded).ToList();
                    break;
                case "ReleaseDateDesc":
                    tracks = tracks.OrderByDescending(x => x.ReleaseDate).ToList();
                    break;
                case "ReleaseDateAsc":
                    tracks = tracks.OrderBy(x => x.ReleaseDate).ToList();
                    break;
            }

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

            return new ObjectResult(tracks) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("albums")]
        public ObjectResult SearchAlbums (int page = 0, int count = 100, string albumName = "", string publisher = "", string releaseType = "", string releaseStatus = "",
                                          long ltPlayCount = -1, long gtPlayCount = -1, long ltRating = -1, long gtRating = -1, int ltYear = -1, int ltMonth = -1, int ltDay = -1,
                                          int gtYear = -1, int gtMonth = -1, int gtDay = -1, [FromQuery] string[] genres = null, bool searchOr = false, string sort = "NameAsc")
        {
            List<ResponseAlbum> albums = new List<ResponseAlbum>();
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            List<FilterDefinition<Album>> filterList = new List<FilterDefinition<Album>>();

            if (albumName != "")
            {
                filterList.Add(Builders<Album>.Filter.Regex(x=>x.AlbumName, new BsonRegularExpression(albumName, "i")));
            }

            if (publisher != "")
            {
                filterList.Add(Builders<Album>.Filter.Regex(x=>x.Publisher, new BsonRegularExpression(publisher, "i")));
            }

            if (releaseType != "")
            {
                filterList.Add(Builders<Album>.Filter.Regex(x=>x.ReleaseType, new BsonRegularExpression(releaseType, "i")));
            }

            if (releaseType != "")
            {
                filterList.Add(Builders<Album>.Filter.Regex(x=>x.ReleaseStatus, new BsonRegularExpression(releaseStatus, "i")));
            }

            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();


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
                    filterList.Add(Builders<Album>.Filter.Regex(x=>x.AlbumGenres, new BsonRegularExpression(genre, "i")));
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

            SortDefinition<Album> sortDefinition = null;
            switch (sort)
            {
                case "NameDesc":
                    sortDefinition = Builders<Album>.Sort.Descending(x => x.AlbumName);
                    break;
                case "NameAsc":
                    sortDefinition = Builders<Album>.Sort.Ascending(x => x.AlbumName);
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
            }

            var albumProjection = Builders<Album>.Projection.Exclude(x => x.AlbumArtPaths)
                                                            .Exclude(x => x.Tracks);
            var albumDocs = AlbumCollection.Find(combinedFilter)
                                           .Project(albumProjection)
                                           .Sort(sortDefinition)
                                           .Skip(page * count)
                                           .Limit(count)
                                           .ToList()
                                           .Select(x => BsonSerializer.Deserialize<ResponseAlbum>(x));

            albums.AddRange(albumDocs);

            switch (sort)
            {
                case "NameDesc":
                    albums = albums.OrderByDescending(x => x.AlbumName).ToList();
                    break;
                case "NameAsc":
                    albums = albums.OrderBy(x => x.AlbumName).ToList();
                    break;
                case "DateAddedDesc":
                    albums = albums.OrderByDescending(x => x.DateAdded).ToList();
                    break;
                case "DateAddedAsc":
                    albums = albums.OrderBy(x => x.DateAdded).ToList();
                    break;
                case "ReleaseDateDesc":
                    albums = albums.OrderByDescending(x => x.ReleaseDate).ToList();
                    break;
                case "ReleaseDateAsc":
                    albums = albums.OrderBy(x => x.ReleaseDate).ToList();
                    break;
            }

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

            return new ObjectResult(albums) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artists")]
        public ObjectResult SearchArtists(int page = 0, int count = 100, string artistName = "", long ltPlayCount = -1, long gtPlayCount = -1, long ltRating = -1, 
                                          long gtRating = -1, [FromQuery] string[] genres = null, bool searchOr = false, string sort = "NameAsc")
        {
            List<ResponseArtist> artists = new List<ResponseArtist>();
            
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            List<FilterDefinition<Artist>> filterList = new List<FilterDefinition<Artist>>();
            if (artistName != "")
            {
                filterList.Add(Builders<Artist>.Filter.Regex(x=>x.ArtistName, new BsonRegularExpression(artistName, "i")));
            }

            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();

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
                    filterList.Add(Builders<Artist>.Filter.Regex(x=>x.Genres, new BsonRegularExpression(genre, "i")));
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

            SortDefinition<Artist> sortDefinition = null;
            switch (sort)
            {
                case "NameDesc":
                    sortDefinition = Builders<Artist>.Sort.Descending(x => x.ArtistName);
                    break;
                case "NameAsc":
                    sortDefinition = Builders<Artist>.Sort.Ascending(x => x.ArtistName);
                    break;
                case "DateAddedDesc":
                    sortDefinition = Builders<Artist>.Sort.Descending(x => x.DateAdded);
                    break;
                case "DateAddedAsc":
                    sortDefinition = Builders<Artist>.Sort.Ascending(x => x.DateAdded);
                    break;
            }

            var artistProjection = Builders<Artist>.Projection.Exclude(x => x.ArtistBannerPaths)
                                                              .Exclude(x => x.ArtistPfpPaths)
                                                              .Exclude(x => x.Releases)
                                                              .Exclude(x => x.SeenOn)
                                                              .Exclude(x => x.Tracks)
                                                              .Exclude(x => x.ConnectedArtists);
            var ArtistDocs = ArtistCollection.Find(combinedFilter)
                                             .Project(artistProjection)
                                             .Sort(sortDefinition)
                                             .Skip(page * count)
                                             .Limit(count)
                                             .ToList()
                                             .Select(x => BsonSerializer.Deserialize<ResponseArtist>(x));

            artists.AddRange(ArtistDocs);

            switch (sort)
            {
                case "NameDesc":
                    artists = artists.OrderByDescending(x => x.ArtistName).ToList();
                    break;
                case "NameAsc":
                    artists = artists.OrderBy(x => x.ArtistName).ToList();
                    break;
                case "DateAddedDesc":
                    artists = artists.OrderByDescending(x => x.DateAdded).ToList();
                    break;
                case "DateAddedAsc":
                    artists = artists.OrderBy(x => x.DateAdded).ToList();
                    break;
            }

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

            return new ObjectResult(artists) { StatusCode = 200 };
        }

    }
}