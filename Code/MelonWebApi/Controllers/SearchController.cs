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
        public ObjectResult SearchTracks(int page = 0, int count = 100, [FromQuery] List<string> andFilters = null, [FromQuery] List<string> orFilters = null, string sort = "NameAsc")
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/search/tracks", curId, new Dictionary<string, object>()
                {
                    { "page", page },
                    { "count", count },
                    { "andFilters", andFilters },
                    { "orFilters", orFilters },
                    { "sort", sort },
                });

            var tracks = MelonAPI.FindTracks(andFilters, orFilters, curId, page, count, sort);

            if(tracks == null || tracks.Count == 0)
            {
                args.SendEvent("No tracks found", 200, Program.mWebApi);
                return new ObjectResult(new List<ResponseTrack>()) { StatusCode = 200 };
            }

            var mongoDatabase = StateManager.DbClient.GetDatabase("Melon");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

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
        public ObjectResult SearchAlbums (int page = 0, int count = 100, [FromQuery] List<string> andFilters = null, [FromQuery] List<string> orFilters = null, string sort = "NameAsc")
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/search/albums", curId, new Dictionary<string, object>()
                {
                    { "page", page },
                    { "count", count },
                    { "andFilters", andFilters },
                    { "orFilters", orFilters },
                    { "sort", sort },
                });

            var albums = MelonAPI.FindAlbums(andFilters, orFilters, curId, page, count, sort);

            if (albums == null || albums.Count == 0)
            {
                args.SendEvent("No albums found", 200, Program.mWebApi);
                return new ObjectResult(new List<ResponseTrack>()) { StatusCode = 200 };
            }

            var mongoDatabase = StateManager.DbClient.GetDatabase("Melon");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

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
        public ObjectResult SearchArtists(int page = 0, int count = 100, [FromQuery] List<string> andFilters = null, [FromQuery] List<string> orFilters = null, string sort = "NameAsc")
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/search/artists", curId, new Dictionary<string, object>()
                {
                    { "page", page },
                    { "count", count },
                    { "andFilters", andFilters },
                    { "orFilters", orFilters },
                    { "sort", sort },
                });

            var artists = MelonAPI.FindArtists(andFilters, orFilters, curId, page, count, sort);

            if (artists == null || artists.Count == 0)
            {
                args.SendEvent("No artists found", 200, Program.mWebApi);
                return new ObjectResult(new List<ResponseTrack>()) { StatusCode = 200 };
            }

            var mongoDatabase = StateManager.DbClient.GetDatabase("Melon");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

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