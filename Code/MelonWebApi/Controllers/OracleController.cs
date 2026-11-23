using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Melon.LocalClasses;
using Melon.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace MelonWebApi.Controllers
{
    /// <summary>An Oracle Mix: the server's pick for the day or month, the same for every user and device.</summary>
    public class OracleMix
    {
        /// <summary>"daily" or "monthly".</summary>
        public string Period { get; set; }
        /// <summary>The server's local date the mix is for: yyyy-MM-dd (daily) or yyyy-MM (monthly).</summary>
        public string Date { get; set; }
        /// <summary>The genre the oracle drew, or null when the library has no genre big enough to draw from.</summary>
        public string Theme { get; set; }
        /// <summary>When the next mix replaces this one, in UTC.</summary>
        public DateTime NextUtc { get; set; }
        public List<ResponseTrack> Tracks { get; set; }
    }

    /// <summary>
    /// Oracle Mixes. The pick comes from the <b>server's</b> local date, so everyone on the server sees the same mix
    /// whatever their time zone or device. A theme genre is drawn first (from genres with enough tracks to fill a
    /// mix), then tracks from it; a small library is topped up from all tracks. Each track's place is a hash of the
    /// date and its id, so the same day always gives the same mix, and adding tracks changes it only slightly.
    /// </summary>
    [ApiController]
    [Route("api/oracle")]
    [Authorize(Roles = "Admin,User,Pass")]
    public class OracleController : ControllerBase
    {
        // period key ("daily:2026-11-22") → picked theme and track ids. A day's pick is fixed once made, even if the
        // library changes later that day; only the current period of each kind is kept.
        private static readonly ConcurrentDictionary<string, (string Theme, List<string> Ids)> Picks = new();
        private static readonly SemaphoreSlim Picking = new(1, 1);
        private const int MaxTracks = 200;

        private string CurrentUser => ((ClaimsIdentity)User.Identity!).Claims
            .Where(c => c.Type == ClaimTypes.UserData).Select(c => c.Value).FirstOrDefault() ?? "";

        [HttpGet("daily")]
        public Task<IActionResult> Daily(int count = 50) => Mix("daily", count);

        [HttpGet("monthly")]
        public Task<IActionResult> Monthly(int count = 50) => Mix("monthly", count);

        private async Task<IActionResult> Mix(string period, int count)
        {
            var args = new WebApiEventArgs("api/oracle/" + period, CurrentUser, new Dictionary<string, object> { { "count", count } });
            count = Math.Clamp(count, 1, MaxTracks);
            var now = DateTime.Now;
            bool daily = period == "daily";
            string date = daily ? now.ToString("yyyy-MM-dd") : now.ToString("yyyy-MM");
            var next = daily ? now.Date.AddDays(1) : new DateTime(now.Year, now.Month, 1).AddMonths(1);
            string key = period + ":" + date;

            var db = StateManager.DbClient.GetDatabase("Melon");
            if (!Picks.TryGetValue(key, out var pick))
            {
                await Picking.WaitAsync(HttpContext.RequestAborted);
                try
                {
                    if (!Picks.TryGetValue(key, out pick))
                    {
                        pick = await Task.Run(() => Draw(db, key), HttpContext.RequestAborted);
                        foreach (var old in Picks.Keys.Where(k => k.StartsWith(period + ":") && k != key)) Picks.TryRemove(old, out _);
                        Picks[key] = pick;
                    }
                }
                finally { Picking.Release(); }
            }

            var ids = pick.Ids.Take(count).ToList();
            var visible = db.GetCollection<User>("Users").Find(u => u.PublicStats).Project(u => u._id).ToList().ToHashSet();
            visible.Add(CurrentUser);
            var docs = await db.GetCollection<BsonDocument>("Tracks")
                .Find(Builders<BsonDocument>.Filter.In("_id", ids))
                .Project(Builders<BsonDocument>.Projection.Exclude("Path").Exclude("LyricsPath"))
                .ToListAsync(HttpContext.RequestAborted);
            var byId = docs.Select(d => BsonSerializer.Deserialize<ResponseTrack>(d)).ToDictionary(t => t._id);
            List<UserStat> Keep(List<UserStat> stats) => stats?.Where(s => visible.Contains(s.UserId)).ToList() ?? [];
            var tracks = ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList(); // tracks deleted since the pick drop out
            foreach (var t in tracks) { t.PlayCounts = Keep(t.PlayCounts); t.SkipCounts = Keep(t.SkipCounts); t.Ratings = Keep(t.Ratings); }

            args.SendEvent("Oracle mix sent", 200, Program.mWebApi);
            return Ok(new OracleMix { Period = period, Date = date, Theme = pick.Theme, NextUtc = next.ToUniversalTime(), Tracks = tracks });
        }

        private static (string Theme, List<string> Ids) Draw(IMongoDatabase db, string seed)
        {
            var all = db.GetCollection<BsonDocument>("Tracks").Find(FilterDefinition<BsonDocument>.Empty)
                .Project(Builders<BsonDocument>.Projection.Include("_id").Include("TrackGenres")).ToList()
                .Select(d => (Id: d["_id"].ToString()!, Genres: d.TryGetValue("TrackGenres", out var g) && g.IsBsonArray
                    ? g.AsBsonArray.Where(x => x.IsString).Select(x => x.AsString.Trim()).Where(x => x.Length > 0).ToList() : []))
                .ToList();

            // A theme needs enough tracks to make a mix of its own: 25, or a twentieth of a small library.
            int enough = Math.Max(5, Math.Min(25, all.Count / 20));
            var themes = all.SelectMany(t => t.Genres.Distinct(StringComparer.OrdinalIgnoreCase))
                .GroupBy(g => g, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() >= enough)
                .Select(g => g.Key).ToList();
            string theme = themes.OrderBy(g => Rank(seed, "theme|" + g.ToLowerInvariant())).FirstOrDefault();

            IEnumerable<string> Ranked(IEnumerable<string> ids) => ids.OrderBy(id => Rank(seed, id));
            var picked = theme == null ? new List<string>()
                : Ranked(all.Where(t => t.Genres.Contains(theme, StringComparer.OrdinalIgnoreCase)).Select(t => t.Id)).Take(MaxTracks).ToList();
            if (picked.Count < MaxTracks)
            {
                var have = picked.ToHashSet();
                picked.AddRange(Ranked(all.Select(t => t.Id).Where(id => !have.Contains(id))).Take(MaxTracks - picked.Count));
            }
            return (theme, picked);
        }

        private static ulong Rank(string seed, string value)
        {
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(Encoding.UTF8.GetBytes("oracle|" + seed + "|" + value), hash);
            return BitConverter.ToUInt64(hash);
        }
    }
}
