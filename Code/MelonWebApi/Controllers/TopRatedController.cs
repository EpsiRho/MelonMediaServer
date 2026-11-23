using System.Security.Claims;
using Melon.LocalClasses;
using Melon.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace MelonWebApi.Controllers
{
    /// <summary>
    /// Server-wide favourites: tracks, albums and artists by their average rating across users. Only ratings that
    /// clients may see count — those of users with public stats, plus the caller's own — so a private user's ratings
    /// can't be read back from the ranking. Unrated items (no rating above 0) are left out. Items come back as the
    /// usual response models, their <c>Ratings</c> holding exactly the ratings the average was taken over.
    /// </summary>
    [ApiController]
    [Route("api/top-rated")]
    [Authorize(Roles = "Admin,User,Pass")]
    public class TopRatedController : ControllerBase
    {
        private string CurrentUser => ((ClaimsIdentity)User.Identity!).Claims
            .Where(c => c.Type == ClaimTypes.UserData).Select(c => c.Value).FirstOrDefault() ?? "";

        /// <param name="minRatings">Leave out items rated by fewer users than this (1 or more).</param>
        [HttpGet("tracks")]
        public IActionResult Tracks(int page = 0, int count = 50, int minRatings = 1) =>
            Ranked<ResponseTrack>("api/top-rated/tracks", "Tracks", page, count, minRatings, ["Path", "LyricsPath"],
                (t, keep) => { t.PlayCounts = keep(t.PlayCounts); t.SkipCounts = keep(t.SkipCounts); t.Ratings = keep(t.Ratings); });

        [HttpGet("albums")]
        public IActionResult Albums(int page = 0, int count = 50, int minRatings = 1) =>
            Ranked<ResponseAlbum>("api/top-rated/albums", "Albums", page, count, minRatings, ["AlbumArtPaths"],
                (a, keep) => { a.PlayCounts = keep(a.PlayCounts); a.SkipCounts = keep(a.SkipCounts); a.Ratings = keep(a.Ratings); });

        [HttpGet("artists")]
        public IActionResult Artists(int page = 0, int count = 50, int minRatings = 1) =>
            Ranked<ResponseArtist>("api/top-rated/artists", "Artists", page, count, minRatings, ["ArtistBannerPaths", "ArtistPfpPaths", "ConnectedArtists"],
                (a, keep) => { a.PlayCounts = keep(a.PlayCounts); a.SkipCounts = keep(a.SkipCounts); a.Ratings = keep(a.Ratings); });

        private IActionResult Ranked<T>(string route, string collection, int page, int count, int minRatings, string[] hidden,
            Action<T, Func<List<UserStat>, List<UserStat>>> trimStats)
        {
            var curId = CurrentUser;
            var args = new WebApiEventArgs(route, curId, new Dictionary<string, object> { { "page", page }, { "count", count }, { "minRatings", minRatings } });
            if (page < 0) return BadRequest("Page must be 0 or more.");
            count = Math.Clamp(count, 1, 500);
            minRatings = Math.Max(1, minRatings);

            var db = StateManager.DbClient.GetDatabase("Melon");
            var visible = db.GetCollection<User>("Users").Find(u => u.PublicStats).Project(u => u._id).ToList().ToHashSet();
            visible.Add(curId);
            var ids = new BsonArray(visible);

            var counted = new BsonDocument("$filter", new BsonDocument
            {
                { "input", new BsonDocument("$ifNull", new BsonArray { "$Ratings", new BsonArray() }) },
                { "as", "r" },
                { "cond", new BsonDocument("$and", new BsonArray
                    {
                        new BsonDocument("$in", new BsonArray { "$$r.UserId", ids }),
                        new BsonDocument("$gt", new BsonArray { "$$r.Value", 0 }),
                    }) },
            });
            var exclude = new BsonDocument { { "_counted", 0 }, { "_avg", 0 }, { "_n", 0 } };
            foreach (var field in hidden) exclude[field] = 0;

            var docs = db.GetCollection<BsonDocument>(collection).Aggregate()
                .Match(new BsonDocument("Ratings", new BsonDocument("$elemMatch", new BsonDocument { { "UserId", new BsonDocument("$in", ids) }, { "Value", new BsonDocument("$gt", 0) } })))
                .AppendStage<BsonDocument>(new BsonDocument("$addFields", new BsonDocument("_counted", counted)))
                .AppendStage<BsonDocument>(new BsonDocument("$addFields", new BsonDocument { { "_avg", new BsonDocument("$avg", "$_counted.Value") }, { "_n", new BsonDocument("$size", "$_counted") } }))
                .Match(new BsonDocument("_n", new BsonDocument("$gte", minRatings)))
                .Sort(new BsonDocument { { "_avg", -1 }, { "_n", -1 }, { "Name", 1 } })
                .Skip(page * count)
                .Limit(count)
                .Project(exclude)
                .ToList();

            List<UserStat> Keep(List<UserStat> stats) => stats?.Where(s => visible.Contains(s.UserId)).ToList() ?? [];
            var items = docs.Select(d => BsonSerializer.Deserialize<T>(d)).ToList();
            foreach (var item in items) trimStats(item, Keep);
            args.SendEvent("Top rated sent", 200, Program.mWebApi);
            return Ok(items);
        }
    }
}
