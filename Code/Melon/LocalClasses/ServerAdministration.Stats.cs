using System.Globalization;
using Melon.Interface;
using Melon.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Melon.LocalClasses;

public sealed partial class ServerAdministration
{
    public ListeningSummary GetListeningSummary(string userId = "", int days = 7)
    {
        if (days is < 1 or > 366) throw new ArgumentException("Choose between 1 and 366 days.");
        var now = DateTime.UtcNow;
        var start = now.Date.AddDays(1 - days);
        var match = new BsonDocument("LogDate", new BsonDocument { { "$gte", start }, { "$lte", now } });
        match.Add("Type", new BsonDocument("$in", new BsonArray { "Play", "Skip" }));
        if (!string.IsNullOrEmpty(userId)) match.Add("UserId", userId);
        var facet = BsonDocument.Parse("""
            { "$facet": {
              "days": [
                { "$group": {
                  "_id": { "$dateToString": { "format":"%Y-%m-%d", "date":"$LogDate" }},
                  "plays": { "$sum": { "$cond":[{ "$eq":["$Type","Play"] },1,0] }},
                  "skips": { "$sum": { "$cond":[{ "$eq":["$Type","Skip"] },1,0] }},
                  "ms": { "$sum": { "$cond":[{ "$eq":["$Type","Play"] },
                    { "$convert":{ "input":"$Duration","to":"double","onError":0,"onNull":0 }},0] }}
                }}
              ],
              "tracks": [
                { "$match":{ "Type":"Play" }},
                { "$group":{ "_id":"$TrackId", "plays":{ "$sum":1 }}},
                { "$sort":{ "plays":-1,"_id":1 }},
                { "$limit":5 },
                { "$lookup":{ "from":"Tracks","localField":"_id","foreignField":"_id","as":"track" }},
                { "$project":{ "plays":1,"name":{ "$arrayElemAt":["$track.Name",0] }}}
              ]
            }}
            """);
        var report = Database.GetCollection<BsonDocument>("Stats").Aggregate<BsonDocument>(
            new[] { new BsonDocument("$match", match), facet }).Single();
        var byDay = report["days"].AsBsonArray.ToDictionary(d => d["_id"].AsString);
        var history = Enumerable.Range(0, days).Select(i =>
        {
            var day = start.AddDays(i);
            return byDay.TryGetValue(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), out var row)
                ? new ListeningDay(day, row["plays"].ToInt64(), row["skips"].ToInt64(), row["ms"].ToDouble())
                : new ListeningDay(day, 0, 0, 0);
        }).ToArray();
        var tracks = report["tracks"].AsBsonArray.Select(row => new RankedTrack(
            row.AsBsonDocument.GetValue("name", row["_id"]).ToString() ?? "", row["plays"].ToInt64())).ToArray();
        return new(history, tracks);
    }

    public void PruneDeletedUserStats() => WithIdleDatabase(() =>
    {
        var history = Database.GetCollection<PlayStat>("Stats");
        var recorded = history.Distinct(s => s.UserId, FilterDefinition<PlayStat>.Empty).ToList().ToHashSet();
        var media = new[] { "Tracks", "Albums", "Artists" }.Select(name => Database.GetCollection<BsonDocument>(name)).ToArray();
        string[] fields = ["PlayCounts", "SkipCounts", "Ratings"];
        foreach (var collection in media)
            foreach (var field in fields)
                recorded.UnionWith(collection.Distinct<string>(field + ".UserId", FilterDefinition<BsonDocument>.Empty).ToList());
        recorded.ExceptWith(Users.Find(FilterDefinition<User>.Empty).Project(u => u._id).ToList());
        if (recorded.Count == 0) return true;
        var ids = new BsonArray(recorded.Where(id => !string.IsNullOrEmpty(id)));
        foreach (var collection in media)
            foreach (var field in fields)
            {
                var match = new BsonDocument("$in", ids);
                collection.UpdateMany(new BsonDocument(field + ".UserId", match),
                    new BsonDocument("$pull", new BsonDocument(field, new BsonDocument("UserId", match))));
            }
        history.DeleteMany(Builders<PlayStat>.Filter.In(s => s.UserId, recorded));
        return true;
    });
}
